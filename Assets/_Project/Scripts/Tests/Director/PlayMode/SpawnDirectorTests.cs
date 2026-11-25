#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.AI.Core.Director;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Lockdown.Tests.Director
{
    public sealed class SpawnDirectorTests
    {
        private DirectorSettings settings;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (settings != null) Object.DestroyImmediate(settings);
        }

        /// <summary>Loads the museum with a director that acts quickly, without touching the saved settings asset.</summary>
        private IEnumerator LoadFast(System.Action<DirectorTuning> change = null)
        {
            yield return MuseumFixture.Load();
            settings = ScriptableObject.CreateInstance<DirectorSettings>();
            settings.tuning = new DirectorTuning { openingGrace = 0f, spawnCooldown = 1f, decisionInterval = 0.5f, waitUtility = 0f, baseTarget = 0.6f };
            change?.Invoke(settings.tuning);
            var agent = MuseumFixture.Director;
            agent.enabled = false;
            var data = new SerializedObject(agent);
            data.FindProperty("settings").objectReferenceValue = settings;
            data.ApplyModifiedPropertiesWithoutUndo();
            agent.enabled = true;
        }

        [UnityTest]
        public IEnumerator EverySpawnIsHiddenReachableOutsideTheSafeRoomAndUnderTheCaps()
        {
            yield return LoadFast();
            // Nothing can reach the closed garden where the player starts, so test from the Central Hall.
            MuseumFixture.MovePlayer(new Vector3(0f, 0.05f, -3.5f), Quaternion.identity);
            var director = MuseumFixture.Director;
            var player = MuseumFixture.Player;
            var problems = new List<string>();
            var routes = new MuseumRoutes();
            director.EnemySpawned += (enemy, option) =>
            {
                Vector3 feet = enemy.transform.position;
                if (PlayerSight.InView(player.PlayerCamera, feet)) problems.Add(option.Kind + " appeared in view at " + feet);
                if (PlayerSight.HasLine(player.PlayerCamera.transform.position, feet, player.transform, enemy.transform))
                    problems.Add(option.Kind + " appeared in line of sight at " + feet);
                if (option.RoomId == director.SafeRoomId) problems.Add(option.Kind + " appeared in the safe room");
                if (float.IsInfinity(routes.Length(feet, player.transform.position))) problems.Add(option.Kind + " cannot reach the player");
            };
            float deadline = Time.time + 25f;
            while (Time.time < deadline)
            {
                var living = new EnemyRoster();
                living.Refresh(player.transform.position, 25f);
                Assert.That(living.Census.Total, Is.LessThanOrEqualTo(settings.tuning.maximumTotal));
                foreach (var kind in SpawnKinds.All)
                    Assert.That(living.Census.Count(kind), Is.LessThanOrEqualTo(SpawnLimits.Maximum(kind, settings.tuning)), kind.ToString());
                yield return new WaitForSeconds(0.25f);
            }
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(director.SpawnCount, Is.GreaterThan(0), Reasons(director));
        }

        [UnityTest]
        public IEnumerator AnAlarmNobodyCanAnswerBringsInAGuardWhoGoesToTheGlass()
        {
            yield return LoadFast(tuning => { tuning.baseTarget = 0f; tuning.lootTarget = 0f; tuning.waitUtility = 0.15f; });
            MuseumFixture.MovePlayer(new Vector3(-14.6f, 0.05f, 3.6f), Quaternion.Euler(0f, 180f, 0f));
            var director = MuseumFixture.Director;
            GameObject arrival = null;
            director.EnemySpawned += (enemy, option) => { if (option.Kind == SpawnKind.Guard) arrival = arrival != null ? arrival : enemy; };
            yield return new WaitForSeconds(1.5f);
            Assert.That(director.SpawnCount, Is.Zero, "Without a target or an alarm there is nothing to do.");
            var glass = MuseumFixture.CaseNear(new Vector3(-9.75f, 0f, 18.4f));
            MuseumFixture.Break(glass);
            float deadline = Time.time + 6f;
            while (Time.time < deadline && arrival == null) yield return null;
            Assert.That(arrival, Is.Not.Null, Reasons(director));
            var guard = arrival.GetComponent<PursuerAgent>();
            Assert.That(MuseumFixture.Relay.Responder, Is.EqualTo(guard));
            Assert.That(MuseumFixture.Relay.IsUnanswered, Is.False);
            yield return null;
            yield return null;
            Assert.That(guard.IsAnsweringAlarm, Is.True, guard.DecisionReason);
            deadline = Time.time + 25f;
            while (Time.time < deadline && Flat(guard.transform.position, glass.transform.position) > 2.4f) yield return null;
            Assert.That(Flat(guard.transform.position, glass.transform.position), Is.LessThan(2.4f), guard.State + " " + guard.DecisionReason);
        }

        [UnityTest]
        public IEnumerator NothingSpawnsAfterCapture()
        {
            yield return LoadFast();
            var director = MuseumFixture.Director;
            MuseumFixture.Player.GetComponent<PlayerVitals>().Capture();
            yield return new WaitForSeconds(3f);
            Assert.That(director.SpawnCount, Is.Zero);
            Assert.That(director.Observation.CanAct, Is.False);
        }

        [UnityTest]
        public IEnumerator DamageRaisesStressAndIntensity()
        {
            yield return LoadFast(tuning => { tuning.maximumTotal = 0; });
            var director = MuseumFixture.Director;
            yield return new WaitForSeconds(0.6f);
            float before = director.Brain.Stress;
            MuseumFixture.Player.GetComponent<Health>().TakeDamage(40f);
            yield return new WaitForSeconds(0.6f);
            Assert.That(director.Observation.RecentDamage, Is.GreaterThan(0.3f));
            Assert.That(director.Observation.HealthFraction, Is.EqualTo(0.6f).Within(0.01f));
            Assert.That(director.Brain.Stress, Is.GreaterThan(before + 0.15f));
            Assert.That(director.Brain.Intensity, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator GlassHoundsRespectTheDirectorsCaps()
        {
            yield return LoadFast(tuning => { tuning.maximumHounds = 0; });
            var response = Object.FindAnyObjectByType<GlassHoundResponse>();
            yield return new WaitForSeconds(0.2f);
            MuseumFixture.Break(MuseumFixture.CaseNear(Vector3.zero));
            yield return new WaitForSeconds(2f);
            Assert.That(response.Pack.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FarIdleUnseenSpawnsAreRemovedButPlacedEnemiesStay()
        {
            yield return LoadFast(tuning => { tuning.despawnRoute = 1f; tuning.despawnUnseenSeconds = 1f; tuning.spawnCooldown = 100f; });
            var director = MuseumFixture.Director;
            MuseumFixture.MovePlayer(new Vector3(-14.6f, 0.05f, 3.6f), Quaternion.Euler(0f, 180f, 0f));
            float deadline = Time.time + 12f;
            while (Time.time < deadline && director.Spawned.Removed == 0) yield return null;
            Assert.That(director.SpawnCount, Is.EqualTo(1), Reasons(director));
            Assert.That(director.Spawned.Removed, Is.EqualTo(1));
            Assert.That(MuseumFixture.Guard, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator DecisionsAreLoggedWithTheirReasons()
        {
            yield return LoadFast();
            yield return new WaitForSeconds(2f);
            var log = MuseumFixture.Director.Log;
            Assert.That(log.Count, Is.GreaterThan(1));
            Assert.That(log[0].Decision.Reason, Is.Not.Empty);
            Assert.That(log.ToCsv().Split('\n').Length, Is.EqualTo(log.Count + 2));
        }

        [UnityTest]
        public IEnumerator SpawnPointsStandOnTheNavMeshWithRoomForAnEnemy()
        {
            yield return MuseumFixture.Load();
            var points = Object.FindObjectsByType<SpawnPoint>();
            Assert.That(points.Length, Is.EqualTo(17));
            foreach (var point in points)
            {
                Assert.That(NavMesh.SamplePosition(point.transform.position, out _, 0.2f, NavMesh.AllAreas), Is.True, point.name);
                Assert.That(SpawnPointSurvey.IsClear(point.transform.position), Is.True, point.name);
                Assert.That(point.RoomId, Is.Not.Empty.And.Not.EqualTo(MuseumFixture.Director.SafeRoomId), point.name);
            }
        }

        private static string Reasons(SpawnDirectorAgent director) => string.Join("; ", Enumerable.Range(0, director.Log.Count)
            .Select(i => director.Log[i].Phase + ":" + director.Log[i].Decision.Reason).Distinct());

        private static float Flat(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
    }
}
#endif

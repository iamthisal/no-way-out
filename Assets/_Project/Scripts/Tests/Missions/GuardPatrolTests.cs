#if UNITY_EDITOR
using System.Collections;
using System.Text.RegularExpressions;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class GuardPatrolTests
    {
        private static readonly Vector3 Arena = new Vector3(500f, 0f, 500f);
        private bool previousBackground;
        private GameObject fixture;
        private GuardSenses guard;
        private WanderingEnemy legs;
        private FirstPersonController player;
        private MuseumAlarm alarm;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        [TearDown]
        public void Cleanup()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
            Application.runInBackground = previousBackground;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator GuardRaisesTheAlarmOnlyAfterHisSuspicionFills()
        {
            yield return BuildArena(new Vector3(0f, 0f, 6f));
            yield return new WaitForSeconds(0.25f);
            Assert.That(guard.CanSeePlayer, Is.True);
            Assert.That(guard.Suspicion, Is.GreaterThan(0f).And.LessThan(1f), "Six metres away, noticing takes a moment.");
            Assert.That(guard.IsChasing, Is.False);
            Assert.That(alarm.IsOn, Is.False);
            Assert.That(guard.GetComponent<EnemyShooting>().HoldFire, Is.True, "No shots before he has spotted the player.");
            float deadline = Time.time + 2f;
            while (!guard.IsChasing && Time.time < deadline) yield return null;
            Assert.That(guard.IsChasing, Is.True);
            Assert.That(alarm.IsOn, Is.True);
            Assert.That(alarm.Cause, Is.EqualTo("The guard spotted you"));
            Assert.That(legs.IsPursuing, Is.True);
            yield return null;
            Assert.That(guard.GetComponent<EnemyShooting>().HoldFire, Is.False);
        }

        [UnityTest]
        public IEnumerator GuardMissesAPlayerBehindHimOrBehindAWall()
        {
            yield return BuildArena(new Vector3(0f, 0f, -4f));
            yield return new WaitForSeconds(0.6f);
            Assert.That(guard.CanSeePlayer, Is.False, "Behind his back.");
            Assert.That(guard.Suspicion, Is.Zero);
            Move(new Vector3(0f, 0f, 9.5f));
            yield return new WaitForSeconds(0.6f);
            Assert.That(guard.CanSeePlayer, Is.False, "Ahead and in range, but behind the wall.");
            Assert.That(guard.Suspicion, Is.Zero);
            Assert.That(alarm.IsOn, Is.False);
        }

        [UnityTest]
        public IEnumerator GuardGivesUpWhenHeLosesThePlayerButTheAlarmStaysOn()
        {
            yield return BuildArena(new Vector3(0f, 0f, 3f));
            float deadline = Time.time + 2f;
            while (!guard.IsChasing && Time.time < deadline) yield return null;
            Assert.That(guard.IsChasing, Is.True);
            Move(new Vector3(0f, 0f, 12f));
            yield return new WaitForSeconds(1.6f);
            Assert.That(guard.IsChasing, Is.False, "Out of sight for longer than he searches.");
            Assert.That(guard.Suspicion, Is.Zero);
            Assert.That(legs.IsPursuing, Is.False);
            Assert.That(guard.GetComponent<EnemyShooting>().HoldFire, Is.True);
            Assert.That(alarm.IsOn, Is.True, "Only the Security computer will turn the alarm off.");
        }

        [UnityTest]
        public IEnumerator ShootingTheGuardGivesThePlayerAwayAtOnce()
        {
            yield return BuildArena(new Vector3(0f, 0f, -4f));
            Assert.That(guard.IsChasing, Is.False);
            guard.GetComponent<Health>().TakeDamage(25f);
            Assert.That(guard.IsChasing, Is.True);
            Assert.That(alarm.IsOn, Is.True);
        }

        [UnityTest]
        public IEnumerator MuseumGuardPatrolsTheGalleriesAndLeavesTheLobbyAlone()
        {
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979")))
            {
                LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
                LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
            }
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var museumGuard = Object.FindAnyObjectByType<GuardSenses>();
            Assert.That(museumGuard, Is.Not.Null, "The museum's wandering enemy is the guard.");
            var walker = museumGuard.GetComponent<WanderingEnemy>();
            Assert.That(walker.IsPatrolling, Is.True);
            Assert.That(walker.StopCount, Is.EqualTo(13));
            var egypt = new Rect(-10.5f, 14f, 7f, 8f);
            var storage = new Rect(5f, -3f, 9f, 8f);
            var security = new Rect(5f, -11f, 6f, 5f);
            bool visitsStoneAge = false, visitsLobby = false;
            for (int i = 0; i < walker.StopCount; i++)
            {
                var stop = new Vector2(walker.Stop(i).x, walker.Stop(i).z);
                Assert.That(egypt.Contains(stop) || storage.Contains(stop) || security.Contains(stop), Is.False, "Stop " + i + " is in a closed room.");
                visitsStoneAge |= new Rect(-10.5f, 22f, 7f, 8f).Contains(stop);
                visitsLobby |= new Rect(-5f, -11f, 10f, 6f).Contains(stop);
            }
            Assert.That(visitsStoneAge && visitsLobby, Is.True, "The route reaches the back galleries and the lobby.");
            Assert.That(MuseumAlarm.Current, Is.Not.Null);
            Vector3 start = walker.transform.position;
            yield return new WaitForSeconds(6f);
            Assert.That(Vector3.Distance(start, walker.transform.position), Is.GreaterThan(2f), "He sets off on his route.");
            Assert.That(walker.NextStop, Is.LessThanOrEqualTo(1), "His first stops are Central Hall and the Exhibition.");
            Assert.That(museumGuard.IsChasing, Is.False, "He heads north, away from the player in the lobby.");
            Assert.That(MuseumAlarm.Current.IsOn, Is.False);
        }

        // A flat arena with a wall 8 m in front of a guard who stands still facing +Z.
        private IEnumerator BuildArena(Vector3 playerOffset)
        {
            foreach (var other in Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude)) other.gameObject.SetActive(false);
            fixture = new GameObject("Guard arena");
            fixture.transform.position = Arena;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(fixture.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(40f, 1f, 40f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.5f, 8f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.2f);
            var surface = fixture.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            alarm = new GameObject("Alarm").AddComponent<MuseumAlarm>();
            alarm.transform.SetParent(fixture.transform, false);

            var holder = new GameObject("Guard holder");
            holder.SetActive(false);
            holder.transform.SetParent(fixture.transform, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/WanderingEnemy.prefab");
            legs = Object.Instantiate(prefab, Arena, Quaternion.identity, holder.transform).GetComponent<WanderingEnemy>();
            // Standing still keeps him facing +Z until something makes him chase.
            legs.GetComponent<NavMeshAgent>().speed = 0f;
            guard = legs.gameObject.AddComponent<GuardSenses>();
            var settings = new SerializedObject(guard);
            settings.FindProperty("startGrace").floatValue = 0f;
            settings.FindProperty("giveUpAfter").floatValue = 1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab"),
                fixture.transform).GetComponent<FirstPersonController>();
            Move(playerOffset);
            player.CaptureCursor();
            holder.SetActive(true);
            yield return null;
            Assert.That(legs.isActiveAndEnabled, Is.True, "The guard found the arena's NavMesh.");
        }

        private void Move(Vector3 offset)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = Arena + offset + Vector3.up * 0.05f;
            // Face the guard, as a player sneaking past would glance at him.
            player.transform.rotation = Quaternion.LookRotation(new Vector3(-offset.x, 0f, -offset.z).normalized + Vector3.forward * 1e-4f);
            body.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
#endif

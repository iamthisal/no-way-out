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
    public sealed class SecurityRobotTests
    {
        private const string RobotPath = "Assets/_Project/Prefabs/Characters/SecurityRobot.prefab";
        private static readonly Vector3 Arena = new Vector3(-500f, 0f, 500f);
        private bool previousBackground;
        private GameObject fixture;
        private SecurityRobot robot;
        private RobotLaser laser;
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
        public IEnumerator AlarmWakesTheRobotAndItLasersAPlayerInSight()
        {
            yield return BuildArena(new Vector3(0f, 0f, 6f));
            var health = player.GetComponent<Health>();
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.Docked));
            alarm.Raise("Test", player.transform.position);
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.PoweringUp));
            yield return WaitFor(() => robot.State == SecurityRobot.Mode.Attacking, 3f);
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.Attacking));
            yield return WaitFor(() => laser.ShotsFired > 0, 3f);
            Assert.That(laser.Hits, Is.EqualTo(1));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f - laser.Damage));
        }

        [UnityTest]
        public IEnumerator SidesteppingWhileTheLaserChargesMakesItMiss()
        {
            yield return BuildArena(new Vector3(0f, 0f, 6f));
            var health = player.GetComponent<Health>();
            alarm.Raise("Test", player.transform.position);
            yield return WaitFor(() => laser.IsCharging, 4f);
            Assert.That(laser.IsCharging, Is.True, "The eye glows white before each shot.");
            Move(new Vector3(2.5f, 0f, 6f));
            yield return WaitFor(() => laser.ShotsFired > 0, 2f);
            Assert.That(laser.ShotsFired, Is.EqualTo(1));
            Assert.That(laser.Hits, Is.Zero, "The shot goes where the player was when it locked on.");
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator ThreePistolShotsDestroyARobotForGood()
        {
            yield return BuildArena(new Vector3(0f, 0f, -20f));
            alarm.Raise("Test", Arena + new Vector3(0f, 0f, 5f));
            var health = robot.GetComponent<Health>();
            health.TakeDamage(25f);
            health.TakeDamage(25f);
            Assert.That(robot.State, Is.Not.EqualTo(SecurityRobot.Mode.Destroyed));
            health.TakeDamage(25f);
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.Destroyed));
            Assert.That(robot.GetComponent<NavMeshAgent>().enabled, Is.False);
            Assert.That(robot.IsAway, Is.False, "A wreck no longer needs the bay door.");
            alarm.Raise("Test", Arena + new Vector3(0f, 0f, 3f));
            yield return new WaitForSeconds(0.5f);
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.Destroyed), "Destroyed robots stay down.");
            Assert.That(laser.ShotsFired, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SwitchingTheAlarmOffSendsTheRobotHomeToItsCharger()
        {
            // The player is far out of sight; the robot searches where the noise was.
            yield return BuildArena(new Vector3(0f, 0f, -20f));
            alarm.Raise("Test", Arena + new Vector3(0f, 0f, 7f));
            yield return WaitFor(() => Vector3.Distance(robot.transform.position, robot.DockPosition) > 2f, 5f);
            Assert.That(robot.IsAway, Is.True);
            alarm.Disable();
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.Returning));
            yield return WaitFor(() => robot.State == SecurityRobot.Mode.Docked, 8f);
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.Docked));
            Assert.That(Vector3.Distance(robot.transform.position, robot.DockPosition), Is.LessThan(0.6f));
            alarm.Raise("Test", Arena + new Vector3(0f, 0f, 7f));
            Assert.That(robot.State, Is.EqualTo(SecurityRobot.Mode.Docked), "Once switched off, the alarm cannot wake it.");
        }

        [UnityTest]
        public IEnumerator BrokenGlassSendsRobotsFromTheirCornersStraightToThePlayer()
        {
            yield return LoadMuseum();
            var robots = Object.FindObjectsByType<SecurityRobot>(FindObjectsInactive.Exclude);
            Assert.That(robots.Length, Is.EqualTo(2));
            Assert.That(Vector3.Distance(robots[0].DockPosition, robots[1].DockPosition), Is.GreaterThan(20f), "The robots wait in far-apart corners.");
            foreach (var each in robots) Assert.That(each.State, Is.EqualTo(SecurityRobot.Mode.Docked));

            // The player stands in the Exhibition while a case breaks at the far end of the museum.
            Move(new Vector3(0f, 0f, 8f));
            BreakableGlass farthest = null;
            foreach (var glass in Object.FindObjectsByType<BreakableGlass>(FindObjectsInactive.Exclude))
                if (farthest == null || Vector3.Distance(glass.transform.position, player.transform.position) > Vector3.Distance(farthest.transform.position, player.transform.position))
                    farthest = glass;
            farthest.GetComponent<Health>().TakeDamage(1000f);
            yield return null;
            foreach (var each in robots)
            {
                Assert.That(each.IsAway, Is.True, "Breaking glass wakes every robot at once.");
                Vector3 offset = each.Target - player.transform.position;
                offset.y = 0f;
                Assert.That(offset.magnitude, Is.LessThan(1f), "They head for where the player is, not for the broken case.");
            }
            yield return new WaitForSeconds(2f);
            foreach (var each in robots)
                Assert.That(Vector3.Distance(each.transform.position, each.DockPosition), Is.GreaterThan(2f), each.name + " sets off at once.");

            MuseumAlarm.Current.Disable();
            yield return WaitFor(() => System.Array.TrueForAll(robots, r => r.State == SecurityRobot.Mode.Docked), 10f);
            foreach (var each in robots)
            {
                Assert.That(each.State, Is.EqualTo(SecurityRobot.Mode.Docked), each.name + " goes back to its charger.");
                Assert.That(Vector3.Distance(each.transform.position, each.DockPosition), Is.LessThan(0.6f));
            }
        }

        [UnityTest]
        public IEnumerator OnceTheAlarmIsOffBrokenGlassLeavesTheRobotsCharging()
        {
            yield return LoadMuseum();
            MuseumAlarm.Current.Disable();
            Object.FindAnyObjectByType<BreakableGlass>().GetComponent<Health>().TakeDamage(1000f);
            yield return new WaitForSeconds(1.5f);
            foreach (var each in Object.FindObjectsByType<SecurityRobot>(FindObjectsInactive.Exclude))
                Assert.That(each.State, Is.EqualTo(SecurityRobot.Mode.Docked));
        }

        private IEnumerator LoadMuseum()
        {
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979")))
            {
                LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
                LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
            }
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var senses in Object.FindObjectsByType<GuardSenses>(FindObjectsInactive.Exclude)) senses.enabled = false;
            foreach (var shooting in Object.FindObjectsByType<EnemyShooting>(FindObjectsInactive.Exclude)) shooting.enabled = false;
            player = Object.FindAnyObjectByType<FirstPersonController>();
            player.CaptureCursor();
            yield return null;
        }

        // A flat arena where a robot rests facing +Z on its charger.
        private IEnumerator BuildArena(Vector3 playerOffset)
        {
            foreach (var other in Object.FindObjectsByType<FirstPersonController>(FindObjectsInactive.Exclude)) other.gameObject.SetActive(false);
            fixture = new GameObject("Robot arena");
            fixture.transform.position = Arena;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(fixture.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(60f, 1f, 60f);
            var surface = fixture.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            alarm = new GameObject("Alarm").AddComponent<MuseumAlarm>();
            alarm.transform.SetParent(fixture.transform, false);
            robot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RobotPath), Arena, Quaternion.identity, fixture.transform)
                .GetComponent<SecurityRobot>();
            laser = robot.GetComponent<RobotLaser>();
            player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab"),
                fixture.transform).GetComponent<FirstPersonController>();
            Move(playerOffset);
            player.CaptureCursor();
            yield return null;
            Assert.That(robot.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True, "The robot found the arena's NavMesh.");
        }

        private void Move(Vector3 offset)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = (fixture != null ? Arena : Vector3.zero) + offset + Vector3.up * 0.05f;
            body.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator WaitFor(System.Func<bool> condition, float seconds)
        {
            float deadline = Time.time + seconds;
            while (!condition() && Time.time < deadline) yield return null;
        }
    }
}
#endif

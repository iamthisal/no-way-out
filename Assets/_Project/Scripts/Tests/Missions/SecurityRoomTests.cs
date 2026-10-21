#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.Weapons;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class SecurityRoomTests
    {
        private static readonly string[] SecurityDoors = { "Door_D09_Security", "Door_D10_Security" };
        private bool previousBackground;
        private GameObject fixture;

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

        [Test]
        public void BreakableLockGivesWayToOneHammerBlowButKeyLocksIgnoreDamage()
        {
            fixture = new GameObject("Lock fixture");
            var breakable = Lock(fixture.transform, true);
            var keyed = Lock(fixture.transform, false);
            Assert.That(breakable.GetComponent<Door>().InteractionLabel, Is.EqualTo("Locked. Break the lock with the hammer"));
            breakable.TakeDamage(25f);
            Assert.That(breakable.IsLocked, Is.True, "A pistol shot is not enough.");
            breakable.TakeDamage(100f);
            Assert.That(breakable.IsLocked, Is.False);
            Assert.That(breakable.IsBroken, Is.True);
            Assert.That(breakable.GetComponent<Door>().InteractionLabel, Is.EqualTo("Open door"));
            keyed.TakeDamage(1000f);
            Assert.That(keyed.IsLocked, Is.True, "Keycard locks cannot be smashed.");
        }

        [UnityTest]
        public IEnumerator HammerQuietlyBreaksBothSecurityLocks()
        {
            yield return LoadMuseum();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            Assert.That(player.GetComponent<PlayerEquipment>().TryCollectHammer(), Is.True);
            var hammer = player.GetComponentInChildren<FirstPersonHammer>(true);
            foreach (var name in SecurityDoors)
            {
                var door = GameObject.Find("Museum/Doors/" + name).GetComponentInChildren<Door>(true);
                var doorLock = door.GetComponent<DoorLock>();
                Assert.That(doorLock.IsBreakable && doorLock.IsLocked, Is.True, name + " starts with a breakable lock.");
                yield return Face(player, interaction, door);
                Assert.That(door.InteractionLabel, Is.EqualTo("Locked. Break the lock with the hammer"));
                Assert.That(interaction.TryInteract(), Is.False);
                yield return new WaitForSeconds(0.7f);
                Assert.That(hammer.TrySwing(), Is.True);
                yield return new WaitForSeconds(0.4f);
                Assert.That(doorLock.IsBroken, Is.True, "One hammer blow breaks " + name + "'s lock.");
                Assert.That(MuseumAlarm.Current.IsOn, Is.False, "Breaking the lock is quiet.");
                Assert.That(interaction.TryInteract(), Is.True);
                Assert.That(door.IsOpen, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator SecurityComputerTurnsTheAlarmOff()
        {
            yield return LoadMuseum();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var console = Object.FindAnyObjectByType<AlarmConsole>();
            Assert.That(console, Is.Not.Null);
            var alarm = MuseumAlarm.Current;
            alarm.Raise("Test");
            var screen = console.GetComponent<Collider>().bounds.center;
            Move(player, new Vector3(screen.x, 0f, screen.z + 1.5f));
            Aim(player, screen);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(console));
            Assert.That(console.InteractionLabel, Is.EqualTo("Turn off the alarm"));
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(alarm.IsOn, Is.False);
            Assert.That(console.InteractionLabel, Is.EqualTo("Security console: all quiet"));
            Assert.That(interaction.TryInteract(), Is.False, "Nothing to turn off.");
        }

        [UnityTest]
        public IEnumerator BreakingDisplayGlassRaisesTheAlarm()
        {
            yield return LoadMuseum();
            Assert.That(Object.FindAnyObjectByType<SkeletonBody>(), Is.Null, "The skeleton archer is out of the museum until the dinosaur-room step.");
            Assert.That(MuseumAlarm.Current.IsOn, Is.False);
            var glass = Object.FindAnyObjectByType<BreakableGlass>();
            glass.GetComponent<Health>().TakeDamage(1000f);
            yield return null;
            Assert.That(glass.IsBroken, Is.True);
            Assert.That(MuseumAlarm.Current.IsOn, Is.True);
            Assert.That(MuseumAlarm.Current.Cause, Is.EqualTo(MuseumAlarm.GlassCause));
        }

        private static DoorLock Lock(Transform parent, bool breakable)
        {
            var hinge = new GameObject(breakable ? "Breakable" : "Keyed");
            hinge.SetActive(false);
            hinge.transform.SetParent(parent, false);
            hinge.AddComponent<BoxCollider>();
            hinge.AddComponent<Door>();
            var doorLock = hinge.AddComponent<DoorLock>();
            var serialized = new SerializedObject(doorLock);
            serialized.FindProperty("breakable").boolValue = breakable;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            hinge.SetActive(true);
            return doorLock;
        }

        // The museum, with the guard's eyes closed so he cannot raise the alarm during the test.
        private static IEnumerator LoadMuseum()
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
            Object.FindAnyObjectByType<FirstPersonController>().CaptureCursor();
            Assert.That(MuseumAlarm.Current, Is.Not.Null);
        }

        private static IEnumerator Face(FirstPersonController player, PlayerInteraction interaction, Door door)
        {
            var leaf = door.GetComponent<BoxCollider>().bounds.center;
            foreach (float side in new[] { 1f, -1f })
            {
                Move(player, leaf + door.transform.forward * side * 1.6f);
                Aim(player, leaf);
                yield return null;
                Physics.SyncTransforms();
                if (interaction.CurrentTarget == (Lockdown.Interfaces.IInteractable)door) yield break;
            }
            Assert.Fail(door.transform.parent.name + " cannot be reached from either side.");
        }

        private static void Move(FirstPersonController player, Vector3 position)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            position.y = 0.05f;
            player.transform.position = position;
            body.enabled = true;
        }

        private static void Aim(FirstPersonController player, Vector3 target)
        {
            var direction = target - player.PlayerCamera.transform.position;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f);
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            typeof(FirstPersonController).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            player.CaptureCursor();
        }
    }
}
#endif

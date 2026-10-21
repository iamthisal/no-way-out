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
        // From the Staff Corridor the hammer breaks in; the lobby door stays sealed.
        private const string CorridorDoor = "Door_D09_Security";
        private const string LobbyDoor = "Door_D10_Security";
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
        public void BreakableLockGivesWayToOneHammerBlowButOtherLocksIgnoreDamage()
        {
            fixture = new GameObject("Lock fixture");
            var breakable = Lock(fixture.transform, true);
            var keyed = Lock(fixture.transform, false);
            var sealedLock = Lock(fixture.transform, false, "");
            Assert.That(sealedLock.GetComponent<Door>().InteractionLabel, Is.EqualTo("This door is sealed"));
            sealedLock.TakeDamage(1000f);
            Assert.That(sealedLock.IsLocked, Is.True, "A sealed door cannot be smashed open.");
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
        public IEnumerator HammerQuietlyBreaksTheCorridorLockButTheLobbyDoorStaysSealed()
        {
            yield return LoadMuseum();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            Assert.That(player.GetComponent<PlayerEquipment>().TryCollectHammer(), Is.True);
            var hammer = player.GetComponentInChildren<FirstPersonHammer>(true);

            var lobbyDoor = GameObject.Find("Museum/Doors/" + LobbyDoor).GetComponentInChildren<Door>(true);
            var seal = lobbyDoor.GetComponent<DoorLock>();
            Assert.That(seal.IsLocked && !seal.IsBreakable, Is.True, "The lobby door into Security is sealed.");
            yield return Face(player, interaction, lobbyDoor);
            Assert.That(lobbyDoor.InteractionLabel, Is.EqualTo("This door is sealed"));
            yield return new WaitForSeconds(0.7f);
            Assert.That(hammer.TrySwing(), Is.True);
            yield return new WaitForSeconds(0.4f);
            Assert.That(seal.IsLocked, Is.True, "The hammer cannot open the sealed door.");
            Assert.That(interaction.TryInteract(), Is.False);

            var door = GameObject.Find("Museum/Doors/" + CorridorDoor).GetComponentInChildren<Door>(true);
            var doorLock = door.GetComponent<DoorLock>();
            Assert.That(doorLock.IsBreakable && doorLock.IsLocked, Is.True, "The corridor door has a breakable lock.");
            yield return Face(player, interaction, door);
            Assert.That(door.InteractionLabel, Is.EqualTo("Locked. Break the lock with the hammer"));
            Assert.That(interaction.TryInteract(), Is.False);
            yield return new WaitForSeconds(0.7f);
            Assert.That(hammer.TrySwing(), Is.True);
            yield return new WaitForSeconds(0.4f);
            Assert.That(doorLock.IsBroken, Is.True, "One hammer blow breaks the corridor door's lock.");
            Assert.That(MuseumAlarm.Current.IsOn, Is.False, "Breaking the lock is quiet.");
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(door.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator SecurityComputerSwitchesTheAlarmOffForGood()
        {
            yield return LoadMuseum();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var console = Object.FindAnyObjectByType<AlarmConsole>();
            Assert.That(console, Is.Not.Null);
            var alarm = MuseumAlarm.Current;
            var screen = console.GetComponent<Collider>().bounds.center;
            Move(player, new Vector3(screen.x, 0f, screen.z + 1.5f));
            Aim(player, screen);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(console));
            Assert.That(console.InteractionLabel, Is.EqualTo("Switch off the alarm system"));
            alarm.Raise("Test");
            Assert.That(console.InteractionLabel, Is.EqualTo("Turn off the alarm"));
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(alarm.IsOn, Is.False);
            Assert.That(alarm.IsDisabled, Is.True);
            Assert.That(console.InteractionLabel, Is.EqualTo("Alarm system offline"));
            Assert.That(interaction.TryInteract(), Is.False, "Nothing left to switch off.");
            alarm.Raise("The guard spotted you");
            Object.FindAnyObjectByType<BreakableGlass>().GetComponent<Health>().TakeDamage(1000f);
            yield return null;
            Assert.That(alarm.IsOn, Is.False, "Once off, nothing raises the alarm again.");
        }

        [UnityTest]
        public IEnumerator TheOnlyPistolWaitsInTheSecurityLocker()
        {
            yield return LoadMuseum();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var equipment = player.GetComponent<PlayerEquipment>();
            Assert.That(equipment.HasPistol, Is.False, "The player starts without a gun.");
            Assert.That(equipment.TryEquipPistol(), Is.False);
            var pickup = Object.FindAnyObjectByType<PistolPickup>();
            Assert.That(pickup, Is.Not.Null);
            Assert.That(pickup.name, Is.EqualTo("P_Security_Locker"));
            var locker = pickup.GetComponent<Collider>().bounds;
            // The locker's doors face south into the room; the shelf stands a little further south.
            Move(player, new Vector3(locker.center.x, 0f, locker.min.z - 0.9f));
            Aim(player, locker.center + Vector3.up * 0.3f);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(pickup));
            Assert.That(pickup.InteractionLabel, Is.EqualTo("Take the pistol (2 to equip)"));
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(equipment.HasPistol, Is.True);
            Assert.That(equipment.IsPistolEquipped, Is.True, "Taking the pistol puts it in the player's hands.");
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.Null, "The empty locker has nothing more to offer.");
        }

        [UnityTest]
        public IEnumerator OnceTheAlarmIsOffBreakingGlassBringsTheGuardToLook()
        {
            yield return LoadMuseum(guardListening: true);
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            // Out of the guard's sight, inside the Security room.
            Move(player, new Vector3(8f, 0f, -8.6f));
            MuseumAlarm.Current.Disable();
            var guard = Object.FindAnyObjectByType<GuardSenses>();
            BreakableGlass nearest = null;
            foreach (var glass in Object.FindObjectsByType<BreakableGlass>(FindObjectsInactive.Exclude))
                if (nearest == null || Vector3.Distance(glass.transform.position, guard.transform.position) < Vector3.Distance(nearest.transform.position, guard.transform.position))
                    nearest = glass;
            nearest.GetComponent<Health>().TakeDamage(1000f);
            yield return null;
            Assert.That(MuseumAlarm.Current.IsOn, Is.False);
            Assert.That(guard.IsInvestigating, Is.True, "The guard comes to see what broke.");
            Assert.That(Vector3.Distance(guard.Noise, nearest.transform.position), Is.LessThan(0.01f));
            Assert.That(guard.GetComponent<WanderingEnemy>().IsPursuing, Is.True);
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

        private static DoorLock Lock(Transform parent, bool breakable, string keyId = "StaffKeycard")
        {
            var hinge = new GameObject(breakable ? "Breakable" : "Keyed " + keyId);
            hinge.SetActive(false);
            hinge.transform.SetParent(parent, false);
            hinge.AddComponent<BoxCollider>();
            hinge.AddComponent<Door>();
            var doorLock = hinge.AddComponent<DoorLock>();
            var serialized = new SerializedObject(doorLock);
            serialized.FindProperty("breakable").boolValue = breakable;
            serialized.FindProperty("keyId").stringValue = breakable ? "" : keyId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            hinge.SetActive(true);
            return doorLock;
        }

        // The museum, usually with the guard's senses off so he cannot raise the alarm during the test.
        private static IEnumerator LoadMuseum(bool guardListening = false)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979")))
            {
                LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
                LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
            }
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            if (!guardListening)
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

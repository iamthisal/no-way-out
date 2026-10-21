#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Lockdown.Enemies;
using Lockdown.Player;
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
    public sealed class StorageKeycardTests
    {
        private const string KeyId = "StorageKeycard";
        private static readonly string[] StorageDoors = { "Door_D06_Storage", "Door_D07_Storage", "Door_D08_Storage" };
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

        [UnityTest]
        public IEnumerator LockedDoorOpensOnlyForAPlayerCarryingItsKey()
        {
            fixture = new GameObject("Lock fixture");
            fixture.transform.position = new Vector3(300f, 0f, 300f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(fixture.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            var hinge = new GameObject("Hinge");
            hinge.SetActive(false);
            hinge.transform.SetParent(fixture.transform, false);
            hinge.transform.localPosition = new Vector3(-0.75f, 1.25f, 2f);
            var panel = hinge.AddComponent<BoxCollider>();
            panel.center = new Vector3(0.75f, 0f, 0f);
            panel.size = new Vector3(1.5f, 2.5f, 0.12f);
            var light = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(light.GetComponent<Collider>());
            light.transform.SetParent(fixture.transform, false);
            var door = hinge.AddComponent<Door>();
            var doorLock = hinge.AddComponent<DoorLock>();
            var serialized = new SerializedObject(doorLock);
            serialized.FindProperty("indicators").arraySize = 1;
            serialized.FindProperty("indicators").GetArrayElementAtIndex(0).objectReferenceValue = light.GetComponent<Renderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            hinge.SetActive(true);
            var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab"),
                fixture.transform).GetComponent<FirstPersonController>();
            player.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            var interaction = player.GetComponent<PlayerInteraction>();
            Aim(player, panel.bounds.center);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(door));
            Assert.That(door.InteractionLabel, Is.EqualTo("Locked. Needs the Storage keycard"));
            Assert.That(ColourDistance(LightColour(light), new Color(0.9f, 0.08f, 0.06f)), Is.LessThan(0.002f), "The reader shows red while locked.");
            Assert.That(interaction.TryInteract(), Is.False);
            Assert.That(door.IsOpen, Is.False);

            PlayerKeys.For(player.gameObject).Add("SomeOtherKey");
            Assert.That(interaction.TryInteract(), Is.False, "Only the matching key opens the lock.");
            PlayerKeys.For(player.gameObject).Add(KeyId);
            Assert.That(door.InteractionLabel, Is.EqualTo("Unlock with the Storage keycard"));
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(door.IsOpen, Is.True);
            Assert.That(doorLock.IsLocked, Is.False);
            Assert.That(ColourDistance(LightColour(light), new Color(0.1f, 0.85f, 0.25f)), Is.LessThan(0.002f), "The reader turns green once unlocked.");
            Assert.That(door.InteractionLabel, Is.EqualTo("Close door"));
        }

        [UnityTest]
        public IEnumerator StorageStaysLockedUntilTheStoneAgeKeycardIsTaken()
        {
            ExpectMissingExteriorPrefab();
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var shooting in Object.FindObjectsByType<EnemyShooting>(FindObjectsInactive.Exclude)) shooting.enabled = false;
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            player.CaptureCursor();

            var hammer = Object.FindAnyObjectByType<HammerPickup>();
            var storage = new Bounds(new Vector3(9.5f, 1f, 1f), new Vector3(9f, 3f, 8f));
            Assert.That(storage.Contains(hammer.transform.position), Is.True, "The hammer waits inside the Storage room.");

            foreach (var name in StorageDoors)
            {
                var door = DoorNamed(name);
                Assert.That(door.GetComponent<DoorLock>().IsLocked, Is.True, name + " starts locked.");
                yield return Face(player, interaction, door);
                Assert.That(door.InteractionLabel, Is.EqualTo("Locked. Needs the Storage keycard"));
                Assert.That(interaction.TryInteract(), Is.False);
                Assert.That(door.IsOpen, Is.False, name + " must not open without the keycard.");
            }

            var keycard = Object.FindAnyObjectByType<KeyPickup>();
            Assert.That(keycard.KeyId, Is.EqualTo(KeyId));
            var stoneAge = new Bounds(new Vector3(-7f, 1f, 26f), new Vector3(7f, 3f, 8f));
            Assert.That(stoneAge.Contains(keycard.transform.position), Is.True, "The keycard is in the Stone Age gallery.");
            var card = keycard.GetComponent<Collider>().bounds.center;
            // The plinth stands in a nook, open to the south.
            Move(player, card + Vector3.back * 1.1f);
            Aim(player, card);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(keycard));
            Assert.That(keycard.InteractionLabel, Is.EqualTo("Take the Storage keycard"));
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(player.GetComponent<PlayerKeys>().Has(KeyId), Is.True);
            Assert.That(keycard.gameObject.activeSelf, Is.False);

            foreach (var name in StorageDoors)
            {
                var door = DoorNamed(name);
                yield return Face(player, interaction, door);
                Assert.That(door.InteractionLabel, Is.EqualTo("Unlock with the Storage keycard"));
                Assert.That(interaction.TryInteract(), Is.True);
                Assert.That(door.IsOpen, Is.True, name + " opens with the keycard.");
                Assert.That(door.GetComponent<DoorLock>().IsLocked, Is.False);
            }
        }

        private static Door DoorNamed(string name)
        {
            var root = GameObject.Find("Museum/Doors/" + name);
            Assert.That(root, Is.Not.Null, name);
            return root.GetComponentInChildren<Door>(true);
        }

        // Stand in front of the closed leaf on whichever side gives a clear look at it.
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

        // Property blocks store colours in linear space, so a round trip is only nearly exact.
        private static float ColourDistance(Color a, Color b) => ((Vector4)a - (Vector4)b).magnitude;

        private static Color LightColour(GameObject light)
        {
            var block = new MaterialPropertyBlock();
            light.GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        private static void ExpectMissingExteriorPrefab()
        {
            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979"))) return;
            LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
            LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
        }
    }
}
#endif

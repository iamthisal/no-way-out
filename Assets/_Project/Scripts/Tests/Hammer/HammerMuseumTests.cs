#if UNITY_EDITOR
using System.Collections;
using System.IO;
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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class HammerMuseumTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private bool previousBackground;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("MuseumHammerKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("MuseumHammerMouse");
        }

        [UnityTest]
        public IEnumerator StorageHammerBreaksDisplayUnlocksLootAndRestartRestoresEverything()
        {
            ExpectMissingExteriorPrefab();
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            DisableEnemies();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var equipment = player.GetComponent<PlayerEquipment>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var inventory = player.GetComponent<LootInventory>();
            var hammer = player.GetComponentInChildren<FirstPersonHammer>(true);
            var pickup = Object.FindAnyObjectByType<HammerPickup>();
            var glass = Object.FindAnyObjectByType<BreakableGlass>();
            GameObject loot = null;
            foreach (var candidate in Object.FindObjectsByType<LootItem>(FindObjectsInactive.Exclude))
                if (candidate.ItemName == "Armored Helmet") loot = candidate.gameObject;
            Assert.That(pickup, Is.Not.Null);
            Assert.That(glass, Is.Not.Null);
            Assert.That(glass.IsBroken, Is.False, "Glass must start intact after Health initializes.");
            Assert.That(glass.GetComponent<Health>().CurrentHealth, Is.EqualTo(50f));
            Assert.That(glass.GetComponent<Collider>().enabled, Is.True);
            Assert.That(loot, Is.Not.Null);
            Assert.That(equipment.HasHammer, Is.False);
            Assert.That(equipment.TryEquipHammer(), Is.False);

            // Prove the closed case prevents collection before visiting storage.
            var lootCollider = loot.GetComponent<BoxCollider>();
            var approach = glass.GetComponent<Collider>().bounds.center + Vector3.back * 1.7f;
            MoveAndAim(player, approach, lootCollider.bounds.center);
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.Null, "Intact display glass must block the loot target.");
            Assert.That(loot.GetComponent<LootItem>().TryInteract(player.gameObject), Is.False);

            // Approach the existing hammer from an accessible side of the storage table.
            var pickupCenter = pickup.GetComponent<Collider>().bounds.center;
            bool visible = false;
            foreach (var offset in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
            {
                MoveAndAim(player, pickupCenter + offset * 1.2f, pickupCenter);
                yield return null;
                Physics.SyncTransforms();
                if (interaction.CurrentTarget == pickup) { visible = true; break; }
            }
            Assert.That(visible, Is.True, "The storage hammer must be reachable in the actual room.");
            Capture(player.PlayerCamera, "storage-hammer");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(pickup.IsCollected, Is.True);
            Assert.That(equipment.HasHammer, Is.True);
            Assert.That(equipment.IsHammerEquipped, Is.True);
            Assert.That(inventory.ItemCount, Is.Zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;

            MoveAndAim(player, approach, lootCollider.bounds.center);
            yield return null;
            Physics.SyncTransforms();
            Capture(player.PlayerCamera, "display-intact");
            int ammo = player.GetComponentInChildren<FirstPersonWeapon>(true).CurrentAmmo;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            yield return new WaitForSeconds(0.35f);
            Assert.That(hammer.SwingsPerformed, Is.EqualTo(1));
            Assert.That(glass.IsBroken, Is.True);
            Assert.That(glass.GetComponent<Collider>().enabled, Is.False);
            Assert.That(glass.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(loot.activeInHierarchy, Is.True);
            Assert.That(player.GetComponentInChildren<FirstPersonWeapon>(true).CurrentAmmo, Is.EqualTo(ammo));
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(0.4f);
            Physics.SyncTransforms();
            Capture(player.PlayerCamera, "display-broken");
            Assert.That(interaction.CurrentTarget, Is.SameAs(loot.GetComponent<LootItem>()));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(inventory.ItemCount, Is.EqualTo(1));
            Assert.That(inventory.TotalValue, Is.EqualTo(500));
            Assert.That(loot.activeSelf, Is.False);
            Assert.That(loot.GetComponent<LootItem>().TryInteract(player.gameObject), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;

            player.GetComponent<Health>().TakeDamage(100f);
            ExpectMissingExteriorPrefab();
            player.GetComponent<PlayerVitals>().RestartEncounter();
            yield return null;
            yield return null;
            DisableEnemies();
            player = Object.FindAnyObjectByType<FirstPersonController>();
            Assert.That(player.GetComponent<PlayerEquipment>().HasHammer, Is.False);
            Assert.That(player.GetComponent<LootInventory>().ItemCount, Is.Zero);
            Assert.That(Object.FindAnyObjectByType<HammerPickup>().IsCollected, Is.False);
            Assert.That(Object.FindAnyObjectByType<BreakableGlass>().IsBroken, Is.False);
            Assert.That(Object.FindAnyObjectByType<BreakableGlass>().GetComponent<Collider>().enabled, Is.True);
        }

        private static void ExpectMissingExteriorPrefab()
        {
            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979"))) return;
            LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
            LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
        }

        private static void DisableEnemies()
        {
            foreach (var enemy in Object.FindObjectsByType<EnemyShooting>(FindObjectsInactive.Exclude)) enemy.enabled = false;
        }

        private static void MoveAndAim(FirstPersonController player, Vector3 position, Vector3 target)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            position.y = 0.05f;
            player.transform.position = position;
            body.enabled = true;
            var direction = target - player.PlayerCamera.transform.position;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f);
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            typeof(FirstPersonController).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            player.CaptureCursor();
        }

        private static void Capture(Camera camera, string name)
        {
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var target = new RenderTexture(960, 640, 24);
            var pixels = new Texture2D(960, 640, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 960f, 640f), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Temp/HammerVerification");
                File.WriteAllBytes("Temp/HammerVerification/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }

        [TearDown]
        public void Cleanup()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Application.runInBackground = previousBackground;
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif

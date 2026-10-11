#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class LootPickupTests
    {
        private Keyboard keyboard;
        private GameObject wall;
        private bool previousBackground;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("LootTestKeyboard");
        }

        [UnityTest]
        public IEnumerator ReceptionBlocksCollectWithEAndRestartRestoresLoot()
        {
            LoadMuseum();
            yield return null;
            Object.FindAnyObjectByType<EnemyShooting>().enabled = false;
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var inventory = player.GetComponent<LootInventory>();
            var first = GameObject.Find("P_Lobby_Monitor").GetComponent<LootItem>();
            var second = GameObject.Find("P_Lobby_BrochureTray").GetComponent<LootItem>();
            Assert.That(Object.FindObjectsByType<LootItem>(FindObjectsSortMode.None).Length, Is.EqualTo(2));
            Assert.That(inventory.ItemCount, Is.Zero);
            Assert.That(inventory.TotalValue, Is.Zero);
            Assert.That(player.GetComponent<PlayerEquipment>().IsPistolEquipped, Is.False);
            Aim(player, first, 1.8f);
            player.CaptureCursor();
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(first));
            System.IO.Directory.CreateDirectory("Temp/PlayerVerification");
            ScreenCapture.CaptureScreenshot("Temp/PlayerVerification/loot-prompt.png");
            yield return null;
            yield return null;
            player.CaptureCursor();
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(first.IsCollected, Is.True, "E should collect the focused block.");
            Assert.That(first.gameObject.activeSelf, Is.False);
            Assert.That(inventory.ItemCount, Is.EqualTo(1));
            Assert.That(inventory.TotalValue, Is.EqualTo(250));
            Assert.That(first.TryInteract(player.gameObject), Is.False, "Collected loot must not count twice.");
            Assert.That(inventory.Items[0].Name, Is.EqualTo("Prototype Relic"));
            Aim(player, second, 1.8f);
            yield return null;
            Assert.That(second.IsCollected, Is.False, "Holding E must not automatically collect the next target.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            player.CaptureCursor();
            yield return null;
            Assert.That(player.GetComponent<PlayerEquipment>().TryEquipPistol(), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(second.IsCollected, Is.True, "Pickup should also work while carrying the pistol.");
            Assert.That(inventory.ItemCount, Is.EqualTo(2));
            Assert.That(inventory.TotalValue, Is.EqualTo(350));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab));
            yield return null;
            yield return null;
            ScreenCapture.CaptureScreenshot("Temp/PlayerVerification/loot-collected.png");
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            player.GetComponent<Health>().TakeDamage(100f);
            player.GetComponent<PlayerVitals>().RestartEncounter();
            yield return null;
            yield return null;
            Assert.That(Object.FindAnyObjectByType<LootInventory>().ItemCount, Is.Zero);
            Assert.That(Object.FindAnyObjectByType<LootInventory>().TotalValue, Is.Zero);
            Assert.That(GameObject.Find("P_Lobby_Monitor").GetComponent<LootItem>().IsCollected, Is.False);
            Assert.That(GameObject.Find("P_Lobby_BrochureTray").GetComponent<LootItem>().IsCollected, Is.False);
        }

        [UnityTest]
        public IEnumerator PickupRequiresReachSightCursorAndLivingPlayer()
        {
            LoadMuseum();
            yield return null;
            Object.FindAnyObjectByType<EnemyShooting>().enabled = false;
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var inventory = player.GetComponent<LootInventory>();
            var loot = GameObject.Find("P_Lobby_Monitor").GetComponent<LootItem>();
            Aim(player, loot, interaction.Reach + 2f);
            player.CaptureCursor();
            yield return null;
            Assert.That(interaction.TryInteract(), Is.False, "Distant loot is unreachable.");
            Aim(player, loot, 1.8f);
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(loot));
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = Vector3.Lerp(player.PlayerCamera.transform.position, loot.transform.position, 0.5f);
            wall.transform.localScale = new Vector3(1f, 1f, 0.1f);
            Physics.SyncTransforms();
            Assert.That(interaction.TryInteract(), Is.False, "World geometry must block pickup.");
            Assert.That(loot.TryInteract(player.gameObject), Is.False, "Direct item interaction must not bypass a wall.");
            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            player.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);
            Assert.That(interaction.TryInteract(), Is.False, "The player must look at the item.");
            Aim(player, loot, 1.8f);
            player.ReleaseCursor();
            Assert.That(interaction.TryInteract(), Is.False);
            player.CaptureCursor();
            yield return null;
            interaction.enabled = false;
            Assert.That(interaction.TryInteract(), Is.False);
            interaction.enabled = true;
            player.GetComponent<Health>().TakeDamage(100f);
            Assert.That(interaction.TryInteract(), Is.False, "Dead players cannot collect loot.");
            Assert.That(loot.TryInteract(player.gameObject), Is.False);
            Assert.That(loot.gameObject.activeSelf, Is.True);
            Assert.That(inventory.ItemCount, Is.Zero);
        }

        private static void LoadMuseum()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
        }

        private static void Aim(FirstPersonController player, LootItem loot, float distance)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = new Vector3(loot.transform.position.x, 0.05f, loot.transform.position.z - distance);
            player.transform.rotation = Quaternion.identity;
            body.enabled = true;
            var direction = loot.GetComponent<Collider>().bounds.center - player.PlayerCamera.transform.position;
            float pitch = -Mathf.Atan2(direction.y, direction.z) * Mathf.Rad2Deg;
            // Keep the controller's next look update aimed at the block during simulated key input.
            typeof(FirstPersonController).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void Cleanup()
        {
            if (wall != null) Object.DestroyImmediate(wall);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            Application.runInBackground = previousBackground;
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif

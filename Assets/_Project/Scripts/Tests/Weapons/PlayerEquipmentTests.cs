#if UNITY_EDITOR
using System.Collections;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.Weapons;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class PlayerEquipmentTests
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
            Application.runInBackground = true;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [UnityTest]
        public IEnumerator HandsAndPistolSwitchWithoutResettingAmmoOrBypassingDeath()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<EnemyShooting>().enabled = false;
            keyboard = InputSystem.AddDevice<Keyboard>("EquipmentTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("EquipmentTestMouse");
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var equipment = player.GetComponent<PlayerEquipment>();
            var pistol = player.GetComponentInChildren<FirstPersonWeapon>(true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(equipment, Is.Not.Null);
            Assert.That(equipment.IsPistolEquipped, Is.False);
            Assert.That(equipment.EmptyHands.activeInHierarchy, Is.True);
            Assert.That(pistol.gameObject.activeInHierarchy, Is.False);
            Assert.That(pistol.CurrentAmmo, Is.EqualTo(12));
            Assert.That(pistol.TryFire(), Is.False, "Hands mode must not fire a hidden pistol.");
            Assert.That(equipment.EmptyHands.GetComponentsInChildren<Collider>().Length, Is.Zero);
            Assert.That(pistol.transform.Find("PistolGripHand"), Is.Not.Null);
            player.CaptureCursor();
            yield return null;
            Assert.That(player.CanUseWeapon, Is.True, "Switching requires a captured cursor and enabled controls.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return null;
            yield return null;
            Assert.That(keyboard.digit2Key.isPressed, Is.True, "The test keyboard should deliver digit 2.");
            Assert.That(player.CanUseWeapon, Is.True, "The cursor must remain captured while testing switching.");
            Assert.That(equipment.IsPistolEquipped, Is.True, "2 should select the pistol.");
            Assert.That(equipment.EmptyHands.activeInHierarchy, Is.False);
            Assert.That(pistol.TryFire(), Is.True);
            Assert.That(pistol.CurrentAmmo, Is.EqualTo(11));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return null;
            yield return null;
            Assert.That(equipment.IsPistolEquipped, Is.False, "1 should select empty hands.");
            Assert.That(pistol.TryFire(), Is.False);
            Assert.That(pistol.TryReload(), Is.False);

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return null;
            yield return null;
            Assert.That(pistol.CurrentAmmo, Is.EqualTo(11), "Equipping must not reset ammo.");
            Assert.That(pistol.TryReload(), Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return null;
            yield return null;
            Assert.That(pistol.IsReloading, Is.False, "Holstering should cancel unfinished reloads.");
            yield return new WaitForSeconds(1.3f);
            Assert.That(pistol.CurrentAmmo, Is.EqualTo(11));

            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit2));
            yield return null;
            yield return null;
            Assert.That(equipment.IsPistolEquipped, Is.True);
            Assert.That(pistol.ShotsFired, Is.EqualTo(1), "Equipping while holding Attack must not shoot.");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;

            player.ReleaseCursor();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
            yield return null;
            yield return null;
            Assert.That(equipment.IsPistolEquipped, Is.True, "Released cursor must block switching.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            player.CaptureCursor();
            yield return null;
            Assert.That(equipment.TryEquipHands(), Is.True);
            System.IO.Directory.CreateDirectory("Temp/PlayerVerification");
            ScreenCapture.CaptureScreenshot("Temp/PlayerVerification/equipment-empty-hands.png");
            yield return null;
            yield return null;
            player.CaptureCursor();
            yield return null;
            Assert.That(equipment.TryEquipPistol(), Is.True);
            ScreenCapture.CaptureScreenshot("Temp/PlayerVerification/equipment-pistol.png");
            yield return null;
            yield return null;
            player.CaptureCursor();
            yield return null;
            Assert.That(equipment.TryEquipHands(), Is.True);
            player.GetComponent<Health>().TakeDamage(100f);
            Assert.That(equipment.enabled, Is.False);
            Assert.That(pistol.enabled, Is.False, "Death must also disable a holstered pistol.");
            Assert.That(equipment.TryEquipPistol(), Is.False);
            Assert.That(pistol.TryFire(), Is.False);
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

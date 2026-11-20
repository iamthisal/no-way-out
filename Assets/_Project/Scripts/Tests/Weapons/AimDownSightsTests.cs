#if UNITY_EDITOR
using System.Collections;
using Lockdown.Player;
using Lockdown.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class AimDownSightsTests
    {
        private const int RightButton = 1 << 1;
        private GameObject playerObject;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool previousBackground;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;

        [UnityTest]
        public IEnumerator RightClickPutsThePistolSightsOnTheCrosshairButNotTheHammer()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            foreach (var otherPlayer in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                otherPlayer.gameObject.SetActive(false);
            keyboard = InputSystem.AddDevice<Keyboard>("AimTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("AimTestMouse");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            playerObject = Object.Instantiate(prefab, new Vector3(120f, 0.05f, 0f), Quaternion.identity);
            var player = playerObject.GetComponent<FirstPersonController>();
            var equipment = playerObject.GetComponent<PlayerEquipment>();
            var weapon = playerObject.GetComponentInChildren<FirstPersonWeapon>(true);
            Assert.That(weapon.FrontSight, Is.Not.Null, "The pistol needs a front sight to aim with.");
            Transform camera = player.PlayerCamera.transform;
            yield return null;
            player.CaptureCursor();
            Assert.That(equipment.TryEquipPistol(), Is.True);
            yield return null;
            Vector3 hipPosition = weapon.transform.localPosition;
            Assert.That(weapon.IsAiming, Is.False);

            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = RightButton });
            yield return new WaitForSeconds(0.3f);
            Assert.That(weapon.IsAiming, Is.True, "Holding the right mouse button should aim the pistol.");
            Assert.That(weapon.AimBlend, Is.EqualTo(1f));
            Vector3 sightTop = camera.InverseTransformPoint(weapon.FrontSight.TransformPoint(0f, 0.5f, 0f));
            Assert.That(sightTop.x, Is.EqualTo(0f).Within(1e-3f), "The front sight must be centred under the crosshair.");
            Assert.That(sightTop.y, Is.EqualTo(0f).Within(1e-3f), "The top of the front sight must sit on the crosshair.");
            Assert.That(sightTop.z, Is.GreaterThan(player.PlayerCamera.nearClipPlane));
            Assert.That(Vector3.Angle(weapon.transform.forward, camera.forward), Is.LessThan(0.5f),
                "The sight line must point where the crosshair points.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            Assert.That(weapon.TryFire(), Is.True, "Aiming must not block firing.");
            yield return null;
            yield return null;
            Assert.That(weapon.IsReloading, Is.True);
            Assert.That(weapon.IsAiming, Is.False, "Reloading lowers the sights.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(1.4f);
            Assert.That(weapon.IsAiming, Is.True, "The sights come back up after reloading while still held.");

            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(0.3f);
            Assert.That(weapon.IsAiming, Is.False);
            Assert.That(Vector3.Distance(weapon.transform.localPosition, hipPosition), Is.LessThan(1e-3f),
                "Releasing the button returns the pistol to the hip.");

            Assert.That(equipment.TryCollectHammer(), Is.True);
            var hammer = playerObject.GetComponentInChildren<FirstPersonHammer>();
            Assert.That(hammer, Is.Not.Null);
            yield return null;
            Vector3 hammerRest = hammer.transform.localPosition;
            Quaternion hammerRotation = hammer.transform.localRotation;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = RightButton });
            yield return new WaitForSeconds(0.3f);
            Assert.That(weapon.IsAiming, Is.False, "The holstered pistol must not aim.");
            Assert.That(Vector3.Distance(hammer.transform.localPosition, hammerRest), Is.LessThan(1e-4f),
                "Right click must not move the melee hammer.");
            Assert.That(Quaternion.Angle(hammer.transform.localRotation, hammerRotation), Is.LessThan(0.01f));
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Application.runInBackground = previousBackground;
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }
    }
}
#endif

#if UNITY_EDITOR
using System.Collections;
using Lockdown.Interfaces;
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
    public sealed class WeaponDamageProbe : MonoBehaviour, IDamageable
    {
        public float DamageReceived { get; private set; }
        public void TakeDamage(float amount) { DamageReceived += amount; }
    }

    public sealed class FirstPersonWeaponTests
    {
        private GameObject playerObject;
        private GameObject wall;
        private GameObject receiver;
        private GameObject floor;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool previousBackground;

        [UnityTest]
        public IEnumerator PistolFiresReloadsAndWallsBlockDamage()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            foreach (var otherPlayer in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                otherPlayer.gameObject.SetActive(false);
            keyboard = InputSystem.AddDevice<Keyboard>("WeaponTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("WeaponTestMouse");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            playerObject = Object.Instantiate(prefab, new Vector3(80f, 0.05f, 0f), Quaternion.identity);
            var player = playerObject.GetComponent<FirstPersonController>();
            var weapon = playerObject.GetComponentInChildren<FirstPersonWeapon>();
            Assert.That(weapon, Is.Not.Null, "The player prefab should contain the equipped pistol.");
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(80f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.5f, 20f);
            receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiver.transform.position = new Vector3(80f, 1.5f, 8f);
            receiver.transform.localScale = new Vector3(2f, 3f, 0.5f);
            var damage = receiver.AddComponent<WeaponDamageProbe>();
            Physics.SyncTransforms();
            player.CaptureCursor();
            yield return new WaitForSeconds(0.3f);
            Assert.That(weapon.CurrentAmmo, Is.EqualTo(12));

            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            yield return null;
            Assert.That(weapon.ShotsFired, Is.EqualTo(1), "Left click should fire through the existing Attack binding.");
            Assert.That(damage.DamageReceived, Is.EqualTo(25f));
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return new WaitForSeconds(0.3f);

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(80f, 1.5f, 4f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.5f);
            Physics.SyncTransforms();
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(damage.DamageReceived, Is.EqualTo(25f), "Shots must stop at the nearest wall.");
            Assert.That(weapon.TryFire(), Is.False, "The pistol must enforce its fire interval.");
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.27f);
                Assert.That(weapon.TryFire(), Is.True);
            }
            Assert.That(weapon.CurrentAmmo, Is.Zero);
            yield return new WaitForSeconds(0.27f);
            Assert.That(weapon.TryFire(), Is.False, "An empty magazine must not fire.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            yield return null;
            Assert.That(weapon.IsReloading, Is.True, "R should begin reloading.");
            Assert.That(weapon.TryFire(), Is.False, "Reloading must block firing.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(1.3f);
            Assert.That(weapon.CurrentAmmo, Is.EqualTo(12));
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.TryReload(), Is.False, "A full magazine should not reload.");

            player.ReleaseCursor();
            yield return null;
            Assert.That(weapon.TryFire(), Is.False, "Released cursor should block firing.");
            int shots = weapon.ShotsFired;
            // Capture is performed by the controller before the weapon's Update.
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return null;
            yield return null;
            Assert.That(weapon.ShotsFired, Is.EqualTo(shots), "Recapturing the mouse must not fire a shot.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (floor != null) Object.DestroyImmediate(floor);
            if (wall != null) Object.DestroyImmediate(wall);
            if (receiver != null) Object.DestroyImmediate(receiver);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Application.runInBackground = previousBackground;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }
    }
}
#endif

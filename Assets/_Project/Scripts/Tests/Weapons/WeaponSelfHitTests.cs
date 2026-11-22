#if UNITY_EDITOR
using System.Collections;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class WeaponSelfHitTests
    {
        private GameObject playerObject;
        private GameObject floor;
        private GameObject receiver;

        // Regression: walking backwards and firing used to hit the player's own capsule,
        // which the physics scene still held at its pre-move position.
        [UnityTest]
        public IEnumerator ShootingWhileSteppingBackwardsNeverHitsThePlayer()
        {
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            foreach (var otherPlayer in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                otherPlayer.gameObject.SetActive(false);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(160f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.5f, 30f);
            receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
            receiver.transform.position = new Vector3(160f, 1.5f, 8f);
            receiver.transform.localScale = new Vector3(3f, 3f, 0.5f);
            var damage = receiver.AddComponent<WeaponDamageProbe>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            playerObject = Object.Instantiate(prefab, new Vector3(160f, 0.05f, 0f), Quaternion.identity);
            var player = playerObject.GetComponent<FirstPersonController>();
            var health = playerObject.GetComponent<Health>();
            var body = playerObject.GetComponent<CharacterController>();
            var weapon = playerObject.GetComponentInChildren<FirstPersonWeapon>(true);
            Physics.SyncTransforms();
            yield return null;
            player.CaptureCursor();
            yield return null;
            Assert.That(playerObject.GetComponent<PlayerEquipment>().TryEquipPistol(), Is.True);
            yield return null;
            float startHealth = health.CurrentHealth;

            float[] pitches = { 20f, 40f }; // The level shot (pitch 0) is checked below.
            foreach (float pitch in pitches)
            {
                yield return new WaitForSeconds(0.3f);
                player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
                // Step back and fire in the same frame, before physics syncs the capsule.
                player.CaptureCursor();
                body.Move(-playerObject.transform.forward * 0.3f);
                Assert.That(weapon.TryFire(), Is.True);
                Assert.That(health.CurrentHealth, Is.EqualTo(startHealth),
                    "A shot fired while stepping backwards at pitch " + pitch + " hit the player.");
            }
            player.PlayerCamera.transform.localRotation = Quaternion.identity;
            yield return new WaitForSeconds(0.3f);
            player.CaptureCursor();
            body.Move(-playerObject.transform.forward * 0.3f);
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(damage.DamageReceived, Is.EqualTo(25f), "A level shot should pass the player and hit the target ahead.");
            Assert.That(health.CurrentHealth, Is.EqualTo(startHealth));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (floor != null) Object.DestroyImmediate(floor);
            if (receiver != null) Object.DestroyImmediate(receiver);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }
    }
}
#endif

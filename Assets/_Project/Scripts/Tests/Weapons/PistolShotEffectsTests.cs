#if UNITY_EDITOR
using System.Collections;
using Lockdown.Player;
using Lockdown.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class PistolShotEffectsTests
    {
        private GameObject playerObject;
        private GameObject floor;
        private GameObject wall;

        [UnityTest]
        public IEnumerator ShotShowsMuzzleFlashAndATemporaryDarkHoleWithoutATracer()
        {
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            foreach (var otherPlayer in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                otherPlayer.gameObject.SetActive(false);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(200f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(10f, 0.5f, 20f);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(200f, 1.5f, 5f);
            wall.transform.localScale = new Vector3(4f, 3f, 0.4f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            playerObject = Object.Instantiate(prefab, new Vector3(200f, 0.05f, 0f), Quaternion.identity);
            var player = playerObject.GetComponent<FirstPersonController>();
            var weapon = playerObject.GetComponentInChildren<FirstPersonWeapon>(true);
            Physics.SyncTransforms();
            yield return null;
            player.CaptureCursor();
            yield return null;
            Assert.That(playerObject.GetComponent<PlayerEquipment>().TryEquipPistol(), Is.True);
            yield return new WaitForSeconds(0.3f);

            player.CaptureCursor();
            Assert.That(weapon.TryFire(), Is.True);
            var flash = weapon.transform.Find("Muzzle/MuzzleFlash");
            Assert.That(flash != null && flash.gameObject.activeSelf, Is.True, "Firing should show the muzzle flash.");
            foreach (var line in weapon.GetComponentsInChildren<LineRenderer>(true))
                Assert.That(line.enabled, Is.False, "The pistol must not draw a tracer line.");

            var holes = Object.FindObjectsByType<BulletHole>(FindObjectsSortMode.None);
            Assert.That(holes.Length, Is.EqualTo(1), "One shot should leave one bullet hole.");
            var hole = holes[0];
            float wallFront = wall.transform.position.z - wall.transform.localScale.z * 0.5f;
            Assert.That(hole.transform.position.z, Is.EqualTo(wallFront).Within(0.01f), "The hole should sit on the wall's surface.");
            Assert.That(Vector3.Angle(hole.transform.forward, Vector3.back), Is.LessThan(1f), "The hole should face out of the wall.");
            Assert.That(hole.Surface, Is.EqualTo(wall.transform));
            Color color = hole.GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(color.grayscale, Is.LessThan(0.1f), "The bullet hole should be dark.");

            yield return new WaitForSeconds(0.15f);
            Assert.That(flash.gameObject.activeSelf, Is.False, "The muzzle flash should be brief.");

            wall.transform.position += Vector3.right;
            Vector3 before = hole.transform.position;
            yield return null;
            Assert.That(hole != null, Is.True);
            Assert.That(Vector3.Distance(hole.transform.position, before + Vector3.right), Is.LessThan(1e-3f),
                "The hole should move with the surface it is on.");

            yield return new WaitForSeconds(hole.Lifetime);
            Assert.That(hole == null, Is.True, "The bullet hole should disappear after its lifetime.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (playerObject != null) Object.DestroyImmediate(playerObject);
            if (floor != null) Object.DestroyImmediate(floor);
            if (wall != null) Object.DestroyImmediate(wall);
            foreach (var hole in Object.FindObjectsByType<BulletHole>(FindObjectsSortMode.None))
                Object.DestroyImmediate(hole.gameObject);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            yield return null;
        }
    }
}
#endif

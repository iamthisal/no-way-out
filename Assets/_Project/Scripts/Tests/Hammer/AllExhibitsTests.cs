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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class AllExhibitsTests
    {
        private bool previousBackground;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private static readonly Vector3[] Sides = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [UnityTest]
        public IEnumerator EveryGlassExhibitCanBeBrokenAndLootedIndependentlyAndRestored()
        {
            LoadMuseum();
            yield return null;
            DisableEnemies();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            player.CaptureCursor();
            yield return null;
            var interaction = player.GetComponent<PlayerInteraction>();
            var inventory = player.GetComponent<LootInventory>();
            Assert.That(player.GetComponent<PlayerEquipment>().TryCollectHammer(), Is.True);
            var hammer = player.GetComponentInChildren<FirstPersonHammer>(true);
            var glasses = Object.FindObjectsByType<BreakableGlass>(FindObjectsInactive.Exclude);
            Assert.That(glasses.Length, Is.EqualTo(7));
            int meshPanes = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (renderer.name != "M_Museum_DisplayGlass") continue;
                meshPanes++;
                Assert.That(renderer.GetComponents<BreakableGlass>().Length, Is.EqualTo(1), "Every exhibit glass must be configured exactly once.");
                Assert.That(renderer.GetComponents<BoxCollider>().Length, Is.EqualTo(1));
            }
            Assert.That(meshPanes, Is.EqualTo(7));
            Assert.That(Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude).Length, Is.EqualTo(7));
            Assert.That(GameObject.Find("Courtyard_LockedGlassDoor").GetComponent<BreakableGlass>(), Is.Null);
            int expectedItems = 0;
            long expectedValue = 0;

            foreach (var glass in glasses)
            {
                var display = glass.transform.parent.parent;
                var items = display.GetComponentsInChildren<LootItem>(true);
                Assert.That(items.Length, Is.GreaterThan(0), display.name + " needs actual loot.");
                Assert.That(glass.IsBroken, Is.False, "Another case breaking must not unlock this case.");
                foreach (var item in items)
                {
                    Assert.That(item.DisplayGlass, Is.SameAs(glass));
                    Assert.That(item.IsProtected, Is.True);
                    Assert.That(item.TryInteract(player.gameObject), Is.False);
                }

                var barrier = glass.GetComponent<BoxCollider>();
                var closedBounds = barrier.bounds;
                bool approached = false;
                foreach (var side in Sides)
                {
                    MoveAndAim(player, Outside(closedBounds, side), closedBounds.center);
                    yield return null;
                    Physics.SyncTransforms();
                    if (FirstWorldHit(player) == barrier) { approached = true; break; }
                }
                Assert.That(approached, Is.True, "Glass is not accessible: " + display.name);
                foreach (var item in items) Assert.That(interaction.CurrentTarget, Is.Not.SameAs(item));
                yield return new WaitForSeconds(0.7f);
                Assert.That(hammer.TrySwing(), Is.True);
                yield return new WaitForSeconds(0.3f);
                Assert.That(glass.IsBroken, Is.True, "Hammer could not break " + display.name);
                Assert.That(barrier.enabled, Is.False);
                Assert.That(glass.GetComponent<Renderer>().enabled, Is.False);
                foreach (var item in items) Assert.That(item.gameObject.activeInHierarchy, Is.True);

                foreach (var item in items)
                {
                    bool reachable = false;
                    foreach (var side in Sides)
                    {
                        MoveAndAim(player, Outside(closedBounds, side), item.GetComponent<Collider>().bounds.center);
                        yield return null;
                        Physics.SyncTransforms();
                        if (interaction.CurrentTarget == item) { reachable = true; break; }
                    }
                    Assert.That(reachable, Is.True, "Loot is unreachable after breaking: " + item.ItemName);
                    expectedItems++;
                    expectedValue += item.Value;
                    Assert.That(item.IsProtected, Is.False);
                    Assert.That(interaction.TryInteract(), Is.True);
                    Assert.That(item.IsCollected, Is.True);
                    Assert.That(item.gameObject.activeSelf, Is.False);
                    Assert.That(item.TryInteract(player.gameObject), Is.False);
                    Assert.That(inventory.ItemCount, Is.EqualTo(expectedItems));
                    Assert.That(inventory.TotalValue, Is.EqualTo(expectedValue));
                }
                // Cabinet supports and signage are scenery, not collectible items.
                foreach (var renderer in glass.transform.parent.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.transform != glass.transform && renderer.GetComponentInParent<LootItem>() == null)
                        Assert.That(renderer.enabled, Is.True, "Breaking/looting must preserve the frame: " + display.name);
            }
            Assert.That(expectedItems, Is.EqualTo(10));
            Assert.That(expectedValue, Is.EqualTo(3650));
            Directory.CreateDirectory("Temp/ExhibitVerification");
            File.WriteAllText("Temp/ExhibitVerification/sweep.txt", "Seven cases broken; ten items collected; $3650; no duplicate collection.");

            player.GetComponent<Health>().TakeDamage(100f);
            ExpectMissingExteriorPrefab();
            player.GetComponent<PlayerVitals>().RestartEncounter();
            yield return null;
            yield return null;
            DisableEnemies();
            Assert.That(Object.FindAnyObjectByType<LootInventory>().ItemCount, Is.Zero);
            Assert.That(Object.FindAnyObjectByType<PlayerEquipment>().HasHammer, Is.False);
            var restored = Object.FindObjectsByType<BreakableGlass>(FindObjectsInactive.Exclude);
            Assert.That(restored.Length, Is.EqualTo(7));
            int restoredItems = 0;
            foreach (var glass in restored)
            {
                Assert.That(glass.IsBroken, Is.False);
                Assert.That(glass.GetComponent<Collider>().enabled, Is.True);
                foreach (var item in glass.transform.parent.parent.GetComponentsInChildren<LootItem>())
                {
                    restoredItems++;
                    Assert.That(item.IsCollected, Is.False);
                    Assert.That(item.IsProtected, Is.True);
                }
            }
            Assert.That(restoredItems, Is.EqualTo(10));
        }

        private static Vector3 Outside(Bounds bounds, Vector3 side)
        {
            float extent = Mathf.Abs(side.x) * bounds.extents.x + Mathf.Abs(side.z) * bounds.extents.z;
            return bounds.center + side * (extent + 0.8f);
        }

        private static Collider FirstWorldHit(FirstPersonController player)
        {
            var camera = player.PlayerCamera;
            var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, 1.9f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits) if (!hit.transform.IsChildOf(player.transform)) return hit.collider;
            return null;
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

        private static void LoadMuseum()
        {
            ExpectMissingExteriorPrefab();
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
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
        [TearDown]
        public void Cleanup()
        {
            Application.runInBackground = previousBackground;
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif

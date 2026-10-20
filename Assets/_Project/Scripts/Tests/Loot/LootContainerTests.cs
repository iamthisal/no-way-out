#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class LootContainerTests
    {
        private GameObject fixture;
        private FirstPersonController player;
        private PlayerInteraction interaction;
        private LootInventory inventory;
        private InputActionAsset actions;
        private bool previousBackground;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            fixture = new GameObject("Container tests");
            fixture.transform.position = new Vector3(300f, 0f, 300f);
            var body = new GameObject("Player");
            body.SetActive(false);
            body.transform.SetParent(fixture.transform, false);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(body.transform, false);
            camera.transform.localPosition = Vector3.up * 1.6f;
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = actions.AddActionMap("Player");
            map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            map.AddAction("Jump", InputActionType.Button);
            map.AddAction("Sprint", InputActionType.Button);
            player = body.AddComponent<FirstPersonController>();
            Set(player, "playerCamera", camera);
            Set(player, "inputActions", actions);
            Set(player, "gravity", 0f);
            body.AddComponent<Health>();
            body.AddComponent<LootInventory>();
            interaction = body.AddComponent<PlayerInteraction>();
            inventory = body.GetComponent<LootInventory>();
            body.SetActive(true);
            yield return null;
            player.CaptureCursor();
            yield return null;
        }

        [UnityTest]
        public IEnumerator OpenThenLootIndividuallyWithoutDuplicatesAndReopenEmpty()
        {
            var crate = MakeCrate();
            AimAtCrate(crate);
            yield return null;
            var items = crate.GetComponentsInChildren<LootItem>();
            Assert.That(items.Length, Is.EqualTo(2));
            Assert.That(items[0].IsProtected, Is.True);
            Assert.That(items[0].TryInteract(player.gameObject), Is.False);
            Assert.That(interaction.CurrentTarget, Is.SameAs(crate));
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(crate.IsOpen, Is.True);
            Assert.That(crate.IsAccessible, Is.False);
            Assert.That(interaction.TryInteract(), Is.False, "Opening cannot immediately award loot.");
            Assert.That(inventory.ItemCount, Is.Zero);
            yield return new WaitForSeconds(0.85f);
            Assert.That(crate.IsAccessible, Is.True);
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(inventory.ItemCount, Is.EqualTo(1));
            Assert.That(items[0].gameObject.activeSelf, Is.False);
            Assert.That(items[0].TryInteract(player.gameObject), Is.False);
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(inventory.ItemCount, Is.EqualTo(2));
            Assert.That(inventory.TotalValue, Is.EqualTo(350));
            Assert.That(crate.IsEmpty, Is.True);
            Assert.That(interaction.TryInteract(), Is.True, "Empty crates can close.");
            yield return new WaitForSeconds(0.85f);
            Assert.That(crate.IsOpen, Is.False);
            Assert.That(interaction.TryInteract(), Is.True);
            yield return new WaitForSeconds(0.85f);
            Assert.That(crate.IsEmpty, Is.True);
            Assert.That(inventory.ItemCount, Is.EqualTo(2), "Reopening must not respawn collected contents.");
        }

        [UnityTest]
        public IEnumerator ExistingChestCoinIsCollectedWithItsOriginalVisual()
        {
            var root = new GameObject("Coin chest");
            root.SetActive(false);
            root.transform.SetParent(fixture.transform, false);
            root.transform.localPosition = new Vector3(0f, 0.5f, 2f);
            root.AddComponent<BoxCollider>();
            var hinge = new GameObject("LidPivot").transform;
            hinge.SetParent(root.transform, false);
            var coin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coin.name = "Gold_Coin";
            coin.transform.SetParent(root.transform, false);
            coin.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            coin.transform.localScale = new Vector3(0.15f, 0.03f, 0.15f);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0f, -0.3f, -0.475f);
            wall.transform.localScale = new Vector3(1f, 0.4f, 0.05f);
            var crate = root.AddComponent<LootContainer>();
            Set(crate, "modelKind", LootContainer.ModelKind.Chest);
            root.SetActive(true);
            AimAtCrate(crate);
            yield return null;
            Assert.That(root.GetComponentsInChildren<LootItem>().Length, Is.EqualTo(1));
            Assert.That(coin.GetComponent<LootItem>().IsProtected, Is.True);
            Assert.That(interaction.TryInteract(), Is.True);
            yield return new WaitForSeconds(0.85f);
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(coin.activeSelf, Is.False);
            Assert.That(inventory.ItemCount, Is.EqualTo(1));
            Assert.That(inventory.TotalValue, Is.EqualTo(500));
            Assert.That(crate.IsEmpty, Is.True);
        }

        [UnityTest]
        public IEnumerator ContainerRequiresReachSightAndLivingPlayerAndDoesNotUnlockNeighbours()
        {
            var crate = MakeCrate();
            var neighbour = MakeCrate();
            neighbour.transform.localPosition += Vector3.right * 4f;
            AimAtCrate(crate);
            yield return null;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1f, 0.8f);
            wall.transform.localScale = new Vector3(2f, 3f, 0.1f);
            Physics.SyncTransforms();
            Assert.That(crate.TryInteract(player.gameObject), Is.False);
            Object.DestroyImmediate(wall);
            crate.transform.localPosition += Vector3.forward * 5f;
            AimAtCrate(crate);
            Physics.SyncTransforms();
            Assert.That(crate.TryInteract(player.gameObject), Is.False);
            crate.transform.localPosition -= Vector3.forward * 5f;
            AimAtCrate(crate);
            yield return null;
            Assert.That(interaction.TryInteract(), Is.True);
            yield return new WaitForSeconds(0.85f);
            Assert.That(neighbour.IsOpen, Is.False);
            foreach (var item in neighbour.GetComponentsInChildren<LootItem>())
                Assert.That(item.IsProtected, Is.True);
            player.GetComponent<Health>().TakeDamage(100f);
            Assert.That(crate.TryInteract(player.gameObject), Is.False);
            Assert.That(inventory.ItemCount, Is.Zero);
        }

        private LootContainer MakeCrate()
        {
            var crate = new GameObject("Test crate");
            crate.SetActive(false);
            crate.transform.SetParent(fixture.transform, false);
            crate.transform.localPosition = new Vector3(0f, 0.5f, 2f);
            crate.AddComponent<BoxCollider>();
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
            var material = probe.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(probe);
            var container = crate.AddComponent<LootContainer>();
            Set(container, "modelKind", LootContainer.ModelKind.StorageCrate);
            Set(container, "woodMaterial", material);
            Set(container, "contents", new[] {
                new LootContainer.Contents { itemName = "Clay relic", value = 150, mesh = mesh, material = material },
                new LootContainer.Contents { itemName = "Blue relic", value = 200, mesh = mesh, material = material }
            });
            crate.SetActive(true);
            return container;
        }

        private void AimAtCrate(LootContainer crate)
        {
            var target = crate.transform.position + new Vector3(0f, -0.3f, -0.5f);
            var direction = target - player.PlayerCamera.transform.position;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Set(player, "pitch", pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            player.CaptureCursor();
            Physics.SyncTransforms();
        }

        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(fixture);
            Object.DestroyImmediate(actions);
            Application.runInBackground = previousBackground;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif

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
        public IEnumerator MuseumExitRequiresEveryRelicAndAnAuthorizedIdentityTag()
        {
            LoadMuseum();
            yield return null;
            foreach (var enemy in Object.FindObjectsByType<EnemyShooting>(FindObjectsInactive.Exclude))
                enemy.enabled = false;

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var vitals = player.GetComponent<PlayerVitals>();
            var inventory = player.GetComponent<LootInventory>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var exit = GameObject.Find("Exit_ClosedDoorPlaceholder").GetComponent<MuseumExit>();
            var exitCenter = exit.GetComponent<BoxCollider>().bounds.center;
            var exitApproach = exitCenter + Vector3.back * 1.4f;
            AimAt(player, exitApproach, exitCenter);
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(exit));
            Assert.That(exit.RequiredLootCount, Is.EqualTo(16));
            Assert.That(exit.GetObjective(inventory), Does.Contain("Collect all exhibition loot").And.Contain("0/16"));
            Assert.That(interaction.TryInteract(), Is.False);

            var console = GameObject.Find("P_Security_DeskMonitor").GetComponent<SecurityConsole>();
            var consoleCenter = console.GetComponent<BoxCollider>().bounds.center;
            var consoleApproach = consoleCenter + new Vector3(0.8f, 0f, 1.2f);
            AimAt(player, consoleApproach, consoleCenter);
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(console));
            Assert.That(interaction.TryInteract(), Is.False, "Authorization requires possession of the actual tag.");
            AimAt(player, exitApproach, exitCenter);
            yield return null;

            // Record the actual scene relics to exercise the exit independently of the exhibit-opening tests.
            var collect = typeof(LootItem).GetMethod("Collect", BindingFlags.Instance | BindingFlags.NonPublic);
            var relics = System.Array.FindAll(Object.FindObjectsByType<LootItem>(FindObjectsInactive.Exclude),
                item => item.DisplayGlass != null || item.GetComponentInParent<LootContainer>()?.IsChest == true);
            Assert.That(relics.Length, Is.EqualTo(16));
            for (int index = 0; index < relics.Length - 1; index++)
                collect.Invoke(relics[index], new object[] { inventory });
            Assert.That(exit.GetObjective(inventory), Does.Contain("15/16"));
            Assert.That(interaction.TryInteract(), Is.False);

            collect.Invoke(relics[relics.Length - 1], new object[] { inventory });
            Assert.That(exit.CollectedLootCount(inventory), Is.EqualTo(16));
            Assert.That(exit.GetObjective(inventory), Does.Contain("Find the Identity Tag"));
            Assert.That(interaction.TryInteract(), Is.False);

            var crate = GameObject.Find("P_Temporary_CrateNW").GetComponent<LootContainer>();
            var crateCenter = crate.GetComponent<BoxCollider>().bounds.center;
            var crateApproach = crateCenter + Vector3.back * 1.35f;
            AimAt(player, crateApproach, crateCenter);
            yield return null;
            Assert.That(interaction.TryInteract(), Is.True);
            yield return new WaitForSeconds(0.85f);
            AimAt(player, crateApproach, crateCenter - Vector3.up * 0.25f);
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(exit.CollectedLootCount(inventory), Is.EqualTo(16));
            Assert.That(exit.GetObjective(inventory), Does.Contain("Authorize your Identity Tag in the guard room"));

            AimAt(player, exitApproach, exitCenter);
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(exit));
            Assert.That(interaction.TryInteract(), Is.False, "An ordinary tag cannot bypass the guard room.");

            AimAt(player, consoleApproach, consoleCenter);
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(console));
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(interaction.TryInteract(), Is.False, "A tag only needs to be authorized once.");
            Assert.That(exit.GetObjective(inventory), Does.Contain("You can exit now"));

            AimAt(player, exitApproach, exitCenter);
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(exit));
            float initialTimeScale = Time.timeScale;
            try
            {
                Assert.That(interaction.TryInteract(), Is.True);
                Assert.That(vitals.HasEscaped, Is.True);
                Assert.That(player.enabled, Is.False);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(inventory.ItemCount, Is.EqualTo(17));
                vitals.RestartEncounter();
                yield return null;
                yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(initialTimeScale));
                Assert.That(Object.FindAnyObjectByType<PlayerVitals>().HasEscaped, Is.False);
                Assert.That(Object.FindAnyObjectByType<LootInventory>().ItemCount, Is.Zero);
            }
            finally
            {
                Time.timeScale = initialTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator TemporaryCrateIdentityTagUnlocksAllThreeAssignedDoors()
        {
            LoadMuseum();
            yield return null;
            foreach (var enemy in Object.FindObjectsByType<EnemyShooting>(FindObjectsInactive.Exclude))
                enemy.enabled = false;

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var inventory = player.GetComponent<LootInventory>();
            var entrances = new[]
            {
                (door: "Door_D01_Lobby", reader: "D01 Identity Tag Reader",
                    approach: Vector3.forward, readerOffset: Vector3.forward * 1.2f),
                (door: "Door_D07_Storage", reader: "D07 Identity Tag Reader",
                    approach: Vector3.left, readerOffset: Vector3.left * 1.2f),
                (door: "Door_D10_Security", reader: "D10 Identity Tag Reader",
                    approach: Vector3.left, readerOffset: Vector3.left * 1.2f + Vector3.forward * 0.7f)
            };
            var feedback = player.GetComponent<InteractionFeedback>();
            Assert.That(feedback, Is.Not.Null);
            foreach (string cue in new[] { "doorHinge", "doorLatch", "scanAccepted", "scanDenied", "cardTap" })
                Assert.That(typeof(InteractionFeedback).GetField(cue, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(feedback), Is.InstanceOf<AudioClip>(), cue);
            Assert.That(player.transform.position.z, Is.GreaterThan(-5f),
                "The player must start on the side of D01 that leads to the tag.");
            foreach (var entrance in entrances)
            {
                var door = GameObject.Find(entrance.door).GetComponentInChildren<Door>();
                var reader = GameObject.Find(entrance.reader).GetComponent<DoorReader>();
                var readerCenter = reader.GetComponent<BoxCollider>().bounds.center;
                var panelCenter = door.GetComponent<BoxCollider>().bounds.center;

                AimAt(player, panelCenter + entrance.approach * 1.4f, panelCenter);
                yield return null;
                Assert.That(interaction.CurrentTarget, Is.SameAs(door), entrance.door);
                Assert.That(interaction.TryInteract(), Is.False, entrance.door);
                Assert.That(door.IsOpen, Is.False, entrance.door);
                if (entrance.door == "Door_D01_Lobby")
                {
                    player.GetComponent<CharacterController>().Move(Vector3.back * 2.8f);
                    Assert.That(player.transform.position.z, Is.GreaterThan(-5f), "D01 must block movement when locked.");
                }

                AimAt(player, readerCenter + entrance.readerOffset, readerCenter);
                yield return null;
                Assert.That(interaction.CurrentTarget, Is.SameAs(reader), entrance.reader);
                Assert.That(interaction.TryInteract(), Is.False, entrance.reader);
                Assert.That(door.IsOpen, Is.False, entrance.door);
                Assert.That(player.transform.Find("Identity Tag Scan"), Is.Null,
                    "A denied scan without the tag must not display a card.");
            }

            var crate = GameObject.Find("P_Temporary_CrateNW").GetComponent<LootContainer>();
            var tag = crate.GetComponentInChildren<LootItem>();
            var tagRenderer = tag.GetComponentInChildren<Renderer>();
            var lidRenderer = crate.transform.Find("LP_Temporary_CrateNW/CrateLidHinge/Wooden lid")
                .GetComponent<Renderer>();
            Assert.That(tag.ItemName, Is.EqualTo("Identity Tag"));
            Assert.That(tagRenderer.enabled, Is.True);
            Assert.That(tagRenderer.bounds.max.y, Is.LessThan(lidRenderer.bounds.min.y),
                "The tag should fit beneath the closed lid.");
            Assert.That(tag.IsProtected, Is.True);
            Assert.That(tag.TryInteract(player.gameObject), Is.False);

            var crateCenter = crate.GetComponent<BoxCollider>().bounds.center;
            var approach = crateCenter + Vector3.back * 1.35f;
            AimAt(player, approach, crateCenter);
            yield return null;
            Assert.That(interaction.CurrentTarget, Is.SameAs(crate));
            Assert.That(interaction.TryInteract(), Is.True);
            yield return new WaitForSeconds(0.85f);
            Assert.That(tag.IsProtected, Is.False);
            Assert.That(crate.transform.Find("LP_Temporary_CrateNW/CrateLidHinge/Wooden lid"), Is.Not.Null);

            AimAt(player, approach, tagRenderer.bounds.center);
            Assert.That(interaction.CurrentTarget, Is.SameAs(tag),
                "The card must be directly visible from in front of the open crate.");
            Assert.That(interaction.TryInteract(), Is.True);
            Assert.That(inventory.Items[0].Name, Is.EqualTo("Identity Tag"));
            Assert.That(inventory.TotalValue, Is.Zero);
            Assert.That(tag.IsCollected, Is.True);

            foreach (var entrance in entrances)
            {
                var door = GameObject.Find(entrance.door).GetComponentInChildren<Door>();
                var reader = GameObject.Find(entrance.reader).GetComponent<DoorReader>();
                var readerCenter = reader.GetComponent<BoxCollider>().bounds.center;
                AimAt(player, readerCenter + entrance.readerOffset, readerCenter);
                yield return null;
                Assert.That(interaction.CurrentTarget, Is.SameAs(reader), entrance.reader);
                Assert.That(interaction.TryInteract(), Is.True, entrance.reader);
                var scanCard = player.transform.Find("Identity Tag Scan");
                Assert.That(scanCard, Is.Not.Null, entrance.reader);
                Assert.That(scanCard.GetComponentInChildren<MeshFilter>().sharedMesh, Is.Not.Null);
                yield return new WaitForSeconds(0.6f);
                Assert.That(player.transform.Find("Identity Tag Scan"), Is.Null);
                Assert.That(door.IsOpen, Is.True, entrance.door);
                Assert.That(door.IsMoving, Is.False, entrance.door);
                if (entrance.door == "Door_D01_Lobby")
                {
                    var center = door.transform.parent.position;
                    AimAt(player, center + Vector3.forward * 1.4f, center);
                    player.GetComponent<CharacterController>().Move(Vector3.back * 2.8f);
                    Assert.That(player.transform.position.z, Is.LessThan(-5.5f), "D01 must allow passage once opened.");
                }
            }
            Assert.That(inventory.ItemCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OnlySmallMuseumChestsOpenAndGiveTheirOriginalRelics()
        {
            LoadMuseum();
            yield return null;
            foreach (var enemy in Object.FindObjectsByType<EnemyShooting>(FindObjectsInactive.Exclude))
                enemy.enabled = false;

            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var inventory = player.GetComponent<LootInventory>();
            var chests = System.Array.FindAll(Object.FindObjectsByType<LootContainer>(FindObjectsInactive.Exclude),
                container => container.IsChest);
            Assert.That(System.Array.ConvertAll(chests, chest => chest.name), Is.EquivalentTo(new[]
            {
                "Egyptian_Pendent", "Stoneage Axe", "Asian_Dagger", "FinalLoot", "Rare_Fossil", "CoinBox_GreeceRome"
            }));
            var relics = new System.Collections.Generic.List<string>();
            foreach (var chest in chests)
            {
                var item = chest.GetComponentInChildren<LootItem>();
                Assert.That(item, Is.Not.Null, chest.name);
                var lid = System.Array.Find(chest.GetComponentsInChildren<Collider>(),
                    collider => collider.name == "LidVisual");
                Assert.That(lid, Is.Not.Null, chest.name);
                Assert.That(Quaternion.Angle(lid.transform.parent.localRotation, Quaternion.identity),
                    Is.LessThan(0.1f), chest.name);
                Assert.That(item.IsProtected, Is.True, chest.name);

                bool reached = false;
                Vector3 approach = default;
                foreach (Vector3 side in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
                {
                    approach = lid.bounds.center + side * 1.35f;
                    AimAt(player, approach, lid.bounds.center);
                    yield return null;
                    var camera = player.PlayerCamera.transform;
                    var hits = Physics.RaycastAll(camera.position, camera.forward, interaction.Reach,
                        ~0, QueryTriggerInteraction.Ignore);
                    System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
                    var first = System.Array.Find(hits, hit => !hit.transform.IsChildOf(player.transform));
                    if (first.collider == null || !first.transform.IsChildOf(chest.transform)) continue;
                    Assert.That(interaction.CurrentTarget, Is.SameAs(chest));
                    reached = true;
                    break;
                }
                Assert.That(reached, Is.True, chest.name + " needs a clear approach.");
                if (chest.name == "Rare_Fossil")
                {
                    var side = player.PlayerCamera.transform.right;
                    var bounds = lid.bounds;
                    var chestColliders = chest.GetComponentsInChildren<Collider>();
                    bool reachedBesideChest = false;
                    foreach (var direction in new[] { side, -side })
                    {
                        var edge = bounds.ClosestPoint(bounds.center + direction * 5f) + direction * 0.12f;
                        AimAt(player, approach, edge);
                        yield return null;
                        var camera = player.PlayerCamera.transform;
                        var ray = new Ray(camera.position, camera.forward);
                        var hitsChestDirectly = System.Array.Exists(chestColliders,
                            collider => collider.enabled && collider.Raycast(ray, out _, interaction.Reach));
                        if (hitsChestDirectly || interaction.CurrentTarget != chest) continue;
                        reachedBesideChest = true;
                        break;
                    }
                    Assert.That(reachedBesideChest, Is.True, "Aiming just beside the fossil chest should show its prompt.");

                    var view = player.PlayerCamera.transform;
                    wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.transform.SetPositionAndRotation(Vector3.Lerp(view.position, bounds.center, 0.5f), view.rotation);
                    wall.transform.localScale = new Vector3(1.6f, 1.6f, 0.04f);
                    Physics.SyncTransforms();
                    Assert.That(interaction.CurrentTarget, Is.Null, "Aim tolerance must respect walls.");
                    Object.DestroyImmediate(wall);
                    Physics.SyncTransforms();

                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                    yield return null;
                    yield return null;
                    Assert.That(chest.IsOpen, Is.True, "E should open the chest when the crosshair is just beside it.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                else
                {
                    Assert.That(interaction.TryInteract(), Is.True, chest.name);
                }
                yield return new WaitForSeconds(0.85f);
                Assert.That(chest.IsAccessible, Is.True, chest.name);
                Assert.That(Quaternion.Angle(lid.transform.parent.localRotation, Quaternion.identity),
                    Is.EqualTo(110f).Within(0.1f), chest.name);

                AimAt(player, approach, item.GetComponent<Collider>().bounds.center);
                Assert.That(interaction.TryInteract(), Is.True, item.ItemName);
                Assert.That(item.IsCollected, Is.True, item.ItemName);
                relics.Add(item.ItemName);
            }
            Assert.That(inventory.ItemCount, Is.EqualTo(6));
            Assert.That(relics, Is.EquivalentTo(new[]
            {
                "Ancient Eye Pendant", "Stone Age Axe", "Bronze Dagger", "Crystal", "Rare Ammonite Fossil", "Ancient Gold Coin"
            }));
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
            AimAt(player, loot.transform.position - Vector3.forward * distance,
                loot.GetComponent<Collider>().bounds.center);
        }

        private static void AimAt(FirstPersonController player, Vector3 approach, Vector3 target)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = new Vector3(approach.x, 0.05f, approach.z);
            body.enabled = true;
            var direction = target - player.PlayerCamera.transform.position;
            player.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 0f);
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            // Keep the controller's next look update aimed at the block during simulated key input.
            typeof(FirstPersonController).GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            player.CaptureCursor();
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

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class MovablePropTests
    {
        private const string Museum = "Assets/_Project/Scenes/Museum/Museum_Main.unity";

        [UnityTest]
        public IEnumerator MuseumPropsSpawnClearOfGeometryAndHeavyPropsCarveNavigation()
        {
            yield return LoadMuseum();
            var props = Object.FindObjectsByType<MovableProp>(FindObjectsInactive.Exclude);
            Assert.That(props.Select(prop => prop.name), Is.EquivalentTo(new[]
                { "P_Storage_PackingCrate", "cleaning cart", "exhibition partition", "empty cardboard box" }));

            Physics.SyncTransforms();
            foreach (var prop in props)
            {
                var body = prop.GetComponent<Rigidbody>();
                var shape = prop.GetComponent<BoxCollider>();
                var obstacle = prop.GetComponent<NavMeshObstacle>();
                Assert.That(body.isKinematic, Is.False);
                Assert.That(prop.GetComponentInChildren<MeshRenderer>(), Is.Not.Null);
                Assert.That(obstacle != null && obstacle.carving, Is.EqualTo(!prop.CanThrow));

                var hits = Physics.OverlapBox(prop.transform.position, shape.size * 0.5f - Vector3.one * 0.025f,
                    prop.transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                Assert.That(hits.Where(hit => !hit.transform.IsChildOf(prop.transform)).Select(hit => hit.name),
                    Is.Empty, prop.name + " should start clear of nearby geometry");
            }
        }

        [UnityTest]
        public IEnumerator PushStorageCrateAsideBeforeOpeningD07WithTheIdentityTag()
        {
            yield return LoadMuseum();
            var crate = GameObject.Find("P_Storage_PackingCrate").GetComponent<Rigidbody>();
            var door = GameObject.Find("Door_D07_Storage").GetComponentInChildren<Door>();
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var interaction = player.GetComponent<PlayerInteraction>();
            var character = player.GetComponent<CharacterController>();
            var panel = door.GetComponent<BoxCollider>();
            player.CaptureCursor();
            yield return null;

            Vector3 corridorSide = panel.bounds.center + Vector3.right * 1.1f;
            corridorSide.y = 0.05f;
            PlacePlayer(player, corridorSide, panel.bounds.center);
            Assert.That(ReferenceEquals(interaction.CurrentTarget, door), Is.True);
            Assert.That(door.IsBarricaded, Is.True);
            Assert.That(interaction.TryInteract(), Is.False);

            float startingZ = crate.position.z;
            var approach = new Vector3(crate.position.x, 0.05f, startingZ + 1.12f);
            PlacePlayer(player, approach, new Vector3(approach.x, player.PlayerCamera.transform.position.y, startingZ));
            for (int step = 0; step < 150 && door.IsBarricaded; step++)
            {
                character.Move(Vector3.back * 0.065f);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(crate.position.z, Is.LessThan(startingZ - 1.65f));
            Assert.That(door.IsBarricaded, Is.False);
            Assert.That(interaction.IsHoldingProp, Is.False, "Walking into the crate should be enough to move it.");
            PlacePlayer(player, corridorSide, panel.bounds.center);
            Assert.That(ReferenceEquals(interaction.CurrentTarget, door), Is.True);
            Assert.That(interaction.TryInteract(), Is.False, "Clearing the crate must leave the reader access check in effect.");

            var tag = new UnityEditor.SerializedObject(door).FindProperty("requiredIdentityTag").objectReferenceValue;
            Assert.That(tag, Is.Not.Null);
            typeof(LootInventory).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(player.GetComponent<LootInventory>(), new object[] { "Identity tag", 0, tag });
            Assert.That(interaction.TryInteract(), Is.True);
            float end = Time.time + 1.1f;
            while (door.IsMoving && Time.time < end) yield return null;
            Assert.That(door.IsOpen && !door.IsMoving, Is.True);
        }

        [UnityTest]
        public IEnumerator PlayerMovesAndReleasesCrateThenThrowsBoxWithOneLandingCue()
        {
            var inputSettings = InputSystem.settings;
            var oldBackground = inputSettings.backgroundBehavior;
            var oldEditorInput = inputSettings.editorInputBehaviorInPlayMode;
            var oldRunInBackground = Application.runInBackground;
            Keyboard keyboard = null;
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            int landingCues = 0;
            void RecordLanding(MovableProp prop, Vector3 point) { if (prop.CanThrow) landingCues++; }
            MovableProp.DistractionLanded += RecordLanding;

            try
            {
                yield return LoadMuseum();
                keyboard = InputSystem.AddDevice<Keyboard>("MovablePropTestKeyboard");
                var player = Object.FindAnyObjectByType<FirstPersonController>();
                var interaction = player.GetComponent<PlayerInteraction>();
                player.CaptureCursor();
                yield return null;
                Assert.That(player.GetComponent<PlayerEquipment>().TryEquipHands(), Is.True);
                var props = Object.FindObjectsByType<MovableProp>(FindObjectsInactive.Exclude);
                var crate = props.Single(prop => prop.name == "P_Storage_PackingCrate");
                var box = props.Single(prop => prop.CanThrow);

                // Isolate physics and input from the live encounter while using the actual scene props.
                var arena = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arena.transform.position = new Vector3(100f, -0.25f, 100f);
                arena.transform.localScale = new Vector3(22f, 0.5f, 22f);
                var crateBody = crate.GetComponent<Rigidbody>();
                var boxBody = box.GetComponent<Rigidbody>();
                crateBody.position = new Vector3(100f, 0.55f, 102.2f);
                boxBody.position = new Vector3(100f, 0.17f, 97.8f);
                PlacePlayer(player, new Vector3(100f, 0.05f, 100f), crateBody.position);
                yield return null;
                Assert.That(interaction.TryInteract(), Is.True);
                Assert.That(interaction.IsHoldingProp, Is.True);

                var character = player.GetComponent<CharacterController>();
                character.Move(Vector3.back * 0.65f);
                Assert.That(player.transform.position.z, Is.LessThan(99.5f));
                yield return new WaitForSeconds(0.3f);
                Assert.That(crateBody.position.z, Is.LessThan(101.95f));
                Assert.That(interaction.TryInteract(), Is.True);
                Assert.That(interaction.IsHoldingProp, Is.False);

                PlacePlayer(player, new Vector3(100f, 0.05f, 99.1f), boxBody.position);
                yield return null;
                Assert.That(interaction.TryInteract(), Is.True);
                PlacePlayer(player, player.transform.position,
                    player.PlayerCamera.transform.position + Vector3.back * 6f);
                yield return new WaitForSeconds(0.4f);
                Assert.That(landingCues, Is.Zero);
                Vector3 carryPoint = player.PlayerCamera.transform.TransformPoint(0.28f, -0.27f, 0.92f);
                Assert.That(Vector3.Distance(boxBody.position, carryPoint), Is.LessThan(0.6f), "Box should reach the carry point");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return new WaitForSeconds(0.1f);
                Assert.That(keyboard.fKey.isPressed, Is.True, "The test keyboard should deliver the throw input");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(1f);
                Assert.That(interaction.IsHoldingProp, Is.False);
                Assert.That(landingCues, Is.EqualTo(1));
            }
            finally
            {
                MovableProp.DistractionLanded -= RecordLanding;
                inputSettings.backgroundBehavior = oldBackground;
                inputSettings.editorInputBehaviorInPlayMode = oldEditorInput;
                Application.runInBackground = oldRunInBackground;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            }
        }

        private static IEnumerator LoadMuseum()
        {
            var load = EditorSceneManager.LoadSceneAsyncInPlayMode(Museum, new LoadSceneParameters(LoadSceneMode.Single));
            yield return load;
            yield return null;
        }

        private static void PlacePlayer(FirstPersonController player, Vector3 position, Vector3 aim)
        {
            var character = player.GetComponent<CharacterController>();
            character.enabled = false;
            player.transform.position = position;
            player.transform.forward = Vector3.ProjectOnPlane(aim - position, Vector3.up).normalized;
            character.enabled = true;
            var direction = aim - player.PlayerCamera.transform.position;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            typeof(FirstPersonController).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            Physics.SyncTransforms();
        }
    }
}
#endif

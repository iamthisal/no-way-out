#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lockdown.Tests
{
    public sealed class DoorTests
    {
        private GameObject fixture;
        private FirstPersonController player;
        private PlayerInteraction interaction;
        private Door door;
        private BoxCollider panel;
        private Keyboard keyboard;
        private bool previousBackground;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private Vector3 capturePosition;
        private Quaternion captureRotation;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousBackground = Application.runInBackground;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            Application.runInBackground = true;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>("DoorTestKeyboard");
            fixture = new GameObject("Door test fixture");
            fixture.transform.position = new Vector3(200f, 0f, 200f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.SetParent(fixture.transform, false);
            ground.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            var hinge = new GameObject("Hinge");
            hinge.transform.SetParent(fixture.transform, false);
            hinge.transform.localPosition = new Vector3(-0.75f, 1.25f, 2f);
            panel = hinge.AddComponent<BoxCollider>();
            panel.center = new Vector3(0.75f, 0f, 0f);
            panel.size = new Vector3(1.5f, 2.5f, 0.12f);
            door = hinge.AddComponent<Door>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            player = Object.Instantiate(prefab, fixture.transform).GetComponent<FirstPersonController>();
            player.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            player.transform.localRotation = Quaternion.identity;
            interaction = player.GetComponent<PlayerInteraction>();
            AimAt(panel.bounds.center);
            player.CaptureCursor();
            yield return null;
            yield return null;
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(door));
        }

        [UnityTest]
        public IEnumerator EOpensAndClosesWithoutRepeatingWhenHeld()
        {
            Assert.That(door.InteractionLabel, Is.EqualTo("Open door"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(door.IsOpen, Is.True);
            Assert.That(door.InteractionLabel, Is.EqualTo("Close door"));
            yield return WaitForDoor();
            Assert.That(Quaternion.Angle(Quaternion.identity, door.transform.localRotation), Is.EqualTo(95f).Within(0.02f));
            yield return new WaitForSeconds(0.1f);
            Assert.That(door.IsOpen, Is.True, "Holding E must not repeatedly toggle the door.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            AimAt(panel.bounds.center);
            Physics.SyncTransforms();
            Assert.That(interaction.CurrentTarget, Is.SameAs(door));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(door.IsOpen, Is.False);
            yield return WaitForDoor();
            Assert.That(Quaternion.Angle(Quaternion.identity, door.transform.localRotation), Is.LessThan(0.02f));
        }

        [UnityTest]
        public IEnumerator ClosedPanelBlocksPlayerAndOpenPanelClearsPassage()
        {
            var body = player.GetComponent<CharacterController>();
            body.Move(Vector3.forward * 3.5f);
            Assert.That(player.transform.localPosition.z, Is.LessThan(2f));
            ResetPlayer();
            Assert.That(interaction.TryInteract(), Is.True);
            yield return WaitForDoor();
            Physics.SyncTransforms();
            body.Move(Vector3.forward * 3.5f);
            Assert.That(player.transform.localPosition.z, Is.GreaterThan(3f));
        }

        [UnityTest]
        public IEnumerator InteractionRequiresReachSightAndLivingActivePlayer()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(fixture.transform, false);
            wall.transform.localPosition = new Vector3(0f, 1.4f, 1f);
            wall.transform.localScale = new Vector3(2f, 3f, 0.1f);
            Physics.SyncTransforms();
            Assert.That(door.TryInteract(player.gameObject), Is.False, "Direct calls must not bypass walls.");
            Object.DestroyImmediate(wall);
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.localPosition = new Vector3(0f, 0.05f, -4f);
            body.enabled = true;
            Physics.SyncTransforms();
            Assert.That(door.TryInteract(player.gameObject), Is.False, "Distant doors are unreachable.");
            ResetPlayer();
            player.ReleaseCursor();
            Assert.That(door.TryInteract(player.gameObject), Is.False);
            player.CaptureCursor();
            yield return null;
            interaction.enabled = false;
            Assert.That(door.TryInteract(player.gameObject), Is.False);
            interaction.enabled = true;
            door.enabled = false;
            Assert.That(interaction.TryInteract(), Is.False);
            door.enabled = true;
            player.GetComponent<Health>().TakeDamage(100f);
            Assert.That(door.TryInteract(player.gameObject), Is.False);
            Assert.That(door.TryInteract(null), Is.False);
            Assert.That(door.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator MovingDoorCanReverseAndKeepsItsHingePosition()
        {
            fixture.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Physics.SyncTransforms();
            var hingePosition = door.transform.position;
            Assert.That(interaction.TryInteract(), Is.True);
            yield return null;
            AimAt(panel.bounds.center);
            Physics.SyncTransforms();
            Assert.That(interaction.TryInteract(), Is.True);
            yield return WaitForDoor();
            Assert.That(door.IsOpen, Is.False);
            Assert.That(Vector3.Distance(door.transform.position, hingePosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(door.transform.localRotation, Quaternion.identity), Is.LessThan(0.02f));
        }

        [UnityTest]
        public IEnumerator MuseumInteriorDoorsHaveMovingCollidersAndFixedWashroomFrames()
        {
            // This checkout already references a missing exterior-door prefab. Expect only
            // those two specific load errors while still checking every interior door.
            if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath("3c09e9fafae22354683897089c02f979")))
            {
                LogAssert.Expect(LogType.Error, new Regex("^Problem detected while opening the Scene file: 'Assets/_Project/Scenes/Museum/Museum_Main.unity'"));
                LogAssert.Expect(LogType.Error, "Prefab instance problem. Missing Prefab Asset: 'Exit_MuseumDoubleDoor (Missing Prefab with guid: 3c09e9fafae22354683897089c02f979)'");
            }
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            var museum = SceneManager.GetSceneByName("Museum_Main");
            foreach (var root in museum.GetRootGameObjects())
            {
                foreach (var shooting in root.GetComponentsInChildren<Lockdown.Enemies.EnemyShooting>(true)) shooting.enabled = false;
                foreach (var otherPlayer in root.GetComponentsInChildren<FirstPersonController>(true)) otherPlayer.gameObject.SetActive(false);
            }
            // This sweep checks how doors move, so release the mission locks first.
            foreach (var root in museum.GetRootGameObjects())
                foreach (var doorLock in root.GetComponentsInChildren<DoorLock>(true)) doorLock.Unlock();
            int count = 0;
            foreach (var root in museum.GetRootGameObjects())
            {
                foreach (var configured in root.GetComponentsInChildren<Door>(true))
                {
                    count++;
                    Assert.That(configured.GetComponent<BoxCollider>(), Is.Not.Null);
                    Assert.That(configured.GetComponent<UnityEngine.AI.NavMeshObstacle>().carving, Is.True);
                    Assert.That(configured.transform.childCount, Is.GreaterThan(0));
                    var collider = configured.GetComponent<BoxCollider>();
                    var closedRotation = configured.transform.localRotation;
                    var normal = configured.transform.forward;
                    bool found = false;
                    foreach (float side in new[] { 1f, -1f })
                    {
                        var body = player.GetComponent<CharacterController>();
                        body.enabled = false;
                        var position = collider.bounds.center + normal * side * 1.6f;
                        position.y = 0.05f;
                        player.transform.position = position;
                        body.enabled = true;
                        AimAt(collider.bounds.center);
                        player.CaptureCursor();
                        yield return null;
                        Physics.SyncTransforms();
                        if (interaction.CurrentTarget == configured) { found = true; break; }
                    }
                    Assert.That(found, Is.True, configured.transform.parent.name + " cannot be reached from either side.");
                    if (configured.transform.parent.name == "Door_D09_Security") Capture(configured, "closed");
                    Assert.That(interaction.TryInteract(), Is.True);
                    float deadline = Time.realtimeSinceStartup + 3f;
                    while (configured.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(configured.IsMoving, Is.False);
                    Assert.That(Quaternion.Angle(configured.transform.localRotation, closedRotation), Is.GreaterThan(90f));
                    Physics.SyncTransforms();
                    if (configured.transform.parent.name == "Door_D09_Security") Capture(configured, "open");
                    // Approach the opened leaf rather than interacting through empty space.
                    var controller = player.GetComponent<CharacterController>();
                    controller.enabled = false;
                    var approach = collider.bounds.center + configured.transform.forward * 1.2f;
                    approach.y = 0.05f;
                    player.transform.position = approach;
                    controller.enabled = true;
                    AimAt(collider.bounds.center);
                    Physics.SyncTransforms();
                    Assert.That(interaction.TryInteract(), Is.True, "Opened panel must remain interactable: " + configured.transform.parent.name);
                    deadline = Time.realtimeSinceStartup + 3f;
                    while (configured.IsMoving && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(configured.IsOpen, Is.False);
                    Assert.That(configured.IsMoving, Is.False);
                    Assert.That(Quaternion.Angle(configured.transform.localRotation, closedRotation), Is.LessThan(0.02f));
                    if (configured.transform.parent.name.Contains("Toilet"))
                    {
                        var frame = configured.transform.parent.Find("Visuals/door_washroom/Cube.001");
                        Assert.That(frame, Is.Not.Null);
                        Assert.That(frame.IsChildOf(configured.transform), Is.False);
                        Assert.That(frame.GetComponent<MeshCollider>(), Is.Not.Null);
                    }
                }
            }
            Assert.That(count, Is.EqualTo(7));
            yield return SceneManager.UnloadSceneAsync(museum);
        }

        private void Capture(Door configured, string state)
        {
            var view = new GameObject("Door verification camera");
            view.transform.SetParent(fixture.transform);
            var camera = view.AddComponent<Camera>();
            if (state == "closed")
            {
                capturePosition = player.PlayerCamera.transform.position;
                captureRotation = player.PlayerCamera.transform.rotation;
            }
            view.transform.SetPositionAndRotation(capturePosition, captureRotation);
            var target = new RenderTexture(640, 640, 24);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 640f, 640f), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Temp/DoorsVerification");
                File.WriteAllBytes("Temp/DoorsVerification/door-" + state + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(view);
            }
        }

        private IEnumerator WaitForDoor()
        {
            float timeout = Time.realtimeSinceStartup + 3f;
            while (door.IsMoving && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(door.IsMoving, Is.False, "Door animation did not finish.");
        }

        private void ResetPlayer()
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            body.enabled = true;
            AimAt(panel.bounds.center);
            Physics.SyncTransforms();
        }

        private void AimAt(Vector3 point)
        {
            var direction = point - player.PlayerCamera.transform.position;
            var local = fixture.transform.InverseTransformDirection(direction);
            player.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, 0f);
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            typeof(FirstPersonController).GetField("pitch", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, pitch);
            player.PlayerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        [TearDown]
        public void Cleanup()
        {
            if (fixture != null) Object.DestroyImmediate(fixture);
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

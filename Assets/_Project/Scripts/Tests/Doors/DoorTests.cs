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

#if UNITY_EDITOR
using System.Collections;
using Lockdown.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class FirstPersonPlayerTests
    {
        private GameObject player;
        private GameObject floor;
        private GameObject wall;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool previousRunInBackground;

        [SetUp]
        public void SetUp()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        [UnityTest]
        public IEnumerator PlayerMovesLooksJumpsAndCollidesUsingProjectBindings()
        {
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                listener.enabled = false;
            foreach (var otherPlayer in Object.FindObjectsByType<FirstPersonController>(FindObjectsSortMode.None))
                otherPlayer.gameObject.SetActive(false);
            keyboard = InputSystem.AddDevice<Keyboard>("PlayerTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("PlayerTestMouse");
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "PlayerTestFloor";
            floor.transform.position = new Vector3(60f, -0.25f, 0f);
            floor.transform.localScale = new Vector3(20f, 0.5f, 40f);
            Physics.SyncTransforms();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);
            player = Object.Instantiate(prefab, new Vector3(60f, 0.05f, -10f), Quaternion.identity);
            var controller = player.GetComponent<FirstPersonController>();
            var body = player.GetComponent<CharacterController>();
            controller.CaptureCursor();
            yield return new WaitForSeconds(1f);
            Assert.That(controller.IsGrounded, Is.True, "Gravity should settle the player on the floor. Position=" + player.transform.position);
            Assert.That(controller.PlayerCamera.transform.localPosition.y, Is.EqualTo(1.6f).Within(0.01f));

            Vector3 start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            float walked = player.transform.position.z - start.z;
            Assert.That(walked, Is.InRange(1.5f, 3f), "W should move forward at walking speed.");

            start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return new WaitForSeconds(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            float sprinted = player.transform.position.z - start.z;
            Assert.That(sprinted, Is.GreaterThan(walked * 1.3f), "Sprint should increase travel speed.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return new WaitForSeconds(0.15f);
            Assert.That(player.transform.position.y, Is.GreaterThan(0.35f), "Space should jump from the floor.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSeconds(0.8f);
            Assert.That(controller.IsGrounded, Is.True, "The player should land after jumping.");

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "PlayerTestWall";
            wall.transform.position = new Vector3(60f, 2f, player.transform.position.z + 1.5f);
            wall.transform.localScale = new Vector3(8f, 4f, 0.4f);
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
            yield return new WaitForSeconds(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(player.transform.position.z, Is.LessThan(wall.transform.position.z - 0.4f), "The body must not pass through the wall.");

            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(150f, 2000f) });
            yield return null;
            yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, player.transform.eulerAngles.y)), Is.GreaterThan(5f), "Mouse input should turn the body.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, controller.PlayerCamera.transform.localEulerAngles.x)), Is.InRange(80f, 85.1f), "Vertical look must be clamped.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            Assert.That(controller.IsCursorCaptured, Is.False, "Escape should release the cursor.");
            start = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(0.2f);
            Assert.That(Vector3.Distance(start, player.transform.position), Is.LessThan(0.1f), "Movement should stop while the cursor is released.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            controller.CaptureCursor();
            body.enabled = false;
            player.transform.position = new Vector3(60f, -30f, 0f);
            body.enabled = true;
            yield return null;
            yield return null;
            Assert.That(player.transform.position.z, Is.EqualTo(-10f).Within(0.1f), "Falling outside the world should restore the spawn position.");

            Cleanup();
        }

        [UnityTest]
        public IEnumerator MuseumPlayerSpawnsOnFloorAndWalksThroughLobbyDoorway()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>("MuseumTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("MuseumTestMouse");
            var controller = Object.FindFirstObjectByType<FirstPersonController>();
            Assert.That(controller, Is.Not.Null, "Museum_Main should contain the player prefab.");
            player = controller.gameObject;
            controller.CaptureCursor();
            yield return new WaitForSeconds(0.5f);
            Assert.That(controller.IsGrounded, Is.True, "The museum floor should support the player.");
            int activeCameras = 0;
            int activeListeners = 0;
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (camera.isActiveAndEnabled) activeCameras++;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) activeListeners++;
            Assert.That(activeCameras, Is.EqualTo(1));
            Assert.That(activeListeners, Is.EqualTo(1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(1.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(player.transform.position.z, Is.GreaterThan(-5f), "The player should fit through the lobby doorway into the central hall.");
            Assert.That(controller.IsGrounded, Is.True, "The player should remain on the museum floor.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Cleanup();
            Application.runInBackground = previousRunInBackground;
            yield return null;
        }

        private void Cleanup()
        {
            if (player != null) Object.DestroyImmediate(player);
            if (floor != null) Object.DestroyImmediate(floor);
            if (wall != null) Object.DestroyImmediate(wall);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif

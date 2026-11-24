#if UNITY_EDITOR
using System.Collections;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class MenuTests
    {
        private const string VolumeKey = "Lockdown.MasterVolume";
        private Keyboard keyboard;
        private bool previousBackground;
        private InputSettings.BackgroundBehavior previousInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private float previousVolume;
        private bool hadSavedVolume;
        private float savedVolume;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            previousInputBackground = InputSystem.settings.backgroundBehavior;
            previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousVolume = AudioListener.volume;
            hadSavedVolume = PlayerPrefs.HasKey(VolumeKey);
            savedVolume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        }

        [Test]
        public void MainMenuIsTheFirstBuildSceneAndTheMuseumFollows()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(scenes[0].path, Is.EqualTo(SceneFlow.MainMenuScene), "Builds must open on the title screen.");
            Assert.That(scenes[0].enabled, Is.True);
            Assert.That(System.Array.Exists(scenes, s => s.path == SceneFlow.MuseumScene && s.enabled), Is.True);
            Assert.That(MainMenu.GameTitle, Is.EqualTo("No Way Out"));
        }

        [Test]
        public void MasterVolumeIsClampedAppliedAndRemembered()
        {
            GameSettings.Volume = 0.35f;
            Assert.That(AudioListener.volume, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(PlayerPrefs.GetFloat(VolumeKey), Is.EqualTo(0.35f).Within(0.0001f));
            GameSettings.Volume = 2f;
            Assert.That(GameSettings.Volume, Is.EqualTo(1f));
            GameSettings.Volume = -1f;
            Assert.That(GameSettings.Volume, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MainMenuOpensUnfrozenWithAFreeCursor()
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            EditorSceneManager.LoadSceneInPlayMode(SceneFlow.MainMenuScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var menu = Object.FindAnyObjectByType<MainMenu>();
            Assert.That(menu, Is.Not.Null, "The main menu scene needs its MainMenu component.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            menu.OpenOptions();
            Assert.That(menu.IsShowingOptions, Is.True);
            menu.CloseOptions();
            Assert.That(menu.IsShowingOptions, Is.False);
        }

        [UnityTest]
        public IEnumerator StartGameLoadsTheMuseumWithAPausablePlayer()
        {
            EditorSceneManager.LoadSceneInPlayMode(SceneFlow.MainMenuScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindAnyObjectByType<MainMenu>().StartGame();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(SceneFlow.MuseumScene));
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            Assert.That(player, Is.Not.Null);
            Assert.That(player.GetComponent<PauseMenu>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator EscapePausesTheMuseumAndEscapeAgainContinues()
        {
            yield return LoadMuseum();
            keyboard = InputSystem.AddDevice<Keyboard>("MenuTestKeyboard");
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var pause = player.GetComponent<PauseMenu>();
            Assert.That(pause, Is.Not.Null, "PlayerVitals should give the player a pause menu.");
            player.CaptureCursor();
            yield return null;

            yield return Tap(Key.Escape);
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            Assert.That(player.IsCursorCaptured, Is.False);
            Assert.That(player.InputBlocked, Is.True);

            pause.OpenOptions();
            yield return Tap(Key.Escape);
            Assert.That(pause.IsShowingOptions, Is.False, "Escape should step back out of Options...");
            Assert.That(pause.IsPaused, Is.True, "...without leaving the pause menu.");

            yield return Tap(Key.Escape);
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
            Assert.That(player.IsCursorCaptured, Is.True);
            Assert.That(player.InputBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator PauseNeverOpensOverTheEndScreens()
        {
            yield return LoadMuseum();
            var vitals = Object.FindAnyObjectByType<PlayerVitals>();
            var pause = vitals.GetComponent<PauseMenu>();
            Assert.That(vitals.Capture(), Is.True);
            pause.Pause();
            Assert.That(pause.IsPaused, Is.False, "The caught screen owns the screen; pausing must not stack on it.");
        }

        [UnityTest]
        public IEnumerator MainMenuButtonLeavesThePauseBehind()
        {
            yield return LoadMuseum();
            var pause = Object.FindAnyObjectByType<PauseMenu>();
            pause.Pause();
            Assert.That(Time.timeScale, Is.Zero);
            pause.ReturnToMainMenu();
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(SceneFlow.MainMenuScene));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(AudioListener.pause, Is.False);
        }

        private static IEnumerator LoadMuseum()
        {
            EditorSceneManager.LoadSceneInPlayMode(SceneFlow.MuseumScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var enemy in Object.FindObjectsByType<EnemyShooting>(FindObjectsInactive.Exclude)) enemy.enabled = false;
            yield return null;
        }

        private IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        [TearDown]
        public void Cleanup()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            AudioListener.volume = previousVolume;
            if (hadSavedVolume) PlayerPrefs.SetFloat(VolumeKey, savedVolume);
            else PlayerPrefs.DeleteKey(VolumeKey);
            Application.runInBackground = previousBackground;
            InputSystem.settings.backgroundBehavior = previousInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInput;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif

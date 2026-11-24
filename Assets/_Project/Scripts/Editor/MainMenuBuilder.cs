using System;
using System.Collections.Generic;
using System.IO;
using Lockdown.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    /// <summary>Generates the main menu scene and makes it the first scene of every build.</summary>
    public static class MainMenuBuilder
    {
        private static readonly Color Background = new Color(0.06f, 0.045f, 0.035f, 1f);

        [MenuItem("Tools/Lockdown/Build Main Menu")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play mode before building the main menu.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = SceneManager.GetActiveScene().path;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            cameraObject.AddComponent<AudioListener>();
            new GameObject("Main Menu").AddComponent<MainMenu>();

            Directory.CreateDirectory(Path.GetDirectoryName(SceneFlow.MainMenuScene));
            if (!EditorSceneManager.SaveScene(scene, SceneFlow.MainMenuScene))
                throw new IOException("Could not save " + SceneFlow.MainMenuScene);
            PutFirstInBuild();
            Debug.Log("Built " + SceneFlow.MainMenuScene + " and made it the first build scene.");

            if (!string.IsNullOrEmpty(previous) && previous != SceneFlow.MainMenuScene)
                EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        }

        // The build starts at the first enabled scene, so the menu goes first and the museum stays enabled.
        private static void PutFirstInBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == SceneFlow.MainMenuScene);
            scenes.Insert(0, new EditorBuildSettingsScene(SceneFlow.MainMenuScene, true));
            int museum = scenes.FindIndex(s => s.path == SceneFlow.MuseumScene);
            if (museum < 0) scenes.Insert(1, new EditorBuildSettingsScene(SceneFlow.MuseumScene, true));
            else scenes[museum].enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.UI
{
    /// <summary>The game's scenes and how to move between them, in the editor and in builds.</summary>
    public static class SceneFlow
    {
        public const string MainMenuScene = "Assets/_Project/Scenes/MainMenu/MainMenu.unity";
        public const string MuseumScene = "Assets/_Project/Scenes/Museum/Museum_Main.unity";

        /// <summary>Loads a scene by path, resetting the pause state it might inherit.</summary>
        public static void Load(string path)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
#if UNITY_EDITOR
            // Works in Play mode even for scenes that are not in the build list.
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path, LoadSceneMode.Single);
#endif
        }

        /// <summary>Leaves the game; in the editor this stops Play mode.</summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Browsers ignore quitting, so the Web build hides Exit.</summary>
        public static bool CanQuit => Application.platform != RuntimePlatform.WebGLPlayer;
    }
}

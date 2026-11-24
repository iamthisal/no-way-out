using UnityEngine;

namespace Lockdown.UI
{
    /// <summary>Player options that persist between sessions. For now, the master volume.</summary>
    public static class GameSettings
    {
        internal const string VolumeKey = "Lockdown.MasterVolume";

        /// <summary>Master volume from 0 to 1, applied to every sound through the AudioListener.</summary>
        public static float Volume
        {
            get => AudioListener.volume;
            set
            {
                float volume = Mathf.Clamp01(value);
                AudioListener.volume = volume;
                PlayerPrefs.SetFloat(VolumeKey, volume);
            }
        }

        /// <summary>Writes changed options to disk; call when the options panel closes.</summary>
        public static void Save() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplySaved() => AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
    }
}

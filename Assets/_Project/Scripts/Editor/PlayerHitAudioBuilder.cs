using System;
using Lockdown.Player;
using UnityEditor;
using UnityEngine;

namespace Lockdown.Editor
{
    public static class PlayerHitAudioBuilder
    {
        public const string ClipPath = "Assets/_Project/Audio/Player/ClassicOof.ogg";
        public const string DeathClipPath = "Assets/_Project/Audio/Player/WilhelmDeathScream.ogg";

        [MenuItem("Tools/Lockdown/Configure Player Hit Audio")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before configuring player audio.");
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath) == null)
                throw new InvalidOperationException("The player hit audio clip is missing.");
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(DeathClipPath) == null)
                throw new InvalidOperationException("The player death audio clip is missing.");
            var player = PrefabUtility.LoadPrefabContents(FirstPersonPlayerBuilder.PrefabPath);
            try
            {
                Configure(player);
                PrefabUtility.SaveAsPrefabAsset(player, FirstPersonPlayerBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
        }

        public static void Configure(GameObject player)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null) return;
            var vitals = player.GetComponent<PlayerVitals>();
            if (vitals == null) throw new InvalidOperationException("PlayerVitals is missing.");
            var audio = player.GetComponent<AudioSource>();
            if (audio == null) audio = player.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 0f;
            var serialized = new SerializedObject(vitals);
            serialized.FindProperty("hitSound").objectReferenceValue = clip;
            var deathClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DeathClipPath);
            if (deathClip != null) serialized.FindProperty("deathSound").objectReferenceValue = deathClip;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

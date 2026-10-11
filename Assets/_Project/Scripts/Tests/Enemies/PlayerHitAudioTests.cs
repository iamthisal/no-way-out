#if UNITY_EDITOR
using System.Collections;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lockdown.Tests
{
    public sealed class PlayerHitAudioTests
    {
        private bool previousBackground;

        [SetUp]
        public void Setup()
        {
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        [TearDown]
        public void Cleanup() { Application.runInBackground = previousBackground; }

        [UnityTest]
        public IEnumerator HitAndDeathSoundsAreDistinctAndRejectedDamageIsSilent()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/Museum/Museum_Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            Object.FindFirstObjectByType<EnemyShooting>().enabled = false;
            var player = Object.FindFirstObjectByType<PlayerVitals>();
            var health = player.GetComponent<Health>();
            var audio = player.GetComponent<AudioSource>();
            var clip = new SerializedObject(player).FindProperty("hitSound").objectReferenceValue as AudioClip;
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            Assert.That(clip.length, Is.GreaterThan(0f));
            var samples = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(samples, 0), Is.True);
            Assert.That(System.Array.Exists(samples, sample => Mathf.Abs(sample) > 0.01f), Is.True,
                "The imported recording must contain audible samples.");
            var deathClip = new SerializedObject(player).FindProperty("deathSound").objectReferenceValue as AudioClip;
            Assert.That(deathClip, Is.Not.Null);
            Assert.That(deathClip, Is.Not.SameAs(clip));
            Assert.That(deathClip.loadState, Is.EqualTo(AudioDataLoadState.Loaded));
            var deathSamples = new float[deathClip.samples * deathClip.channels];
            Assert.That(deathClip.GetData(deathSamples, 0), Is.True);
            Assert.That(System.Array.Exists(deathSamples, sample => Mathf.Abs(sample) > 0.01f), Is.True);
            Assert.That(audio.isPlaying, Is.False);

            health.TakeDamage(0f);
            health.TakeDamage(-10f);
            health.TakeDamage(float.NaN);
            health.TakeDamage(float.PositiveInfinity);
            Assert.That(audio.isPlaying, Is.False);
            health.TakeDamage(10f);
            Assert.That(audio.isPlaying, Is.True);
            Assert.That(audio.clip, Is.Null, "Nonfatal hits use one-shot oof playback, not the death scream.");
            health.TakeDamage(100f);
            Assert.That(health.IsDead, Is.True);
            Assert.That(audio.isPlaying, Is.True, "The death scream should replace active hit audio.");
            Assert.That(audio.clip, Is.SameAs(deathClip));
            Assert.That(audio.volume, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(audio.loop, Is.False);
            yield return new WaitForSeconds(deathClip.length + 0.1f);
            Assert.That(audio.isPlaying, Is.False, "The scream should finish without looping.");
            audio.Stop();
            health.TakeDamage(10f);
            Assert.That(audio.isPlaying, Is.False, "Dead players should not repeat the hit sound.");
        }
    }
}
#endif

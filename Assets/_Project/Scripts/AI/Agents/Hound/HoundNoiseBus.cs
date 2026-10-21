using System;
using Lockdown.AI.Core.Hound;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    // Gameplay emits signals explicitly. Playing an AudioSource alone does not emit a clue.
    public static class HoundNoiseBus
    {
        public static event Action<HoundSound, Transform> NoiseEmitted;

        public static void Emit(Vector3 position, HoundSoundKind kind, Transform source = null)
        {
            if (!Application.isPlaying || Time.timeScale <= 0f) return;
            Publish(HoundSoundProfiles.Create(kind, position, Time.timeAsDouble), source);
        }

        public static void Publish(HoundSound sound, Transform source = null)
        {
            if (!Application.isPlaying || Time.timeScale <= 0f || !sound.IsValid) return;
            NoiseEmitted?.Invoke(sound, source);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { NoiseEmitted = null; }
    }
}

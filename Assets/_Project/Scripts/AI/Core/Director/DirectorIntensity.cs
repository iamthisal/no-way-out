using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Internal state: smoothed stress that rises fast and falls slowly, like real tension.</summary>
    public sealed class DirectorIntensity
    {
        private readonly DirectorTuning tuning;
        public float Value { get; private set; }

        public DirectorIntensity(DirectorTuning tuning) { this.tuning = tuning; }

        public void Tick(float stress, float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(stress)) return;
            float rate = stress > Value ? tuning.riseRate : tuning.fallRate;
            Value += (Mathf.Clamp01(stress) - Value) * (1f - Mathf.Exp(-rate * deltaTime));
        }
    }
}

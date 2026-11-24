using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>A 0-1 signal that halves every half-life. Used for recent damage, noise and being seen.</summary>
    public sealed class DecayingMeter
    {
        private readonly float halfLife;
        public float Value { get; private set; }

        public DecayingMeter(float halfLife) { this.halfLife = Mathf.Max(0.01f, halfLife); }

        /// <summary>Accumulates, so several small hits add up.</summary>
        public void Add(float amount)
        {
            if (amount > 0f && !float.IsNaN(amount)) Value = Mathf.Clamp01(Value + amount);
        }

        /// <summary>Lifts the signal to a level without accumulating, so steady footsteps stay at that level.</summary>
        public void RaiseTo(float level)
        {
            if (!float.IsNaN(level)) Value = Mathf.Max(Value, Mathf.Clamp01(level));
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) Value *= Mathf.Pow(0.5f, deltaTime / halfLife);
        }

        public void Reset() { Value = 0f; }
    }
}

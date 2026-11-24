using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Continuous contact is required. A broken reach/line of sight cancels the attempt.</summary>
    public sealed class PursuerArrest
    {
        private float elapsed;
        public float Progress { get; private set; }
        public bool Tick(bool inReach, float deltaTime, float duration)
        {
            if (!inReach) { Reset(); return false; }
            elapsed += Mathf.Max(0f, deltaTime);
            Progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
            return Progress >= 1f;
        }
        public void Reset() { elapsed = 0f; Progress = 0f; }
    }
}

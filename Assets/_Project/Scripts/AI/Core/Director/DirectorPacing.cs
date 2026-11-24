using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Append only: values may be logged and compared across runs.</summary>
    public enum DirectorPhase { BuildUp, Peak, Relax }

    /// <summary>Build-up, peak and relax phases. Only build-up has a positive intensity goal.</summary>
    public sealed class DirectorPacing
    {
        private readonly DirectorTuning tuning;
        public DirectorPhase Phase { get; private set; } = DirectorPhase.BuildUp;
        public float TimeInPhase { get; private set; }

        public DirectorPacing(DirectorTuning tuning) { this.tuning = tuning; }

        /// <summary>The intensity goal. It grows with the loot carried, so a nearly finished heist is harder.</summary>
        public float Target(float lootFraction) => Phase == DirectorPhase.BuildUp
            ? tuning.baseTarget + tuning.lootTarget * Mathf.Clamp01(lootFraction) : 0f;

        public void Tick(float intensity, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            TimeInPhase += deltaTime;
            if (Phase == DirectorPhase.BuildUp && intensity >= tuning.peakEnter) Enter(DirectorPhase.Peak);
            else if (Phase == DirectorPhase.Peak && (intensity < tuning.peakExit || TimeInPhase >= tuning.peakMaximum))
                Enter(DirectorPhase.Relax);
            else if (Phase == DirectorPhase.Relax && TimeInPhase >= tuning.relaxMinimum && intensity < tuning.relaxExit)
                Enter(DirectorPhase.BuildUp);
        }

        private void Enter(DirectorPhase phase) { Phase = phase; TimeInPhase = 0f; }
    }
}

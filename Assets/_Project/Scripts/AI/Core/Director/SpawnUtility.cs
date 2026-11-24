namespace Lockdown.AI.Core.Director
{
    /// <summary>Utility of one action: spawn this type at this point. Compared against the utility of waiting.</summary>
    public static class SpawnUtility
    {
        public static float Threat(SpawnKind kind, DirectorTuning tuning) =>
            kind == SpawnKind.Guard ? tuning.guardThreat : kind == SpawnKind.Archer ? tuning.archerThreat : tuning.houndThreat;

        public static float Compute(SpawnKind kind, float deficit, float fit, float point,
            in DirectorObservation observation, DirectorTuning tuning)
        {
            float utility = deficit <= 0f ? 0f : deficit * SpawnLimits.FreeShare(observation.Census, tuning)
                * (tuning.threatWeight * Threat(kind, tuning) + tuning.fitWeight * fit + tuning.pointWeight * point);
            // The bonus is scaled by the point, so the answer to an alarm comes from close to it.
            if (kind == SpawnKind.Guard && observation.AlarmUnanswered) utility += tuning.alarmBonus + tuning.pointWeight * point;
            return utility;
        }
    }
}

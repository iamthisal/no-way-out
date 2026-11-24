using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Instant stress: a weighted sum of how hard the player is being pressed right now.</summary>
    public static class DirectorStress
    {
        public static float Compute(in DirectorObservation observation, DirectorTuning tuning)
        {
            float injury = 1f - Mathf.Clamp01(observation.HealthFraction);
            float engaged = Mathf.Clamp01((float)observation.EngagedEnemies / Mathf.Max(1, tuning.engagedForFullStress));
            return Mathf.Clamp01(tuning.damageWeight * Mathf.Clamp01(observation.RecentDamage)
                + tuning.injuryWeight * injury
                + tuning.engagedWeight * engaged
                + tuning.seenWeight * Mathf.Clamp01(observation.Seen));
        }
    }
}

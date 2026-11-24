using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Soft 0-1 score for a spawn point that already passed the hard filters.</summary>
    public static class SpawnPointScorer
    {
        private const float DistanceShare = 0.4f, NoveltyShare = 0.3f, RelevanceShare = 0.3f, ExitRelevance = 0.6f;

        public static float Score(in SpawnCandidate candidate, DirectorTuning tuning) =>
            DistanceShare * DistanceComfort(candidate.RouteLength, tuning)
            + NoveltyShare * Novelty(candidate.SecondsSinceUsed, tuning)
            + RelevanceShare * Relevance(candidate, tuning);

        /// <summary>1 inside the comfort band, falling to 0 at the nearest and farthest allowed routes.</summary>
        public static float DistanceComfort(float route, DirectorTuning tuning)
        {
            if (route < tuning.comfortMinimum) return Mathf.InverseLerp(tuning.minimumRoute, tuning.comfortMinimum, route);
            if (route > tuning.comfortMaximum) return Mathf.InverseLerp(tuning.maximumRoute, tuning.comfortMaximum, route);
            return 1f;
        }

        /// <summary>0 just after use, recovering towards 1, so enemies do not keep using one entrance.</summary>
        public static float Novelty(float secondsSinceUsed, DirectorTuning tuning) =>
            float.IsInfinity(secondsSinceUsed) ? 1f : 1f - Mathf.Exp(-Mathf.Max(0f, secondsSinceUsed) / tuning.noveltySeconds);

        /// <summary>How well the point suits the story: close to the alarm, or along the way out.</summary>
        public static float Relevance(in SpawnCandidate candidate, DirectorTuning tuning)
        {
            float alarm = float.IsInfinity(candidate.AlarmDistance) ? 0f
                : Mathf.Exp(-Mathf.Max(0f, candidate.AlarmDistance) / tuning.alarmRelevanceDistance);
            return Mathf.Max(alarm, candidate.OnExitRoute ? ExitRelevance : 0f);
        }
    }
}

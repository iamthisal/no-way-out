using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>How well an enemy type suits the player's behaviour and the place, 0-1.</summary>
    public static class SpawnFit
    {
        public static float Score(SpawnKind kind, SpawnZone zone, in DirectorObservation observation)
        {
            if (kind == SpawnKind.Hound) return Mathf.Lerp(0.2f, 1f, observation.Noise);
            if (kind == SpawnKind.Archer)
                return (zone & SpawnZone.Corridor) != 0 ? 0.9f : (zone & SpawnZone.OpenGallery) != 0 ? 0.4f : 0.25f;
            // A guard is the natural answer to an alarm nobody else can reach.
            if (observation.AlarmUnanswered) return 1f;
            float openness = (zone & SpawnZone.OpenGallery) != 0 ? 0.36f : 0f;
            return Mathf.Clamp01(0.3f + openness + 0.4f * Mathf.Clamp01(observation.LootFraction));
        }
    }
}

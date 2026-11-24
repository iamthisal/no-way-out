namespace Lockdown.AI.Core.Director
{
    /// <summary>When the director may quietly remove an enemy it spawned earlier.</summary>
    public static class DespawnRule
    {
        /// <param name="route">Walking distance to the player; infinity when the player cannot be reached.</param>
        public static bool Allows(DirectorPhase phase, float route, float unseenSeconds, bool engaged, DirectorTuning tuning) =>
            phase != DirectorPhase.Peak && !engaged
            && route > tuning.despawnRoute && unseenSeconds >= tuning.despawnUnseenSeconds;
    }
}

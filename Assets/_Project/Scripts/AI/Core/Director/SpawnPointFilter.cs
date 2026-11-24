namespace Lockdown.AI.Core.Director
{
    public enum SpawnRejection { None, NotAllowedHere, Visible, Distance, Unreachable, Crowded, SafeRoom, BlocksWoundedPlayer }

    /// <summary>Hard rules a spawn point must pass before it is scored at all.</summary>
    public static class SpawnPointFilter
    {
        public static SpawnRejection Check(in SpawnCandidate candidate, SpawnKind kind, float healthFraction, DirectorTuning tuning)
        {
            if (!candidate.Allowed.Contains(kind)) return SpawnRejection.NotAllowedHere;
            if (candidate.InSafeRoom) return SpawnRejection.SafeRoom;
            if (candidate.InView || candidate.HasLineOfSight) return SpawnRejection.Visible;
            if (!candidate.Reachable) return SpawnRejection.Unreachable;
            if (candidate.RouteLength < tuning.minimumRoute || candidate.RouteLength > tuning.maximumRoute) return SpawnRejection.Distance;
            if (!candidate.ClearSpace) return SpawnRejection.Crowded;
            if (candidate.OnExitRoute && healthFraction < tuning.woundedHealth) return SpawnRejection.BlocksWoundedPlayer;
            return SpawnRejection.None;
        }

        /// <summary>A point lies on the way out when going through it is hardly longer than going straight there.</summary>
        public static bool LiesOnRoute(float playerToPoint, float pointToExit, float playerToExit, float slack) =>
            !float.IsInfinity(playerToExit) && playerToPoint + pointToExit <= playerToExit * slack;
    }
}

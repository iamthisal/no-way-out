namespace Lockdown.AI.Core.Director
{
    /// <summary>Hard population caps. They apply to every enemy, not only those the director spawned.</summary>
    public static class SpawnLimits
    {
        public static int Maximum(SpawnKind kind, DirectorTuning tuning) =>
            kind == SpawnKind.Guard ? tuning.maximumGuards
            : kind == SpawnKind.Archer ? tuning.maximumArchers : tuning.maximumHounds;

        public static bool HasRoom(SpawnKind kind, in EnemyCensus census, DirectorTuning tuning) =>
            census.Total < tuning.maximumTotal && census.Count(kind) < Maximum(kind, tuning);

        /// <summary>1 when the museum is empty, 0 when it is full. Scales down further spawns.</summary>
        public static float FreeShare(in EnemyCensus census, DirectorTuning tuning)
        {
            if (tuning.maximumTotal <= 0) return 0f;
            float share = 1f - (float)census.Active / tuning.maximumTotal;
            return share < 0f ? 0f : share > 1f ? 1f : share;
        }
    }
}

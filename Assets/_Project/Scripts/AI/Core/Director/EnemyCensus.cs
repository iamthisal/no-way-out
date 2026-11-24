namespace Lockdown.AI.Core.Director
{
    /// <summary>Living enemies in the museum, whoever placed them.</summary>
    public struct EnemyCensus
    {
        public int Guards, Archers, Hounds;
        /// <summary>Alive but not awake yet, such as an archer still lying as a pile of bones.</summary>
        public int Dormant;

        public int Total => Guards + Archers + Hounds;
        public int Active => Total - Dormant;

        public int Count(SpawnKind kind) => kind == SpawnKind.Guard ? Guards : kind == SpawnKind.Archer ? Archers : Hounds;

        public void Add(SpawnKind kind, bool dormant = false)
        {
            if (kind == SpawnKind.Guard) Guards++;
            else if (kind == SpawnKind.Archer) Archers++;
            else Hounds++;
            if (dormant) Dormant++;
        }
    }
}

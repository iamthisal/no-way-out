namespace Lockdown.AI.Core.Director
{
    /// <summary>The director's percepts for one tick. The director observes the whole museum; the enemies do not.</summary>
    public struct DirectorObservation
    {
        /// <summary>False while paused, or once the player is dead, caught or escaped.</summary>
        public bool CanAct;
        public float RecentDamage;
        public float HealthFraction;
        public int EngagedEnemies;
        public float Seen;
        public float Noise;
        public float LootFraction;
        /// <summary>The glass alarm was raised and no guard could walk to it.</summary>
        public bool AlarmUnanswered;
        public EnemyCensus Census;
    }
}

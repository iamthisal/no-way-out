using System;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Enemy types the director may send in. Append only: values are serialized.</summary>
    public enum SpawnKind { Guard, Archer, Hound }

    /// <summary>What kind of place a spawn point is. Append only: values are serialized.</summary>
    [Flags]
    public enum SpawnZone { None = 0, Corridor = 1, OpenGallery = 2, Staff = 4 }

    /// <summary>Set of enemy types, used by spawn points to say who may enter there.</summary>
    [Flags]
    public enum SpawnKindSet { None = 0, Guard = 1, Archer = 2, Hound = 4, All = Guard | Archer | Hound }

    public static class SpawnKinds
    {
        public static readonly SpawnKind[] All = { SpawnKind.Guard, SpawnKind.Archer, SpawnKind.Hound };

        public static bool Contains(this SpawnKindSet set, SpawnKind kind) => (set & (SpawnKindSet)(1 << (int)kind)) != 0;
    }
}

using System.Collections.Generic;

namespace Lockdown.AI.Core.Director
{
    /// <summary>World questions the brain asks; the Unity agent answers with physics and NavMesh.</summary>
    public interface IDirectorWorld
    {
        /// <summary>Adds every spawn point with its current facts.</summary>
        void CollectCandidates(List<SpawnCandidate> results);
    }
}

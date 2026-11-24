using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    /// <summary>Walking distances on the baked NavMesh. Closed doors carve it, so they block routes.</summary>
    public sealed class MuseumRoutes
    {
        private readonly NavMeshPath path = new NavMeshPath();
        private readonly Vector3[] corners = new Vector3[256];

        /// <summary>Length of a complete route, or infinity when there is none.</summary>
        public float Length(Vector3 from, Vector3 to, float sampleRadius = 1.5f)
        {
            if (!NavMesh.SamplePosition(from, out var start, sampleRadius, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(to, out var end, sampleRadius, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete) return float.PositiveInfinity;
            int count = path.GetCornersNonAlloc(corners);
            float length = 0f;
            for (int i = 1; i < count; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }
    }
}

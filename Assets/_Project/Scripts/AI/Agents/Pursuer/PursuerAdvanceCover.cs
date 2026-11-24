using System;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Chooses a short forward move behind geometry. Never picks a retreat or a distant hiding place.</summary>
    public static class PursuerAdvanceCover
    {
        public static bool TryChoose(Vector3 guard, Vector3 threat, PursuerRouteMeasure route,
            Func<Vector3, Vector3, bool> screened, out Vector3 goal)
        {
            goal = guard;
            Vector3 forward = threat - guard; forward.y = 0f;
            float distance = forward.magnitude;
            if (distance < 3f) return false;
            forward.Normalize(); Vector3 side = Vector3.Cross(Vector3.up, forward);
            float best = float.NegativeInfinity;
            bool found = false;
            for (int step = 1; step <= 3; step++)
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 candidate = guard + forward * step + side * (sign * 1.5f);
                float advance = distance - Vector3.ProjectOnPlane(threat - candidate, Vector3.up).magnitude;
                if (advance < 0.3f || !route(guard, candidate, out float length) || length > 4.5f || !screened(candidate, threat)) continue;
                float score = advance - length * 0.25f;
                if (score <= best) continue;
                best = score; goal = candidate; found = true;
            }
            return found;
        }
    }
}

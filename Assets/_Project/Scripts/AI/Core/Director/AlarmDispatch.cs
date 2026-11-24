using System.Collections.Generic;

namespace Lockdown.AI.Core.Director
{
    /// <summary>Facts about one guard when an alarm goes off.</summary>
    public struct AlarmCandidate
    {
        /// <summary>Dead, arresting, fighting or chasing: must not be pulled away.</summary>
        public bool Busy;
        public bool Reachable;
        public float RouteLength;
    }

    /// <summary>Picks the single best guard to answer an alarm: free, able to walk there, shortest route.</summary>
    public static class AlarmDispatch
    {
        /// <returns>Index of the responder, or -1 when nobody can go.</returns>
        public static int Choose(IReadOnlyList<AlarmCandidate> guards)
        {
            int best = -1;
            for (int i = 0; i < guards.Count; i++)
                if (!guards[i].Busy && guards[i].Reachable && (best < 0 || guards[i].RouteLength < guards[best].RouteLength)) best = i;
            return best;
        }
    }
}

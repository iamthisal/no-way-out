using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.AI.Core.Hound
{
    // Edge cost is physical distance. No NavMesh path search is used here.
    public static class HoundAStar
    {
        public static bool FindPath(List<Vector3> points, List<List<int>> neighbours,
            int start, int goal, Func<int, int, bool> canTraverse,
            List<int> path, out float cost)
        {
            path.Clear();
            cost = float.PositiveInfinity;
            if (start < 0 || goal < 0 || start >= points.Count || goal >= points.Count
                || neighbours.Count != points.Count) return false;

            var open = new List<int> { start };
            var closed = new HashSet<int>();
            var previous = new Dictionary<int, int>();
            var distance = new Dictionary<int, float> { [start] = 0f };

            while (open.Count > 0)
            {
                int current = open[0];
                float bestScore = distance[current] + Vector3.Distance(points[current], points[goal]);
                for (int i = 1; i < open.Count; i++)
                {
                    int candidate = open[i];
                    // g = distance travelled; h = straight-line distance remaining; f = g + h.
                    float score = distance[candidate] + Vector3.Distance(points[candidate], points[goal]);
                    if (score < bestScore) { current = candidate; bestScore = score; }
                }

                if (current == goal)
                {
                    cost = distance[goal];
                    path.Add(current);
                    while (previous.TryGetValue(current, out int parent))
                    {
                        current = parent;
                        path.Add(current);
                    }
                    path.Reverse();
                    return true;
                }

                open.Remove(current);
                closed.Add(current);
                for (int i = 0; i < neighbours[current].Count; i++)
                {
                    int next = neighbours[current][i];
                    if (closed.Contains(next) || (canTraverse != null && !canTraverse(current, next))) continue;
                    float tentative = distance[current] + Vector3.Distance(points[current], points[next]);
                    if (distance.TryGetValue(next, out float known) && tentative >= known) continue;
                    previous[next] = current;
                    distance[next] = tentative;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            return false;
        }
    }
}

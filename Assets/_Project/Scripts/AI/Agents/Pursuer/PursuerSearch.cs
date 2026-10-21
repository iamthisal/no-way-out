using System;
using System.Collections.Generic;
using UnityEngine;
namespace Lockdown.AI.Agents
{
    public sealed class PursuerSearch
    {
        private Vector3 center;
        private float deadline, radius, duration, travelDeadline;
        private int index;
        private readonly List<Vector3> preferred = new List<Vector3>();
        private int preferredIndex;
        public bool HasReachedArea { get; private set; }
        public Vector3 Center => center;
        public void Begin(Vector3 position, float now, float duration, float searchRadius)
        {
            center = position; this.duration = duration; deadline = now + duration; radius = searchRadius;
            index = preferredIndex = 0; preferred.Clear(); HasReachedArea = true;
        }
        public void BeginApproach(IReadOnlyList<Vector3> areas, float now, float searchDuration, float searchRadius, float travelTimeout)
        {
            Begin(areas[0], now, searchDuration, searchRadius);
            preferred.AddRange(areas); HasReachedArea = false;
            travelDeadline = now + travelTimeout;
        }
        public void Arrived(Vector3 position, float now)
        {
            if (HasReachedArea) return;
            center = position; deadline = now + duration; HasReachedArea = true;
            // The area's center was just inspected; do not immediately select it again.
            index = 1;
        }
        public bool Expired(float now) => now >= (HasReachedArea ? deadline : travelDeadline);
        public bool TryNext(Func<Vector3, bool> reachable, out Vector3 position)
        {
            while (preferredIndex < preferred.Count)
            {
                position = preferred[preferredIndex++];
                if (reachable(position)) return true;
            }
            while (index < 17)
            {
                int current = index++;
                float angle = (current - 1) % 8 * Mathf.PI / 4f;
                float ring = current <= 8 ? 0.5f : 1f;
                position = current == 0 ? center : center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * ring;
                if (reachable(position)) return true;
            }
            position = center; return false;
        }
    }
}

using System;
using UnityEngine;
namespace Lockdown.AI.Agents
{
    public sealed class PursuerSearch
    {
        private Vector3 center;
        private float deadline, radius;
        private int index;
        public void Begin(Vector3 position, float now, float duration, float searchRadius)
        { center = position; deadline = now + duration; radius = searchRadius; index = 0; }
        public bool Expired(float now) => now >= deadline;
        public bool TryNext(Func<Vector3, bool> reachable, out Vector3 position)
        {
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

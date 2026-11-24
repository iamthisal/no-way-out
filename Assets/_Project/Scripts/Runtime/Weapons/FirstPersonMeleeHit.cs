using System;
using UnityEngine;

namespace Lockdown.Weapons
{
    internal static class FirstPersonMeleeHit
    {
        internal static bool TryFind(Transform camera, Transform owner, float reach, out RaycastHit target)
        {
            var hits = Physics.RaycastAll(camera.position, camera.forward, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(owner)) continue;
                target = hit;
                return true;
            }
            target = default;
            return false;
        }
    }
}

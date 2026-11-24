using Lockdown.Combat;
using Lockdown.World;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    /// <summary>Could the player see something standing at this point? Used so nothing appears or vanishes in view.</summary>
    public static class PlayerSight
    {
        private static readonly float[] BodyHeights = { 0.2f, 1f, 1.7f };
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private const float ViewMargin = 0.12f;

        /// <summary>Inside the camera frustum plus a margin, ignoring walls.</summary>
        public static bool InView(Camera camera, Vector3 feet)
        {
            foreach (float height in BodyHeights)
            {
                Vector3 view = camera.WorldToViewportPoint(feet + Vector3.up * height);
                if (view.z > 0f && view.x > -ViewMargin && view.x < 1f + ViewMargin
                    && view.y > -ViewMargin && view.y < 1f + ViewMargin) return true;
            }
            return false;
        }

        /// <summary>A straight unblocked line from the player's eyes, whichever way they face.</summary>
        public static bool HasLine(Vector3 eyes, Vector3 feet, Transform player, Transform self = null)
        {
            foreach (float height in BodyHeights)
                if (IsOpen(eyes, feet + Vector3.up * height, player, self)) return true;
            return false;
        }

        private static bool IsOpen(Vector3 from, Vector3 to, Transform player, Transform self)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance < 0.01f) return true;
            int count = Physics.RaycastNonAlloc(from, offset / distance, Hits, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i].transform;
                if (hit.IsChildOf(player) || (self != null && hit.IsChildOf(self))) continue;
                // Glass and other characters do not hide anything.
                if (hit.GetComponentInParent<BreakableGlass>() != null || hit.GetComponentInParent<Health>() != null) continue;
                return false;
            }
            return true;
        }
    }
}

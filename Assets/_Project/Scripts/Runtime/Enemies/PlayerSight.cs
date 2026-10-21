using Lockdown.Player;
using UnityEngine;

namespace Lockdown.Enemies
{
    /// <summary>Cone-and-line-of-sight checks against the player, shared by the guard and the security robots.</summary>
    public static class PlayerSight
    {
        // True when the player's head or chest is within range and inside the cone around forward, and the
        // first thing a line from the eye meets (other than the looker itself) is the player.
        public static bool Sees(Transform looker, Vector3 eye, Vector3 forward, float range, float fieldOfView,
            FirstPersonController player, out float distance)
        {
            distance = 0f;
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            var health = player.GetComponent<Lockdown.Combat.Health>();
            if (health == null || health.IsDead) return false;
            forward.y = 0f;
            Vector3 head = player.PlayerCamera != null ? player.PlayerCamera.transform.position : player.transform.position + Vector3.up * 1.6f;
            foreach (var point in new[] { head, player.transform.position + Vector3.up * 1.0f })
            {
                Vector3 direction = point - eye;
                if (direction.magnitude > range) continue;
                Vector3 flat = direction;
                flat.y = 0f;
                if (Vector3.Angle(forward, flat) > fieldOfView * 0.5f) continue;
                if (!ClearLine(looker, eye, direction, player)) continue;
                distance = direction.magnitude;
                return true;
            }
            return false;
        }

        private static bool ClearLine(Transform looker, Vector3 eye, Vector3 direction, FirstPersonController player)
        {
            float nearest = float.PositiveInfinity;
            Collider first = null;
            foreach (var hit in Physics.RaycastAll(eye, direction.normalized, direction.magnitude + 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(looker) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                first = hit.collider;
            }
            return first != null && first.GetComponentInParent<FirstPersonController>() == player;
        }
    }
}

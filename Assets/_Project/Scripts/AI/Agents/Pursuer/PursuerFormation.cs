using System;
using System.Collections.Generic;
using UnityEngine;
namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    public sealed class PursuerFormation : MonoBehaviour
    {
        private static readonly List<PursuerFormation> Members = new List<PursuerFormation>();
        private Transform target;
        public void SetTarget(Transform value) { target = value; }
        private void OnEnable() { if (!Members.Contains(this)) Members.Add(this); }
        private void OnDisable() { Members.Remove(this); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Members.Clear(); }
        public bool TryFlank(Vector3 playerPosition, Func<Vector3, bool> reachable, out Vector3 point)
        {
            point = playerPosition;
            if (!isActiveAndEnabled || target == null) return false;
            // Also recover registration when entering Play Mode without a scene reload.
            Members.RemoveAll(member => member == null);
            if (!Members.Contains(this)) Members.Add(this);
            int slot = 0;
            foreach (var member in Members)
            {
                if (member == this) break;
                if (member != null && member.isActiveAndEnabled && member.target == target
                    && (member.transform.position - playerPosition).sqrMagnitude < 400f) slot++;
            }
            point = playerPosition;
            if (slot == 0) return false;
            // Stable per-agent slots avoid all pursuers claiming the same approach.
            float angle = slot * 137.5f * Mathf.Deg2Rad;
            float radius = 2.5f + slot / 6 * 0.8f;
            Vector3 candidate = playerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!reachable(candidate)) return false;
            Vector3 direction = playerPosition - candidate;
            foreach (var hit in Physics.RaycastAll(candidate + Vector3.up * 1.2f, direction.normalized,
                direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && (target == null || !hit.transform.IsChildOf(target))) return false;
            point = candidate; return true;
        }
    }
}

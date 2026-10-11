using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PursuerAgent : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField] private Transform playerTarget;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2f;
        [SerializeField, Min(0.1f)] private float arrivalDistance = 0.5f;

        [Header("Player Detection")]
        [SerializeField, Min(0.1f)] private float detectionRange = 20f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 100f;
        [SerializeField, Min(0f)] private float eyeHeight = 1.2f;
        [SerializeField, Min(0f)] private float targetHeight = 1.2f;
        [SerializeField] private LayerMask sightLayers = Physics.DefaultRaycastLayers;

        private NavMeshAgent agent;
        private int patrolIndex;

        public bool IsPlayerVisible { get; private set; }

        private void Awake() { agent = GetComponent<NavMeshAgent>(); }

        private void Update()
        {
            // Detection does not require navigation and does not change patrol behavior yet.
            IsPlayerVisible = CanSeePlayer();

            // Navigation commands require an enabled agent placed on a baked NavMesh.
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

            Patrol();
        }

        private void Patrol()
        {
            if (patrolPoints.Length == 0) { agent.ResetPath(); return; }
            Transform point = patrolPoints[patrolIndex];
            if (point == null)
            {
                // Skip missing waypoints and wrap around to keep the route looping.
                agent.ResetPath();
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                return;
            }
            Vector3 offset = point.position - transform.position;
            // Check ground-plane distance so waypoint height does not prevent arrival.
            offset.y = 0f;
            // Wait for path calculation and respect the agent's stopping distance.
            if (!agent.pathPending && offset.magnitude <= Mathf.Max(arrivalDistance, agent.stoppingDistance))
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                return;
            }
            agent.speed = patrolSpeed;
            agent.isStopped = false;
            // Request a path only when needed or when the waypoint moves more than 0.2 units.
            if (!agent.hasPath || (agent.destination - point.position).sqrMagnitude > 0.04f)
                agent.SetDestination(point.position);
        }

        private bool CanSeePlayer()
        {
            if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy) return false;

            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 direction = playerTarget.position + Vector3.up * targetHeight - origin;
            if (direction.sqrMagnitude > detectionRange * detectionRange
                || Vector3.Angle(transform.forward, direction) > fieldOfView * 0.5f) return false;

            // Ignore both characters' colliders; any other solid hit blocks visibility.
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
                sightLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(playerTarget)) continue;
                return false;
            }
            return true;
        }

        private void OnDisable()
        {
            IsPlayerVisible = false;
            // Clear the route so disabling this controller does not leave the agent moving.
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
        }
    }
}

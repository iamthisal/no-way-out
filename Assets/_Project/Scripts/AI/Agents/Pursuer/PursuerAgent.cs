using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PursuerAgent : MonoBehaviour
    {
        public enum PursuerState { Patrol, Chase, Search, Return }

        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private Transform[] patrolPoints = new Transform[0];

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 4f;
        [SerializeField, Min(0.1f)] private float arrivalDistance = 0.5f;

        [Header("Perception")]
        [SerializeField, Min(0.1f)] private float detectionRange = 20f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 100f;
        [SerializeField, Min(0f)] private float eyeHeight = 1.2f;
        [SerializeField, Min(0f)] private float targetHeight = 1.2f;
        [SerializeField] private LayerMask sightLayers = Physics.DefaultRaycastLayers;

        [Header("Memory")]
        [SerializeField, Min(0f)] private float loseSightDelay = 2f;
        [SerializeField, Min(0f)] private float searchDuration = 14f;

        private NavMeshAgent agent;
        private int patrolIndex;
        private float timeWithoutSight;
        private float searchTimeLeft;

        public PursuerState State { get; private set; } = PursuerState.Patrol;
        public Vector3 LastKnownPosition { get; private set; }

        private void Awake() { agent = GetComponent<NavMeshAgent>(); }

        private void Update()
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

            if (CanSeeTarget())
            {
                LastKnownPosition = target.position;
                timeWithoutSight = 0f;
                State = PursuerState.Chase;
            }
            else if (State == PursuerState.Chase)
            {
                timeWithoutSight += Time.deltaTime;
                if (timeWithoutSight >= loseSightDelay)
                {
                    State = PursuerState.Search;
                    searchTimeLeft = searchDuration;
                }
            }

            switch (State)
            {
                case PursuerState.Patrol:
                case PursuerState.Return:
                    Patrol();
                    break;
                case PursuerState.Chase:
                    MoveTo(LastKnownPosition, chaseSpeed);
                    break;
                case PursuerState.Search:
                    // Initial search visits the last seen location; local search points come later.
                    MoveTo(LastKnownPosition, patrolSpeed);
                    searchTimeLeft -= Time.deltaTime;
                    if (searchTimeLeft <= 0f) State = PursuerState.Return;
                    break;
            }
        }

        private void Patrol()
        {
            if (patrolPoints.Length == 0) { agent.ResetPath(); return; }
            Transform point = patrolPoints[patrolIndex];
            if (point == null)
            {
                agent.ResetPath();
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                return;
            }
            Vector3 offset = point.position - transform.position;
            offset.y = 0f;
            if (!agent.pathPending && offset.magnitude <= Mathf.Max(arrivalDistance, agent.stoppingDistance))
            {
                State = PursuerState.Patrol;
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                return;
            }
            MoveTo(point.position, patrolSpeed);
        }

        private void MoveTo(Vector3 destination, float speed)
        {
            agent.speed = speed;
            agent.isStopped = false;
            if (!agent.hasPath || (agent.destination - destination).sqrMagnitude > 0.04f)
                agent.SetDestination(destination);
        }

        private bool CanSeeTarget()
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 direction = target.position + Vector3.up * targetHeight - origin;
            if (direction.sqrMagnitude > detectionRange * detectionRange
                || Vector3.Angle(transform.forward, direction) > fieldOfView * 0.5f) return false;

            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude,
                sightLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }

        private void OnDisable()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
        }
    }
}

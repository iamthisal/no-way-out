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

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2f;
        [SerializeField, Min(0.1f)] private float arrivalDistance = 0.5f;

        private NavMeshAgent agent;
        private int patrolIndex;

        private void Awake() { agent = GetComponent<NavMeshAgent>(); }

        private void Update()
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

            Patrol();
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
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                return;
            }
            agent.speed = patrolSpeed;
            agent.isStopped = false;
            if (!agent.hasPath || (agent.destination - point.position).sqrMagnitude > 0.04f)
                agent.SetDestination(point.position);
        }

        private void OnDisable()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
        }
    }
}

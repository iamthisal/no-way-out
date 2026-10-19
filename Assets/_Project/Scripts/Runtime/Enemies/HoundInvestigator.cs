using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HoundInvestigator : MonoBehaviour
    {
        private NavMeshAgent agent;
        private Vector3 clue;
        private bool hasClue;
        private float nextAttempt;
        public Vector3 LastNoisePosition => clue;
        public bool HasClue => hasClue;
        public bool HasArrived { get; private set; }

        private void Awake() { agent = GetComponent<NavMeshAgent>(); }

        public void Investigate(Vector3 position)
        {
            clue = position;
            hasClue = true;
            HasArrived = false;
            nextAttempt = 0f;
            TryMove();
        }

        private void Update()
        {
            if (!hasClue || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= agent.stoppingDistance + 0.12f)
            {
                agent.ResetPath();
                HasArrived = true;
            }
            // Retry if a door was closed or a carved obstacle changed the route.
            if (!HasArrived && !agent.pathPending && Time.time >= nextAttempt) TryMove();
        }

        private void TryMove()
        {
            nextAttempt = Time.time + 1f;
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            if (!TryFindApproach(transform.position, clue, agent.agentTypeID, agent.areaMask, out var target)) return;
            agent.isStopped = false;
            agent.SetDestination(target);
        }

        // Glass is often inside a display pedestal, outside the walkable mesh.
        // Pick the nearest reachable approach rather than walking through the case.
        public static bool TryFindApproach(Vector3 origin, Vector3 noise, int agentType, int areaMask, out Vector3 target)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = areaMask };
            var path = new NavMeshPath();
            target = default;
            float best = float.PositiveInfinity;
            for (int ring = 0; ring <= 4; ring++)
            {
                int samples = ring == 0 ? 1 : 12;
                for (int i = 0; i < samples; i++)
                {
                    float angle = i * Mathf.PI * 2f / samples;
                    var candidate = noise + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * 0.6f);
                    candidate.y = origin.y;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.45f, filter)) continue;
                    if (Mathf.Abs(hit.position.y - origin.y) > 0.8f) continue;
                    if (!NavMesh.CalculatePath(origin, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    float score = (hit.position - noise).sqrMagnitude;
                    if (score >= best) continue;
                    best = score;
                    target = hit.position;
                }
            }
            return !float.IsPositiveInfinity(best);
        }

        private void OnDisable()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
        }
    }
}

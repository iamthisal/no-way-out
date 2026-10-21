using Lockdown.Combat;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Scripting.APIUpdating;

namespace Lockdown.Agents.Pursuer
{
    public enum PursuerRouteStatus { Unavailable, Idle, Moving, Arrived, Blocked }
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [MovedFrom(true, "Lockdown.AI.Agents", "Lockdown.AI.Agents", "PursuerNavigation")]
    public sealed class PursuerNavigation : MonoBehaviour
    {
        [SerializeField] private LayerMask solidMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.1f)] private float sampleRadius = 0.65f;
        private NavMeshAgent agent;
        private NavMeshPath queryPath;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private bool travelling;
        private bool failed;
        private Vector3 destination;
        private Vector3 progressPosition;
        private Vector3 safePosition;
        private double lastProgressAt;
        private double nextRepath;
        public float StuckTimeout { get; set; } = 2.5f;
        public bool IsReady => agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
        public Vector3 Velocity => IsReady ? agent.velocity : Vector3.zero;
        public Vector3 Destination => destination;
        public bool IsTravelling => travelling;

        private NavMeshQueryFilter Filter => new NavMeshQueryFilter
            { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            queryPath = new NavMeshPath();
            safePosition = transform.position;
        }

        private void LateUpdate()
        {
            if (!IsReady || Time.deltaTime <= 0f) return;
            // NavMesh movement runs before Update. Sweep the movement already taken as well as
            // looking ahead, so a long frame cannot tunnel into a newly closed physical door.
            if (SegmentBlocked(safePosition, transform.position, true))
            {
                agent.Warp(safePosition);
                FailRoute();
            }
            safePosition = transform.position;
        }

        public bool BindToNavMesh()
        {
            if (IsReady) return true;
            if (agent == null || !agent.isActiveAndEnabled) return false;
            if (!NavMesh.SamplePosition(transform.position, out var hit, sampleRadius, Filter)
                || Mathf.Abs(hit.position.y - transform.position.y) > sampleRadius || !agent.Warp(hit.position)) return false;
            safePosition = transform.position;
            return true;
        }

        public bool CanReach(Vector3 point) => TryPath(point, out _);

        private bool TryPath(Vector3 point, out Vector3 snapped)
        {
            snapped = point;
            if (!IsReady || !IsFinite(point.x) || !IsFinite(point.y) || !IsFinite(point.z)) return false;
            if (!NavMesh.SamplePosition(point, out var hit, sampleRadius, Filter)
                || Mathf.Abs(hit.position.y - point.y) > sampleRadius) return false;
            // Sampling alone can select the wrong side of a thin wall. Check the short connection too.
            if (SegmentBlocked(point + Vector3.up * 0.45f, hit.position + Vector3.up * 0.45f, false)) return false;
            snapped = hit.position;
            return agent.CalculatePath(snapped, queryPath) && queryPath.status == NavMeshPathStatus.PathComplete;
        }

        public bool MoveTo(Vector3 point, float speed)
        {
            if (travelling && !failed && (point - destination).sqrMagnitude < 0.0625f) { agent.speed = speed; return true; }
            if (!TryPath(point, out var snapped) || !agent.SetPath(queryPath))
            {
                Stop();
                failed = true;
                return false;
            }
            destination = snapped;
            agent.speed = Mathf.Max(0.1f, speed);
            agent.isStopped = false;
            travelling = true;
            failed = false;
            progressPosition = transform.position;
            lastProgressAt = Time.timeAsDouble;
            nextRepath = Time.timeAsDouble + 0.5;
            return true;
        }

        public PursuerRouteStatus Status()
        {
            if (!IsReady) return PursuerRouteStatus.Unavailable;
            if (failed) return PursuerRouteStatus.Blocked;
            if (!travelling) return PursuerRouteStatus.Idle;
            if (agent.pathPending) return PursuerRouteStatus.Moving;
            Vector3 remaining = destination - transform.position;
            remaining.y = 0;
            if (remaining.magnitude <= agent.stoppingDistance + 0.15f
                && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f))
            {
                Stop();
                return PursuerRouteStatus.Arrived;
            }
            if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete) return FailRoute();
            if ((transform.position - progressPosition).sqrMagnitude > 0.01f)
            {
                progressPosition = transform.position;
                lastProgressAt = Time.timeAsDouble;
            }
            if (Time.timeAsDouble - lastProgressAt > StuckTimeout) return FailRoute();

            // A closed non-carving door still has a collider. Never walk through it while waiting for a rebake.
            Vector3 direction = agent.steeringTarget - transform.position;
            direction.y = 0;
            float distance = Mathf.Min(direction.magnitude, Mathf.Max(0.25f, agent.speed * Time.deltaTime + 0.1f));
            if (distance > 0.01f && SegmentBlocked(transform.position,
                transform.position + direction.normalized * distance, true)) return FailRoute();
            if (agent.isPathStale && Time.timeAsDouble >= nextRepath)
            {
                nextRepath = Time.timeAsDouble + 0.5;
                if (!TryPath(destination, out _) || !agent.SetPath(queryPath)) return FailRoute();
            }
            return PursuerRouteStatus.Moving;
        }

        private bool SegmentBlocked(Vector3 from, Vector3 to, bool bodyCast)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return false;
            int count;
            if (bodyCast)
            {
                float radius = Mathf.Min(agent.radius * 0.85f, 0.35f);
                Vector3 bottom = from + Vector3.up * (radius + 0.08f);
                Vector3 top = from + Vector3.up * Mathf.Max(radius + 0.08f, agent.height - radius);
                count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction.normalized, hits,
                    direction.magnitude, solidMask, QueryTriggerInteraction.Ignore);
            }
            else count = Physics.RaycastNonAlloc(from, direction.normalized, hits, direction.magnitude,
                solidMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return true;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].transform.IsChildOf(transform)) continue;
                // Other characters are handled by local avoidance and the close-range attack checks.
                if (hits[i].collider.GetComponentInParent<Health>() != null) continue;
                return true;
            }
            return false;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private PursuerRouteStatus FailRoute() { Stop(); failed = true; return PursuerRouteStatus.Blocked; }

        public void Stop()
        {
            if (IsReady) { agent.ResetPath(); agent.isStopped = true; }
            travelling = false;
            failed = false;
        }

        private void OnDisable() { Stop(); }
        private void OnEnable() { safePosition = transform.position; }
    }
}

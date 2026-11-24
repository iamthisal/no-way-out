using Lockdown.AI.Core.Hound;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HoundNavigation : MonoBehaviour
    {
        [SerializeField] private LayerMask solidMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.1f)] private float sampleRadius = 0.65f;
        [SerializeField, Range(0.25f, 1f)] private float waypointSpacing = 0.5f;
        [Header("A* runtime information")]
        [SerializeField] private List<Vector3> route = new List<Vector3>();
        [SerializeField] private float routeCost;
        [SerializeField] private int waypointIndex;
        private HoundNavGraph graph;
        private readonly List<Vector3> candidateRoute = new List<Vector3>();
        private readonly Collider[] overlaps = new Collider[32];
        private float finalStoppingDistance;
        private bool needsRepath;
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
        public IReadOnlyList<Vector3> Route => route;
        public float RouteCost => routeCost;

        private NavMeshQueryFilter Filter => new NavMeshQueryFilter
            { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            queryPath = new NavMeshPath();
            finalStoppingDistance = agent.stoppingDistance;
            agent.autoRepath = false; // Only our A* may choose a replacement route.
            safePosition = transform.position;
        }

        private void Update()
        {
            if (!IsReady || Time.deltaTime <= 0f) return;
            // NavMesh movement runs before Update. Correct it before the brain/combat Update,
            // not in LateUpdate, so other gameplay cannot observe a step through a closed door.
            if (SegmentBlocked(safePosition, transform.position, true))
            {
                agent.Warp(safePosition);
                agent.ResetPath();
                agent.isStopped = true;
                needsRepath = true;
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

        public bool CanReach(Vector3 point) => TryPath(point, out _, out _);

        private bool TryPath(Vector3 point, out Vector3 snapped, out float cost)
        {
            snapped = point;
            cost = float.PositiveInfinity;
            if (!IsReady || !HoundSound.IsFinite(point.x) || !HoundSound.IsFinite(point.y)
                || !HoundSound.IsFinite(point.z)) return false;
            // Noises can originate at hand/display height. This museum has one walkable storey.
            Vector3 groundPoint = new Vector3(point.x, transform.position.y, point.z);
            if (graph == null)
            {
                graph = new HoundNavGraph(waypointSpacing);
                if (!graph.Build(transform.position.y, Filter)) { graph = null; return false; }
            }
            // A smashed vase can remain above a solid pedestal. Investigate a nearby accessible
            // approach, not the inside of that pedestal. Prefer candidates nearest the sound.
            var approaches = new List<Vector3>();
            for (int ring = 0; ring <= 3; ring++)
                for (int i = 0; i < (ring == 0 ? 1 : 12); i++)
                {
                    float angle = i * Mathf.PI / 6f;
                    Vector3 probe = groundPoint + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * 0.6f);
                    if (!NavMesh.SamplePosition(probe, out var hit, sampleRadius, Filter)
                        || Mathf.Abs(hit.position.y - groundPoint.y) > 0.25f) continue;
                    // Check at the sound's height so the base of a display does not hide its top.
                    float height = Mathf.Max(groundPoint.y + 0.45f, point.y);
                    Vector3 from = new Vector3(point.x, height, point.z);
                    Vector3 to = new Vector3(hit.position.x, height, hit.position.z);
                    if (SegmentBlocked(from, to, false) || !EdgeClear(hit.position, hit.position)) continue;
                    if (!approaches.Contains(hit.position)) approaches.Add(hit.position);
                }
            approaches.Sort((a, b) => (a - groundPoint).sqrMagnitude.CompareTo((b - groundPoint).sqrMagnitude));
            for (int i = 0; i < approaches.Count; i++)
                if (graph.FindPath(transform.position, approaches[i], EdgeClear, candidateRoute, out cost))
                { snapped = approaches[i]; return true; }
            return false;
        }

        public bool MoveTo(Vector3 point, float speed)
        {
            if (!TryPath(point, out var snapped, out float cost))
            {
                Stop();
                failed = true;
                return false;
            }
            destination = snapped;
            route.Clear();
            route.AddRange(candidateRoute);
            routeCost = cost;
            waypointIndex = 1;
            agent.speed = Mathf.Max(0.1f, speed);
            travelling = true;
            failed = false;
            needsRepath = false;
            progressPosition = transform.position;
            lastProgressAt = Time.timeAsDouble;
            nextRepath = Time.timeAsDouble + 0.5;
            if (!FollowWaypoint()) { FailRoute(); return false; }
            return true;
        }

        private bool FollowWaypoint()
        {
            if (waypointIndex >= route.Count) return false;
            Vector3 next = route[waypointIndex];
            if (!EdgeClear(transform.position, next)) return false;
            // Unity handles locomotion for ONE verified straight edge, not the overall route.
            if (!agent.CalculatePath(next, queryPath) || queryPath.status != NavMeshPathStatus.PathComplete) return false;
            Vector3[] corners = queryPath.corners;
            for (int i = 1; i < corners.Length; i++)
                if (!EdgeClear(corners[i - 1], corners[i])) return false;
            agent.stoppingDistance = waypointIndex == route.Count - 1 ? finalStoppingDistance : 0.05f;
            if (!agent.SetPath(queryPath)) return false;
            agent.isStopped = false;
            return true;
        }

        private bool EdgeClear(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(to, out var hit, 0.06f, Filter)
                || Vector3.Distance(hit.position, to) > 0.06f) return false;
            if (NavMesh.Raycast(from, to, out _, Filter)) return false;
            float radius = Mathf.Max(0.05f, agent.radius);
            Vector3 bottom = to + Vector3.up * (radius + 0.05f);
            Vector3 top = to + Vector3.up * Mathf.Max(radius + 0.05f, agent.height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps, solidMask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!IgnoreBody(overlaps[i])) return false;
            return !SegmentBlocked(from, to, true);
        }

        private bool IgnoreBody(Collider body)
        {
            // A destructible display may have Health: it is still a solid obstacle until broken.
            return body.transform.IsChildOf(transform)
                || body.GetComponentInParent<CharacterController>() != null
                || body.GetComponentInParent<NavMeshAgent>() != null;
        }

        private HoundRouteStatus Replan()
        {
            agent.isStopped = true;
            needsRepath = true;
            if (Time.timeAsDouble < nextRepath) return HoundRouteStatus.Moving;
            if (agent.isPathStale) graph = null;
            return MoveTo(destination, agent.speed) ? HoundRouteStatus.Moving : FailRoute();
        }

        public HoundRouteStatus Status()
        {
            if (!IsReady) return HoundRouteStatus.Unavailable;
            if (failed) return HoundRouteStatus.Blocked;
            if (!travelling) return HoundRouteStatus.Idle;
            if (needsRepath || agent.isPathStale) return Replan();
            if (agent.pathPending) return HoundRouteStatus.Moving;
            Vector3 remaining = route[waypointIndex] - transform.position;
            remaining.y = 0;
            if (remaining.magnitude <= agent.stoppingDistance + 0.15f
                && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f))
            {
                if (waypointIndex == route.Count - 1)
                {
                    Stop();
                    return HoundRouteStatus.Arrived;
                }
                waypointIndex++;
                if (!FollowWaypoint()) return Replan();
            }
            if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete) return Replan();
            if ((transform.position - progressPosition).sqrMagnitude > 0.01f)
            {
                progressPosition = transform.position;
                lastProgressAt = Time.timeAsDouble;
            }
            if (Time.timeAsDouble - lastProgressAt > StuckTimeout) return Replan();

            // A closed non-carving door still has a collider. Never walk through it while waiting for a rebake.
            Vector3 direction = agent.steeringTarget - transform.position;
            direction.y = 0;
            float distance = Mathf.Min(direction.magnitude, Mathf.Max(0.25f, agent.speed * Time.deltaTime + 0.1f));
            if (distance > 0.01f && SegmentBlocked(transform.position,
                transform.position + direction.normalized * distance, true)) return Replan();
            return HoundRouteStatus.Moving;
        }

        private bool SegmentBlocked(Vector3 from, Vector3 to, bool bodyCast)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 0.0001f) return false;
            int count;
            if (bodyCast)
            {
                float radius = Mathf.Max(0.05f, agent.radius);
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
                if (IgnoreBody(hits[i].collider)) continue;
                return true;
            }
            return false;
        }

        private HoundRouteStatus FailRoute() { Stop(); failed = true; return HoundRouteStatus.Blocked; }

        public void Stop()
        {
            if (IsReady) { agent.ResetPath(); agent.isStopped = true; agent.stoppingDistance = finalStoppingDistance; }
            travelling = false;
            failed = false;
            needsRepath = false;
        }

        [ContextMenu("Rebuild Hound Waypoint Graph")]
        public void RebuildGraph() { graph = null; if (travelling) needsRepath = true; }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || route == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < route.Count; i++)
            {
                Vector3 point = route[i] + Vector3.up * 0.15f;
                Gizmos.DrawSphere(point, 0.06f);
                if (i > 0) Gizmos.DrawLine(route[i - 1] + Vector3.up * 0.15f, point);
            }
        }

        private void OnDisable() { Stop(); }
        private void OnEnable() { safePosition = transform.position; graph = null; }
    }
}

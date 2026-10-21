using Lockdown.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class WanderingEnemy : MonoBehaviour
    {
        [Header("Wandering")]
        [SerializeField, Min(1f)] private float wanderRadius = 3f;
        [SerializeField, Min(0f)] private float pauseDuration = 1.3f;
        [Header("Patrol")]
        // Stops walked in order, looping. Leave empty to wander around the spawn point instead.
        [SerializeField] private Vector3[] patrolRoute = new Vector3[0];
        [SerializeField, Min(0f)] private float patrolPause = 2.5f;
        [SerializeField, Range(0f, 90f)] private float lookAroundAngle = 50f;
        [SerializeField, Min(0.1f)] private float pursuitSpeed = 3f;
        [SerializeField, Min(0f)] private float pursuitStopDistance = 2f;
        [Header("Placeholder Model")]
        [SerializeField] private Transform model;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [SerializeField] private Renderer bodyRenderer;

        private NavMeshAgent agent;
        private Health health;
        private Vector3 origin;
        private float nextMoveTime;
        private float travelDeadline;
        private float walkPhase;
        private float hitFlashEndsAt;
        private float deathProgress;
        private bool travelling;
        private MaterialPropertyBlock bodyProperties;
        private Color bodyColor;
        private int nextStop;
        private bool pursuing;
        private float patrolSpeed;
        private float patrolStopDistance;
        private float pauseYaw;
        private float pauseStarted;

        public Vector3 WanderOrigin => origin;
        public float WanderRadius => wanderRadius;
        public bool IsPatrolling => patrolRoute.Length > 1;
        public bool IsPursuing => pursuing;
        public int StopCount => patrolRoute.Length;
        public int NextStop => nextStop;
        public Vector3 Stop(int index) => patrolRoute[index];

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            bodyProperties = new MaterialPropertyBlock();
            if (bodyRenderer != null) bodyColor = bodyRenderer.sharedMaterial.color;
        }

        private void OnEnable()
        {
            health.OnDamaged.AddListener(FlashHit);
            health.OnDeath.AddListener(Die);
        }

        private void Start()
        {
            origin = transform.position;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(origin, out var spawn, 1f, filter) || !agent.Warp(spawn.position))
            {
                Debug.LogWarning("WanderingEnemy needs a baked NavMesh near its spawn.", this);
                enabled = false;
                return;
            }
            origin = spawn.position;
            patrolSpeed = agent.speed;
            patrolStopDistance = agent.stoppingDistance;
            if (IsPatrolling) nextStop = NearestStop();
            nextMoveTime = Time.time + pauseDuration;
        }

        private void OnDisable()
        {
            health.OnDamaged.RemoveListener(FlashHit);
            health.OnDeath.RemoveListener(Die);
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }
        }

        private void Update()
        {
            if (health.IsDead)
            {
                deathProgress = Mathf.MoveTowards(deathProgress, 1f, Time.deltaTime * 3f);
                if (model != null)
                {
                    model.localRotation = Quaternion.Euler(deathProgress * 90f, 0f, 0f);
                    model.localPosition = Vector3.up * (deathProgress * 0.2f);
                }
                return;
            }
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            if (!pursuing)
            {
                if (travelling && !agent.pathPending &&
                    (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f || Time.time >= travelDeadline))
                {
                    agent.ResetPath();
                    travelling = false;
                    nextMoveTime = Time.time + (IsPatrolling ? patrolPause : pauseDuration);
                    if (IsPatrolling)
                    {
                        nextStop = (nextStop + 1) % patrolRoute.Length;
                        pauseYaw = transform.eulerAngles.y;
                        pauseStarted = Time.time;
                    }
                }
                if (!travelling && Time.time >= nextMoveTime) ChooseDestination();
                else if (!travelling && IsPatrolling) LookAround();
            }
            AnimateWalk();
            if (bodyRenderer != null)
            {
                if (bodyProperties == null) bodyProperties = new MaterialPropertyBlock();
                bodyProperties.SetColor("_BaseColor", Time.time < hitFlashEndsAt ? Color.white : bodyColor);
                bodyRenderer.SetPropertyBlock(bodyProperties);
            }
        }

        // Another component, such as the guard's senses, takes over the legs: walk fast toward a point.
        public void Pursue(Vector3 point)
        {
            if (health.IsDead || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.SamplePosition(point, out var hit, 1.5f, filter)) return;
            pursuing = true;
            travelling = false;
            agent.speed = pursuitSpeed;
            agent.stoppingDistance = pursuitStopDistance;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }

        public void ResumePatrol()
        {
            if (!pursuing) return;
            pursuing = false;
            agent.speed = patrolSpeed;
            agent.stoppingDistance = patrolStopDistance;
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            travelling = false;
            if (IsPatrolling) nextStop = NearestStop();
            nextMoveTime = Time.time + pauseDuration;
        }

        private int NearestStop()
        {
            int nearest = 0;
            for (int i = 1; i < patrolRoute.Length; i++)
                if ((patrolRoute[i] - transform.position).sqrMagnitude < (patrolRoute[nearest] - transform.position).sqrMagnitude) nearest = i;
            return nearest;
        }

        // Sweep the gaze left and right while paused at a stop.
        private void LookAround()
        {
            float turn = (Time.time - pauseStarted) / Mathf.Max(0.1f, patrolPause);
            transform.rotation = Quaternion.Euler(0f, pauseYaw + Mathf.Sin(turn * Mathf.PI * 2f) * lookAroundAngle, 0f);
        }

        private void ChooseDestination()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
            if (IsPatrolling)
            {
                agent.isStopped = false;
                if (agent.CalculatePath(patrolRoute[nextStop], path) && path.status == NavMeshPathStatus.PathComplete && agent.SetPath(path))
                {
                    travelling = true;
                    travelDeadline = Time.time + 10f + 2f * PathLength(path) / Mathf.Max(0.1f, agent.speed);
                    return;
                }
                // Skip a stop that cannot be reached, such as one behind a door someone closed.
                nextStop = (nextStop + 1) % patrolRoute.Length;
                nextMoveTime = Time.time + pauseDuration;
                return;
            }
            for (int i = 0; i < 16; i++)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.6f, filter)) continue;
                if (Vector3.Distance(hit.position, origin) > wanderRadius || Vector3.Distance(hit.position, transform.position) < 0.8f) continue;
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                agent.isStopped = false;
                if (!agent.SetPath(path)) continue;
                travelling = true;
                travelDeadline = Time.time + 10f;
                return;
            }
            nextMoveTime = Time.time + pauseDuration + 0.5f;
        }

        private static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        private void AnimateWalk()
        {
            float speed = agent.velocity.magnitude;
            walkPhase += Time.deltaTime * speed * 7f;
            float stride = agent.speed > 0f ? Mathf.Clamp01(speed / agent.speed) : 0f;
            float swing = Mathf.Sin(walkPhase) * stride * 24f;
            if (leftArm != null) leftArm.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if (rightArm != null) rightArm.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
        }

        private void FlashHit() { hitFlashEndsAt = Time.time + 0.12f; }

        private void Die()
        {
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            if (bodyRenderer != null)
            {
                if (bodyProperties == null) bodyProperties = new MaterialPropertyBlock();
                bodyProperties.SetColor("_BaseColor", bodyColor * 0.45f);
                bodyRenderer.SetPropertyBlock(bodyProperties);
            }
        }
    }
}

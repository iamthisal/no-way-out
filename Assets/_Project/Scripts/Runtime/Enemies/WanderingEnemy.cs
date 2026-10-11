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

        public Vector3 WanderOrigin => origin;
        public float WanderRadius => wanderRadius;

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
            if (travelling && !agent.pathPending &&
                (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.1f || Time.time >= travelDeadline))
            {
                agent.ResetPath();
                travelling = false;
                nextMoveTime = Time.time + pauseDuration;
            }
            if (!travelling && Time.time >= nextMoveTime) ChooseDestination();
            AnimateWalk();
            if (bodyRenderer != null)
            {
                if (bodyProperties == null) bodyProperties = new MaterialPropertyBlock();
                bodyProperties.SetColor("_BaseColor", Time.time < hitFlashEndsAt ? Color.white : bodyColor);
                bodyRenderer.SetPropertyBlock(bodyProperties);
            }
        }

        private void ChooseDestination()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            var path = new NavMeshPath();
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

        private void AnimateWalk()
        {
            float speed = agent.velocity.magnitude;
            walkPhase += Time.deltaTime * speed * 7f;
            float swing = Mathf.Sin(walkPhase) * Mathf.Clamp01(speed / agent.speed) * 24f;
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

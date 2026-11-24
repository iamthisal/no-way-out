using Lockdown.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Enemies
{
    /// <summary>Drives the hound's Animator from its actual NavMeshAgent velocity.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HoundLocomotion : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("Ground speed (m/s) at which the fastest clip's paws don't slide.")]
        [SerializeField] private float topClipSpeed = 0.53f;   // walk clip; raise when a run clip is added
        [SerializeField] private float maxPlaybackBoost = 2f;  // cap so the cycle never looks frantic

        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int AnimSpeed = Animator.StringToHash("AnimSpeed");
        private static readonly int Death = Animator.StringToHash("Death");
        private NavMeshAgent agent;
        private Health health;
        private bool dead;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();   // the presentation-only dog has none
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;   // the NavMeshAgent moves the body
        }

        private void OnEnable() { if (health != null) health.OnDeath.AddListener(Die); }

        private void Start() { if (health != null && health.IsDead) Die(); }

        private void Die()
        {
            if (dead || animator == null) return;
            dead = true;
            animator.SetFloat(Speed, 0f);
            animator.SetFloat(AnimSpeed, 1f);
            animator.SetTrigger(Death);
        }

        private void Update()
        {
            if (animator == null || dead) return;
            float speed = agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;

            animator.SetFloat(Speed, speed, 0.1f, Time.deltaTime);    // damped → smooth idle↔walk blend

            // Above the fastest clip, speed the cycle up (capped) to reduce foot sliding.
            float boost = speed > topClipSpeed ? Mathf.Min(speed / topClipSpeed, maxPlaybackBoost) : 1f;
            animator.SetFloat(AnimSpeed, boost, 0.1f, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (health != null) health.OnDeath.RemoveListener(Die);
            if (animator != null && animator.isInitialized) animator.SetFloat(Speed, 0f);
        }
    }
}
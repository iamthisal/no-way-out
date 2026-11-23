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
        private NavMeshAgent agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;   // the NavMeshAgent moves the body
        }

        private void Update()
        {
            if (animator == null) return;
            float speed = agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;

            animator.SetFloat(Speed, speed, 0.1f, Time.deltaTime);    // damped → smooth idle↔walk blend

            // Above the fastest clip, speed the cycle up (capped) to reduce foot sliding.
            float boost = speed > topClipSpeed ? Mathf.Min(speed / topClipSpeed, maxPlaybackBoost) : 1f;
            animator.SetFloat(AnimSpeed, boost, 0.1f, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (animator != null && animator.isInitialized) animator.SetFloat(Speed, 0f);
        }
    }
}
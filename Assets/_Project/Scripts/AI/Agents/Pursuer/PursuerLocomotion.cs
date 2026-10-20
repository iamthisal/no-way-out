using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    /// <summary>Drives the imported character's in-place animation from actual navigation velocity.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PursuerLocomotion : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private NavMeshAgent agent;
        private static readonly int Speed = Animator.StringToHash("Speed");

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.applyRootMotion = false;
        }

        private void Update()
        {
            if (animator == null) return;
            float speed = agent.isActiveAndEnabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
            animator.SetFloat(Speed, speed, 0.12f, Time.deltaTime);
        }

        private void OnDisable()
        {
            if (animator != null && animator.isInitialized) animator.SetFloat(Speed, 0f);
        }
    }
}

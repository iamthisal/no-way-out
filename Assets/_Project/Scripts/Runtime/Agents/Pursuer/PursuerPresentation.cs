using Lockdown.Combat;
using Lockdown.Enemies;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
namespace Lockdown.Agents.Pursuer
{
    [DisallowMultipleComponent]
    [MovedFrom(true, "Lockdown.AI.Agents", "Lockdown.AI.Agents", "PursuerPresentation")]
    public sealed class PursuerPresentation : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AudioClip warningSound;
        private Health health;
        private EnemyShooting weapon;
        private AudioSource audioSource;
        private int shots;
        private bool warning;
        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            health = GetComponent<Health>(); weapon = GetComponent<EnemyShooting>();
            audioSource = GetComponent<AudioSource>();
        }
        private void OnEnable()
        {
            if (health == null) return;
            health.OnDamaged.AddListener(Hit); health.OnDeath.AddListener(Death);
        }
        private void OnDisable()
        {
            if (health == null) return;
            health.OnDamaged.RemoveListener(Hit); health.OnDeath.RemoveListener(Death);
        }
        private void Update()
        {
            if (weapon == null || animator == null || health.IsDead) return;
            if (weapon.ShotsFired != shots) { shots = weapon.ShotsFired; animator.SetTrigger("Shot"); }
        }
        public void SetWarning(bool value)
        {
            if (value && !warning && audioSource != null && warningSound != null) audioSource.PlayOneShot(warningSound);
            warning = value;
        }
        private void Hit() { if (animator != null && !health.IsDead) animator.SetTrigger("Hit"); }
        private void Death()
        {
            if (animator == null) return;
            animator.SetFloat("Speed", 0f); animator.ResetTrigger("Hit"); animator.ResetTrigger("Shot"); animator.SetTrigger("Death");
        }
    }
}

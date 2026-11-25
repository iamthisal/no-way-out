using System.Collections.Generic;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
namespace Lockdown.Agents.Pursuer
{
    [DisallowMultipleComponent]
    [MovedFrom(true, "Lockdown.AI.Agents", "Lockdown.AI.Agents", "PursuerPresentation")]
    public sealed class PursuerPresentation : MonoBehaviour
    {
        // Every guard reports its alert, but only the most urgent one is drawn, so bars never stack.
        private static readonly List<PursuerPresentation> Alerting = new List<PursuerPresentation>();
        private static readonly GuardAlertHud AlertHud = new GuardAlertHud();
        [SerializeField] private Animator animator;
        [SerializeField] private AudioClip warningSound;
        private Health health;
        private EnemyShooting weapon;
        private AudioSource audioSource;
        private int shots;
        private bool warning;
        private GuardAlert alert;
        private float arrestProgress = -1f;
        public GuardAlert Alert => alert;
        public void ShowNotice(GuardAlert value, float progress) { alert = value; arrestProgress = progress; }
        /// <summary>True when this guard's alert is the one on screen.</summary>
        public bool LeadsAlerts
        {
            get
            {
                if (!ShowsAlert) return false;
                bool earlier = true;
                foreach (var other in Alerting)
                {
                    if (other == this) { earlier = false; continue; }
                    if (other == null || !other.ShowsAlert) continue;
                    if (GuardAlerts.Outranks(other.alert, other.arrestProgress, alert, arrestProgress)) return false;
                    // Equal alerts: the guard that registered first keeps the bar, so it never flickers between guards.
                    if (earlier && !GuardAlerts.Outranks(alert, arrestProgress, other.alert, other.arrestProgress)) return false;
                }
                return true;
            }
        }
        private bool ShowsAlert => alert != GuardAlert.None && isActiveAndEnabled && health != null && !health.IsDead;
        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            health = GetComponent<Health>(); weapon = GetComponent<EnemyShooting>();
            audioSource = GetComponent<AudioSource>();
        }
        private void OnEnable()
        {
            if (!Alerting.Contains(this)) Alerting.Add(this);
            if (health == null) return;
            health.OnDamaged.AddListener(Hit); health.OnDeath.AddListener(Death);
        }
        private void OnDisable()
        {
            Alerting.Remove(this);
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
        private void OnGUI()
        {
            if (LeadsAlerts) AlertHud.Draw(alert, arrestProgress);
        }
    }
}

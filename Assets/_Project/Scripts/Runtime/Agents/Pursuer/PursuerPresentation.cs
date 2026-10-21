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
        private string notice;
        private float arrestProgress = -1f;
        private GUIStyle noticeStyle;
        public void ShowNotice(string text, float progress) { notice = text; arrestProgress = progress; }
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
        private void OnGUI()
        {
            if (string.IsNullOrEmpty(notice) || health == null || health.IsDead) return;
            if (noticeStyle == null) noticeStyle = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            float width = Mathf.Min(540f, Screen.width - 32f);
            GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f, width, 68f), notice, noticeStyle);
            if (arrestProgress < 0f) return;
            Color previous = GUI.color; GUI.color = new Color(1f, 0.65f, 0.15f);
            GUI.DrawTexture(new Rect((Screen.width - width) * 0.5f + 8f, Screen.height * 0.72f + 56f,
                (width - 16f) * Mathf.Clamp01(arrestProgress), 5f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}

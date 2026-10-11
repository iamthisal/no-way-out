using Lockdown.Interfaces;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [SerializeField] private UnityEvent onDamaged = new UnityEvent();
        [SerializeField] private UnityEvent onDeath = new UnityEvent();

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0f;
        public UnityEvent OnDamaged => onDamaged;
        public UnityEvent OnDeath => onDeath;

        private void Awake() { CurrentHealth = maxHealth; }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            onDamaged.Invoke();
            if (IsDead) onDeath.Invoke();
        }

        [ContextMenu("Take 25 Test Damage")]
        private void TakeTestDamage()
        {
            if (Application.isPlaying) TakeDamage(25f);
        }
    }
}

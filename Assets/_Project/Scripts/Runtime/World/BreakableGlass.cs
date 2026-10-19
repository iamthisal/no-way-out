using Lockdown.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class BreakableGlass : MonoBehaviour
    {
        [SerializeField] private Renderer[] intactRenderers = new Renderer[0];
        [SerializeField] private Collider[] blockingColliders = new Collider[0];
        [SerializeField] private ParticleSystem breakEffect;
        [SerializeField] private AudioClip breakSound;
        [SerializeField] private UnityEvent onBroken = new UnityEvent();
        private Health health;
        private bool started;

        public bool IsBroken { get; private set; }
        public UnityEvent OnBroken => onBroken;

        private void Awake()
        {
            health = GetComponent<Health>();
            // Default to this object only: a display's loot must remain visible.
            if (intactRenderers.Length == 0) intactRenderers = GetComponents<Renderer>();
            if (blockingColliders.Length == 0) blockingColliders = GetComponents<Collider>();
        }

        private void OnEnable()
        {
            health.OnDeath.AddListener(Break);
            if (started && health.IsDead) Break();
        }

        // All scene Awake calls finish before Start. Health may not have initialized
        // CurrentHealth yet when another component's OnEnable runs on scene load.
        private void Start() { started = true; if (health.IsDead) Break(); }

        private void OnDisable() { health.OnDeath.RemoveListener(Break); }

        private void Break()
        {
            if (IsBroken) return;
            IsBroken = true;
            foreach (var renderer in intactRenderers) if (renderer != null) renderer.enabled = false;
            foreach (var collider in blockingColliders) if (collider != null) collider.enabled = false;
            if (breakEffect != null) breakEffect.Play();
            if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, transform.position, 0.75f);
            onBroken.Invoke();
        }
    }
}

using System.Collections.Generic;
using Lockdown.Player;
using Lockdown.World;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.Enemies
{
    // The archer starts as a pile of bones beside the exhibit. Breaking nearby display glass,
    // stepping close, or shooting the pile makes the bones pull together into the archer.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class SkeletonDormancy : MonoBehaviour
    {
        [SerializeField] private SkeletonBody body;
        [SerializeField] private Behaviour[] enableOnWake = new Behaviour[0];
        [SerializeField] private BreakableGlass[] wakeOnBroken = new BreakableGlass[0];
        [SerializeField, Min(0f)] private float wakeRadius = 2.4f;
        [SerializeField] private bool startDormant = true;
        [SerializeField] private int pileSeed = 1907;
        [SerializeField] private UnityEvent onWake = new UnityEvent();

        private readonly List<BreakableGlass> subscribed = new List<BreakableGlass>();
        private FirstPersonController player;
        private float nextPlayerSearch;
        private bool waking;

        public bool IsWaking => waking;
        public bool HasWoken { get; private set; }
        public UnityEvent OnWake => onWake;

        private void Awake()
        {
            // Awake also runs on a disabled component; only an enabled, dormant setup holds the AI back.
            if (!enabled) return;
            if (!startDormant || body == null) { enabled = false; return; }
            foreach (var behaviour in enableOnWake) if (behaviour != null) behaviour.enabled = false;
        }

        private void Start()
        {
            body.ScatterToPile(transform.position, pileSeed);
            body.Hit += OnPileHit;
            foreach (var glass in wakeOnBroken)
            {
                if (glass == null || subscribed.Contains(glass)) continue;
                glass.OnBroken.AddListener(Wake);
                subscribed.Add(glass);
            }
        }

        private void OnDestroy()
        {
            if (body != null) body.Hit -= OnPileHit;
            foreach (var glass in subscribed) if (glass != null) glass.OnBroken.RemoveListener(Wake);
            subscribed.Clear();
        }

        private void Update()
        {
            if (waking) return;
            if (player == null && Time.time >= nextPlayerSearch)
            {
                player = FindAnyObjectByType<FirstPersonController>();
                nextPlayerSearch = Time.time + 1f;
            }
            if (player == null) return;
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= wakeRadius * wakeRadius) Wake();
        }

        private void OnPileHit(SkeletonHitbox piece, float amount) { Wake(); }

        public void Wake()
        {
            if (waking || body == null || body.IsCollapsed || !body.IsDormant) return;
            waking = true;
            body.Assemble(() =>
            {
                HasWoken = true;
                foreach (var behaviour in enableOnWake) if (behaviour != null) behaviour.enabled = true;
                onWake.Invoke();
            });
        }
    }
}

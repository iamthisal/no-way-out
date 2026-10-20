using Lockdown.Combat;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HoundAgent))]
    public sealed class HoundCombat : MonoBehaviour
    {
        [SerializeField] private Health playerHealth;
        [SerializeField] private LayerMask obstructionMask = Physics.DefaultRaycastLayers;
        [SerializeField] private UnityEvent onBite = new UnityEvent();
        private HoundAgent hound;
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private double nextBite;
        private double nextPlayerSearch;
        public int BitesDelivered { get; private set; }
        public Health Target { get => playerHealth; set => playerHealth = value; }

        private void Awake() { hound = GetComponent<HoundAgent>(); }
        private void OnEnable() { nextBite = Time.timeAsDouble + 0.7; }

        private void Update()
        {
            if (Time.deltaTime <= 0f || !hound.CanBite) return;
            if (playerHealth == null && Time.timeAsDouble >= nextPlayerSearch)
            {
                nextPlayerSearch = Time.timeAsDouble + 1;
                var player = FindAnyObjectByType<FirstPersonController>();
                playerHealth = player != null ? player.GetComponent<Health>() : null;
            }
            TryBite();
        }

        public bool TryBite()
        {
            if (!isActiveAndEnabled || Time.timeScale <= 0f || !hound.CanBite
                || Time.timeAsDouble < nextBite || playerHealth == null || playerHealth.IsDead
                || !playerHealth.gameObject.activeInHierarchy) return false;
            HoundSettings settings = hound.Settings;
            Vector3 offset = playerHealth.transform.position - transform.position;
            if (Mathf.Abs(offset.y) > 1.5f) return false;
            offset.y = 0f;
            float range = settings != null ? settings.biteRange : 1.45f;
            if (offset.sqrMagnitude > range * range) return false;

            Vector3 mouth = transform.position + Vector3.up * 0.6f;
            Vector3 aim = playerHealth.transform.position + Vector3.up * 0.7f;
            Vector3 direction = aim - mouth;
            int count = Physics.RaycastNonAlloc(mouth, direction.normalized, hits, direction.magnitude,
                obstructionMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(playerHealth.transform)) continue;
                return false;
            }
            nextBite = Time.timeAsDouble + (settings != null ? settings.biteCooldown : 1.2f);
            playerHealth.TakeDamage(settings != null ? settings.biteDamage : 12f);
            BitesDelivered++;
            onBite.Invoke();
            return true;
        }
    }
}

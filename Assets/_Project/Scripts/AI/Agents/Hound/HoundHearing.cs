using Lockdown.AI.Core.Hound;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    public sealed class HoundHearing : MonoBehaviour
    {
        [SerializeField] private HoundSettings settings;
        [Tooltip("Range for the original normalized HearNoise(position, loudness) API.")]
        [SerializeField, Min(0.1f)] private float hearingRange = 12f;
        [SerializeField, Min(0.1f)] private float memoryDuration = 5f;
        [SerializeField] private LayerMask obstructionMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.1f)] private float earHeight = 0.65f;
        [Tooltip("Optional territory restriction; leave empty for the connected museum.")]
        [SerializeField] private BoxCollider listeningArea;

        [Header("Runtime information")]
        [SerializeField] private bool hasClue;
        [SerializeField] private Vector3 lastHeardPosition;
        [SerializeField] private float memoryRemaining;
        [SerializeField] private int rememberedSounds;

        private HoundMemory memory;
        private readonly RaycastHit[] hits = new RaycastHit[32];

        public bool HasClue => hasClue;
        public Vector3 LastHeardPosition => lastHeardPosition;
        public HoundSettings Settings => settings;
        public HoundMemory Memory => memory ?? (memory = settings != null ? settings.CreateMemory()
            : new HoundMemory(maximumAge: memoryDuration));

        private void OnEnable() { HoundNoiseBus.NoiseEmitted += ReceiveBroadcast; }
        private void OnDisable()
        {
            HoundNoiseBus.NoiseEmitted -= ReceiveBroadcast;
            memory?.Clear();
            UpdateDebugValues();
        }

        private void ReceiveBroadcast(HoundSound sound, Transform source) { TryHear(sound, source); }

        // Backwards compatible with the first milestone: loudness is normalized to 0..1.
        public void HearNoise(Vector3 position, float loudness)
        {
            if (!HoundSound.IsFinite(loudness)) return;
            loudness = Mathf.Clamp01(loudness);
            TryHear(new HoundSound(position, HoundSoundKind.Custom, loudness * 6f,
                hearingRange * loudness, Time.timeAsDouble));
        }

        public bool TryHear(HoundSound sound, Transform source = null)
        {
            if (!isActiveAndEnabled || !Application.isPlaying || Time.timeScale <= 0f || !sound.IsValid) return false;
            if (source != null && (source == transform || source.IsChildOf(transform))) return false;
            if (listeningArea != null && !listeningArea.bounds.Contains(sound.Position)) return false;
            if ((sound.Position - transform.position).sqrMagnitude > sound.Radius * sound.Radius) return false;

            Vector3 origin = transform.position + Vector3.up * earHeight;
            Vector3 offset = sound.Position - origin;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, hits, offset.magnitude,
                obstructionMask, QueryTriggerInteraction.Ignore);
            bool obstructed = count == hits.Length;
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].transform;
                if (hit.IsChildOf(transform) || (source != null && hit.IsChildOf(source))) continue;
                obstructed = true;
                break;
            }
            if (obstructed)
            {
                sound = sound.Attenuated(settings != null ? settings.obstructedSoundMultiplier : 0f);
                if (!sound.IsValid || offset.magnitude > sound.Radius) return false;
            }
            bool accepted = Memory.Remember(sound, Time.timeAsDouble);
            UpdateDebugValues();
            return accepted;
        }

        private void Update()
        {
            Memory.Prune(transform.position, Time.timeAsDouble);
            UpdateDebugValues();
        }

        private void UpdateDebugValues()
        {
            HoundClue latest = memory?.Latest;
            rememberedSounds = memory?.Count ?? 0;
            hasClue = latest != null;
            memoryRemaining = latest == null ? 0f
                : Mathf.Max(0f, memory.MaximumAge - (float)(Time.timeAsDouble - latest.Sound.EmittedAt));
            if (latest != null) lastHeardPosition = latest.Sound.Position;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, hearingRange);
            if (!hasClue) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lastHeardPosition, 0.3f);
            Gizmos.DrawLine(transform.position, lastHeardPosition);
        }
    }
}

using UnityEngine;

namespace Lockdown.AI.Agents
{
    // First milestone: remember one audible clue. Target scoring and movement come later.
    public sealed class HoundHearing : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float hearingRange = 12f;
        [SerializeField, Min(0.1f)] private float memoryDuration = 5f;

        [Header("Read-only during testing: these values show the latest clue")]
        [SerializeField] private bool hasClue;
        [SerializeField] private Vector3 lastHeardPosition;
        [SerializeField] private float memoryRemaining;

        public bool HasClue => hasClue;
        public Vector3 LastHeardPosition => lastHeardPosition;

        // Call this when a gameplay noise occurs. Audio playback is a separate concern.
        // Loudness is 0..1: 1 reaches the full hearing range, 0.5 reaches half of it.
        public void HearNoise(Vector3 position, float loudness)
        {
            if (!isActiveAndEnabled) return;

            float audibleRange = hearingRange * Mathf.Clamp01(loudness);
            if (audibleRange <= 0f || Vector3.Distance(transform.position, position) > audibleRange)
                return;

            lastHeardPosition = position;
            memoryRemaining = memoryDuration;
            hasClue = true;
        }

        private void Update()
        {
            if (!hasClue) return;

            memoryRemaining = Mathf.Max(0f, memoryRemaining - Time.deltaTime);
            if (memoryRemaining == 0f) hasClue = false;
        }

        private void OnDisable()
        {
            hasClue = false;
            memoryRemaining = 0f;
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

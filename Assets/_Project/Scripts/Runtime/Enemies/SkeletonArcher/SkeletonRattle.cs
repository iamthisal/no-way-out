using UnityEngine;

namespace Lockdown.Enemies
{
    // Loose bones rattle louder the faster the skeleton moves, so players can track him by ear.
    // He falls silent when he stops to aim.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class SkeletonRattle : MonoBehaviour
    {
        [SerializeField] private SkeletonBody body;
        [SerializeField] private Transform tracked;
        [SerializeField, Min(0.1f)] private float fullVolumeSpeed = 4f;
        [SerializeField, Range(0f, 1f)] private float maxVolume = 0.75f;

        private AudioSource source;
        private Vector3 lastPosition;
        private float speed;

        public float Speed => speed;
        public float Volume => source != null ? source.volume : 0f;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            if (tracked == null) tracked = transform.parent != null ? transform.parent : transform;
        }

        private void OnEnable()
        {
            lastPosition = tracked.position;
            speed = 0f;
            // Keep the loop running and shape it with volume, so it never restarts audibly.
            if (source.clip != null) source.Play();
        }

        private void OnDisable() { source.Stop(); }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            Vector3 position = tracked.position;
            Vector3 step = position - lastPosition;
            step.y = 0f;
            lastPosition = position;
            speed = Mathf.Lerp(speed, step.magnitude / deltaTime, 1f - Mathf.Exp(-deltaTime * 8f));
            bool silent = body != null && (body.IsCollapsed || body.IsDormant);
            float amount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, fullVolumeSpeed, speed));
            source.volume = silent ? 0f : maxVolume * amount;
            source.pitch = 0.92f + 0.22f * amount;
        }
    }
}

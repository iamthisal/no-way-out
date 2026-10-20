using UnityEngine;

namespace Lockdown.Enemies
{
    // A standing coffin holding one mummy. Opening it swings the lid aside on its hinge and
    // releases the mummy, which walks out; that also unseals the mummy's canopic jar.
    [DisallowMultipleComponent]
    public sealed class Sarcophagus : MonoBehaviour
    {
        [SerializeField] private string godName = "Imsety";
        [SerializeField] private Transform lid;
        [SerializeField] private MummyBody mummy;
        [SerializeField] private AudioSource grind;
        [SerializeField] private ParticleSystem dust;
        [SerializeField, Range(30f, 170f)] private float openAngle = 105f;
        [SerializeField, Min(0.1f)] private float openDuration = 1.2f;
        [SerializeField, Min(0f)] private float releaseDelay = 0.45f;

        private Quaternion closedRotation;
        private float openSign = 1f;
        private float openedAt;
        private bool released;

        public string GodName => godName;
        public MummyBody Mummy => mummy;
        public bool IsOpen { get; private set; }
        public float LidAngle { get; private set; }

        private void Awake()
        {
            if (lid == null) return;
            closedRotation = lid.rotation;
            // Swing the lid outward: the direction that carries its middle forward, away from the case.
            var renderer = lid.GetComponentInChildren<Renderer>();
            Vector3 centre = renderer != null ? renderer.bounds.center : lid.position;
            Vector3 swung = lid.position + Quaternion.AngleAxis(60f, Vector3.up) * (centre - lid.position);
            openSign = Vector3.Dot(swung - centre, transform.forward) >= 0f ? 1f : -1f;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            openedAt = Time.time;
            if (grind != null && grind.clip != null) grind.Play();
            if (dust != null) dust.Play();
            var handle = GetComponentInChildren<SarcophagusLid>();
            if (handle != null) Destroy(handle);
        }

        private void Update()
        {
            if (!IsOpen) return;
            float t = Mathf.Clamp01((Time.time - openedAt) / openDuration);
            // Heavy at first, then swinging freely to a stop.
            float eased = 1f - (1f - t) * (1f - t) * (1f - t);
            LidAngle = openAngle * eased;
            if (lid != null) lid.rotation = Quaternion.AngleAxis(LidAngle * openSign, Vector3.up) * closedRotation;
            if (!released && Time.time - openedAt >= releaseDelay)
            {
                released = true;
                if (mummy != null) mummy.Release();
            }
            if (t >= 1f && released) enabled = false;
        }
    }
}

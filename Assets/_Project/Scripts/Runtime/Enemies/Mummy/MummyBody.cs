using System;
using Lockdown.Interfaces;
using Lockdown.World;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Enemies
{
    public enum MummyLife { Entombed, Emerging, Up, Down, Rising, Crumbling, Destroyed }

    // A mummy's body: waiting in its sarcophagus, walking out when released, wounds and knockdowns,
    // rising again while its canopic jar is intact, and crumbling to dust once that jar is broken,
    // leaving its amulet on the floor. The amulet shows its jar's glaze while the curse holds.
    [DisallowMultipleComponent]
    public sealed class MummyBody : MonoBehaviour, IDamageable
    {
        private static readonly int EntombedState = Animator.StringToHash("Entombed");
        private static readonly int WalkOutState = Animator.StringToHash("WalkOut");
        private static readonly int FallState = Animator.StringToHash("Fall");
        private static readonly int DownState = Animator.StringToHash("Down");
        private static readonly int RiseState = Animator.StringToHash("Rise");
        private static readonly int LocomotionState = Animator.StringToHash("Locomotion");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Animator animator;
        [SerializeField] private CanopicJar boundJar;
        [SerializeField] private Collider bodyCollider;
        [SerializeField] private Renderer amulet;
        [SerializeField] private Renderer[] pieces = new Renderer[0];
        [SerializeField] private GameObject amuletDrop;
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private AudioSource voice;
        [SerializeField] private AudioClip groanSound;
        [SerializeField] private AudioClip riseSound;
        [SerializeField] private AudioClip crumbleSound;
        [SerializeField, Min(1f)] private float maxHealth = 60f;
        [SerializeField, Min(0f)] private float riseDelay = 10f;
        [SerializeField, Min(0.1f)] private float walkOutDuration = 1.8f;
        [SerializeField, Min(0.1f)] private float fallDuration = 0.8f;
        [SerializeField, Min(0.1f)] private float riseDuration = 1.6f;
        [SerializeField, Min(0.1f)] private float crumbleDuration = 1.4f;
        [SerializeField] private bool startEntombed = true;

        private MaterialPropertyBlock block;
        private Vector3[] pieceScales;
        private int amuletSlot = -1;
        private float health;
        private float timer;
        private float phaseStart;
        private bool lying;
        private bool fallBeforeDust;
        private bool dustStarted;

        public event Action<float> Damaged;
        // First time out of the sarcophagus.
        public event Action Released;
        public event Action Downed;
        // Back up after a knockdown.
        public event Action Risen;
        public event Action Destroyed;
        public event Action JarBroken;
        public MummyLife Life { get; private set; } = MummyLife.Up;
        public bool IsUp => Life == MummyLife.Up;
        public bool IsMortal => boundJar == null || boundJar.IsBroken;
        public CanopicJar BoundJar => boundJar;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public float RiseDelay => riseDelay;
        public int TimesRisen { get; private set; }
        public GameObject DroppedAmulet { get; private set; }

        private void Awake()
        {
            health = maxHealth;
            block = new MaterialPropertyBlock();
            pieceScales = new Vector3[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) if (pieces[i] != null) pieceScales[i] = pieces[i].transform.localScale;
            if (amulet != null)
            {
                var materials = amulet.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && materials[i].name.Contains("Amulet")) amuletSlot = i;
            }
            if (startEntombed) Life = MummyLife.Entombed;
        }

        private void OnEnable() { if (boundJar != null) boundJar.OnBroken.AddListener(OnJarBroken); }

        private void OnDisable() { if (boundJar != null) boundJar.OnBroken.RemoveListener(OnJarBroken); }

        private void Start()
        {
            ShowAmulet();
            if (Life != MummyLife.Entombed) return;
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (animator == null) return;
            animator.Play(EntombedState, 0, 0f);
            // Pose it now, so the first frame never shows the mummy standing outside its coffin.
            animator.Update(0f);
        }

        public void BindJar(CanopicJar jar)
        {
            if (boundJar != null && isActiveAndEnabled) boundJar.OnBroken.RemoveListener(OnJarBroken);
            boundJar = jar;
            if (boundJar != null && isActiveAndEnabled) boundJar.OnBroken.AddListener(OnJarBroken);
            ShowAmulet();
        }

        // Called when its sarcophagus opens: it walks out, and its canopic jar can now be broken.
        public void Release()
        {
            if (Life != MummyLife.Entombed) return;
            Life = MummyLife.Emerging;
            phaseStart = Time.time;
            Play(WalkOutState, 0.1f);
            if (voice != null && groanSound != null) voice.PlayOneShot(groanSound);
            if (boundJar != null) boundJar.Unseal();
        }

        public void TakeDamage(float amount)
        {
            if (Life != MummyLife.Up || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            health = Mathf.Max(0f, health - amount);
            Damaged?.Invoke(amount);
            if (health > 0f) return;
            if (IsMortal) Crumble();
            else KnockDown();
        }

        private void KnockDown()
        {
            Life = MummyLife.Down;
            timer = fallDuration + riseDelay;
            phaseStart = Time.time;
            lying = false;
            if (bodyCollider != null) bodyCollider.enabled = false;
            Play(FallState, 0.08f);
            if (voice != null && groanSound != null) voice.PlayOneShot(groanSound);
            Downed?.Invoke();
        }

        public void Crumble()
        {
            if (Life == MummyLife.Crumbling || Life == MummyLife.Destroyed || Life == MummyLife.Entombed) return;
            fallBeforeDust = Life != MummyLife.Down;
            Life = MummyLife.Crumbling;
            phaseStart = Time.time;
            dustStarted = false;
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (fallBeforeDust) Play(FallState, 0.08f);
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.isStopped = true;
        }

        private void Update()
        {
            switch (Life)
            {
                case MummyLife.Emerging:
                    if (Time.time - phaseStart >= walkOutDuration) StandUp(false);
                    break;
                case MummyLife.Down:
                    if (!lying && Time.time - phaseStart >= fallDuration) { lying = true; Play(DownState, 0.15f); }
                    timer -= Time.deltaTime;
                    if (timer > 0f) break;
                    Life = MummyLife.Rising;
                    phaseStart = Time.time;
                    Play(RiseState, 0.1f);
                    if (voice != null && riseSound != null) voice.PlayOneShot(riseSound);
                    break;
                case MummyLife.Rising:
                    if (Time.time - phaseStart >= riseDuration) StandUp(true);
                    break;
                case MummyLife.Crumbling:
                    UpdateCrumble();
                    break;
            }
        }

        private void StandUp(bool afterKnockdown)
        {
            Life = MummyLife.Up;
            health = maxHealth;
            if (bodyCollider != null) bodyCollider.enabled = true;
            Play(LocomotionState, 0.2f);
            if (afterKnockdown) { TimesRisen++; Risen?.Invoke(); }
            else Released?.Invoke();
        }

        private void UpdateCrumble()
        {
            float elapsed = Time.time - phaseStart - (fallBeforeDust ? fallDuration : 0f);
            if (elapsed < 0f) return;
            if (!dustStarted)
            {
                dustStarted = true;
                if (animator != null) animator.speed = 0f;
                if (dust != null) dust.Play();
                if (crumbleSound != null) AudioSource.PlayClipAtPoint(crumbleSound, transform.position + Vector3.up * 0.5f, 0.9f);
                DropAmulet();
            }
            float k = Mathf.Clamp01(elapsed / crumbleDuration);
            // Wrappings collapse from the head down into dust.
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;
                float height = pieces[i].bounds.center.y - transform.position.y;
                float local = Mathf.Clamp01(k * 1.6f - Mathf.Max(0f, 1.8f - height) * 0.35f);
                pieces[i].transform.localScale = pieceScales[i] * (1f - local * local);
            }
            if (k < 1f) return;
            Life = MummyLife.Destroyed;
            foreach (var renderer in pieces) if (renderer != null) renderer.enabled = false;
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            Destroyed?.Invoke();
        }

        // The amulet survives the dust: it falls to the floor as loot.
        private void DropAmulet()
        {
            if (amuletDrop == null || amulet == null) return;
            amulet.enabled = false;
            DroppedAmulet = Instantiate(amuletDrop, amulet.bounds.center, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            var body = DroppedAmulet.GetComponent<Rigidbody>();
            if (body != null) body.linearVelocity = Vector3.up * 1.2f + transform.forward * 0.6f;
        }

        private void OnJarBroken()
        {
            ShowAmulet();
            JarBroken?.Invoke();
            if (Life == MummyLife.Down) Crumble();
        }

        private void ShowAmulet()
        {
            if (amulet == null || amuletSlot < 0 || block == null) return;
            Color glaze = boundJar != null ? boundJar.Glaze : new Color(0.27f, 0.70f, 0.68f);
            amulet.GetPropertyBlock(block, amuletSlot);
            block.SetColor(BaseColor, IsMortal ? Color.Lerp(glaze, Color.black, 0.8f) : glaze);
            block.SetColor(EmissionColor, IsMortal ? Color.black : glaze.linear * 0.8f);
            amulet.SetPropertyBlock(block, amuletSlot);
        }

        private void Play(int state, float fade)
        {
            if (animator != null && animator.isActiveAndEnabled) animator.CrossFadeInFixedTime(state, fade);
        }
    }
}

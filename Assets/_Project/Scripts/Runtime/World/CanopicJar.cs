using System;
using System.Collections.Generic;
using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.World
{
    // One of the four canopic jars. It holds the curse that raises one mummy and stays sealed,
    // glowing and unbreakable, until that mummy has been released from its sarcophagus.
    // Unsealed, the hammer or the pistol shatters it into its pre-cut shards.
    [DisallowMultipleComponent]
    public sealed class CanopicJar : MonoBehaviour, IDamageable
    {
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private string godName = "Imsety";
        [SerializeField] private Color glaze = new Color(0.27f, 0.70f, 0.68f);
        [SerializeField, Min(1f)] private float maxHealth = 40f;
        [SerializeField] private bool startSealed = true;
        [SerializeField] private Rigidbody[] fragments = new Rigidbody[0];
        [SerializeField] private Collider intactCollider;
        [SerializeField] private ParticleSystem spirit;
        [SerializeField] private AudioClip shatterSound;
        [SerializeField] private AudioClip curseSound;
        [SerializeField] private AudioClip sealedSound;
        [SerializeField, Min(0f)] private float burstSpeed = 1.6f;
        [SerializeField] private UnityEvent onBroken = new UnityEvent();

        private readonly List<(Renderer renderer, int slot)> glazed = new List<(Renderer, int)>();
        private MaterialPropertyBlock block;
        private float health;
        private float nextSealedSound;

        public event Action Rejected;
        public string GodName => godName;
        public Color Glaze => glaze;
        public float Health => health;
        public bool IsSealed { get; private set; }
        public bool IsBroken { get; private set; }
        public UnityEvent OnBroken => onBroken;

        private void Awake()
        {
            health = maxHealth;
            IsSealed = startSealed;
            block = new MaterialPropertyBlock();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && materials[i].name.StartsWith("Jar_" + godName)) glazed.Add((renderer, i));
            }
        }

        private void Update()
        {
            if (!IsSealed || IsBroken) return;
            // The curse shows as a slow pulse in the glaze.
            float pulse = 0.35f + 0.35f * Mathf.Sin(Time.time * 2.4f);
            SetGlow(glaze.linear * pulse);
        }

        public void Unseal()
        {
            if (!IsSealed) return;
            IsSealed = false;
            SetGlow(Color.black);
        }

        public void TakeDamage(float amount)
        {
            if (IsBroken || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            if (IsSealed)
            {
                if (sealedSound != null && Time.time >= nextSealedSound)
                {
                    AudioSource.PlayClipAtPoint(sealedSound, transform.position + Vector3.up * 0.25f, 0.6f);
                    nextSealedSound = Time.time + 0.4f;
                }
                Rejected?.Invoke();
                return;
            }
            health = Mathf.Max(0f, health - amount);
            if (health <= 0f) Shatter();
        }

        public void Shatter()
        {
            if (IsBroken) return;
            IsBroken = true;
            IsSealed = false;
            if (intactCollider != null) intactCollider.enabled = false;
            var player = FindAnyObjectByType<FirstPersonController>();
            Vector3 away = player != null ? transform.position - player.transform.position : Vector3.forward;
            away.y = 0f;
            away = away.sqrMagnitude > 1e-4f ? away.normalized : Vector3.forward;
            Vector3 centre = transform.position + Vector3.up * 0.15f;
            foreach (var fragment in fragments)
            {
                if (fragment == null) continue;
                fragment.isKinematic = false;
                var collider = fragment.GetComponent<Collider>();
                if (collider != null) collider.enabled = true;
                Vector3 outward = fragment.worldCenterOfMass - centre;
                outward.y = 0f;
                outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : UnityEngine.Random.insideUnitSphere;
                fragment.linearVelocity = (outward * 0.7f + away * 0.6f + Vector3.up * UnityEngine.Random.Range(0.6f, 1.4f)) * burstSpeed;
                fragment.angularVelocity = UnityEngine.Random.insideUnitSphere * 9f;
            }
            if (spirit != null)
            {
                var main = spirit.main;
                main.startColor = new ParticleSystem.MinMaxGradient(glaze, Color.Lerp(glaze, Color.white, 0.5f));
                spirit.Play();
            }
            if (shatterSound != null) AudioSource.PlayClipAtPoint(shatterSound, centre, 0.9f);
            if (curseSound != null) AudioSource.PlayClipAtPoint(curseSound, centre, 0.8f);
            onBroken.Invoke();
        }

        private void SetGlow(Color emission)
        {
            foreach (var (renderer, slot) in glazed)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block, slot);
                block.SetColor(EmissionColor, emission);
                renderer.SetPropertyBlock(block, slot);
            }
        }
    }
}

using System;
using Lockdown.Combat;
using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.Weapons
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonHammer : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float reach = 1.9f;
        [SerializeField, Min(1f)] private float damage = 100f;
        [SerializeField, Min(0.1f)] private float swingDuration = 0.55f;
        [SerializeField, Min(0f)] private float impactDelay = 0.16f;
        [SerializeField, Min(0.1f)] private float swingInterval = 0.65f;
        [SerializeField] private AudioSource swingAudio;
        private FirstPersonController player;
        private Health health;
        private InputAction attack;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private float swingStartedAt;
        private float nextSwingTime;
        private bool pendingImpact;
        private bool waitForRelease;

        public bool IsSwinging { get; private set; }
        public int SwingsPerformed { get; private set; }
        public float Reach => reach;
        public event Action<RaycastHit> OnHit;

        private void Awake()
        {
            player = GetComponentInParent<FirstPersonController>();
            health = player != null ? player.GetComponent<Health>() : null;
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            attack = new InputAction("HammerSwing", InputActionType.Button, "<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/rightTrigger");
            if (player == null || health == null)
            {
                Debug.LogError("FirstPersonHammer requires a player with Health.", this);
                enabled = false;
            }
        }

        private void OnEnable() { attack?.Enable(); waitForRelease = true; }
        private void OnDisable()
        {
            attack?.Disable();
            CancelSwing();
        }
        private void OnDestroy() { attack?.Dispose(); }
        private bool CanSwing => isActiveAndEnabled && player != null && player.enabled &&
            player.CanUseWeapon && health != null && !health.IsDead;

        private void Update()
        {
            if (!CanSwing) { CancelSwing(); return; }
            if (!attack.IsPressed()) waitForRelease = false;
            if (!waitForRelease && attack.WasPressedThisFrame()) TrySwing();
            if (!IsSwinging) return;
            float elapsed = Time.time - swingStartedAt;
            if (pendingImpact && elapsed >= Mathf.Min(impactDelay, swingDuration))
            {
                pendingImpact = false;
                Strike();
            }
            float phase = Mathf.Clamp01(elapsed / swingDuration);
            // Fast downstroke, then recover to the same held pose.
            float movement = phase < 0.35f ? Mathf.SmoothStep(0f, 1f, phase / 0.35f) :
                Mathf.SmoothStep(1f, 0f, (phase - 0.35f) / 0.65f);
            transform.localRotation = restRotation * Quaternion.Euler(movement * 70f, movement * -12f, 0f);
            transform.localPosition = restPosition + new Vector3(-0.08f, -0.06f, 0.12f) * movement;
            if (phase >= 1f) CancelSwing();
        }

        public bool TrySwing()
        {
            if (!CanSwing || IsSwinging || Time.time < nextSwingTime) return false;
            swingStartedAt = Time.time;
            nextSwingTime = Time.time + Mathf.Max(swingInterval, swingDuration);
            pendingImpact = true;
            IsSwinging = true;
            SwingsPerformed++;
            if (swingAudio != null && swingAudio.clip != null) swingAudio.Play();
            return true;
        }

        private void Strike()
        {
            var camera = player.PlayerCamera;
            if (camera == null) return;
            var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(player.transform)) continue;
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
                OnHit?.Invoke(hit);
                break; // The first solid surface blocks targets behind it.
            }
        }

        private void CancelSwing()
        {
            pendingImpact = false;
            IsSwinging = false;
            transform.localPosition = restPosition;
            transform.localRotation = restRotation;
        }
    }
}

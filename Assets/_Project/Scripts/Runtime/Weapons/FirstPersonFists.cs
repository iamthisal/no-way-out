using Lockdown.Combat;
using Lockdown.Interfaces;
using Lockdown.Player;
using Lockdown.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.Weapons
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonFists : MonoBehaviour
    {
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform leftForearm;
        [SerializeField] private Transform rightForearm;
        [SerializeField, Min(0.1f)] private float reach = 1.55f;
        [SerializeField, Min(1f)] private float damage = 22f;
        [SerializeField] private AudioClip swingSound;
        [SerializeField] private AudioClip hitSound;

        private FirstPersonController player;
        private Health health;
        private AudioSource audioSource;
        private InputAction attack;
        private Transform punchingHand;
        private Transform punchingForearm;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private Vector3 forearmRestPosition;
        private Quaternion forearmRestRotation;
        private Vector3 forearmRestScale;
        private Vector3 elbow;
        private Vector3 wristInHandSpace;
        private float punchStartedAt;
        private float nextPunchTime;
        private float windup;
        private float impact;
        private float duration;
        private bool nextIsRight;
        private bool pendingImpact;
        private bool waitForRelease;

        public bool IsPunching => punchingHand != null;

        private void Awake()
        {
            player = GetComponentInParent<FirstPersonController>();
            health = player != null ? player.GetComponent<Health>() : null;
            if (health == null || leftHand == null || rightHand == null || leftForearm == null || rightForearm == null)
            {
                enabled = false;
                return;
            }

            attack = new InputAction("Punch", InputActionType.Button, "<Mouse>/leftButton");
            attack.AddBinding("<Gamepad>/rightTrigger");
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        private void OnEnable()
        {
            attack?.Enable();
            waitForRelease = true;
        }

        private void OnDisable()
        {
            attack?.Disable();
            ResetPunch();
        }

        private void OnDestroy() => attack?.Dispose();

        private bool CanPunch => isActiveAndEnabled && player != null && player.enabled && player.CanUseWeapon &&
            health != null && !health.IsDead;

        private void Update()
        {
            if (!CanPunch)
            {
                ResetPunch();
                return;
            }

            if (!attack.IsPressed()) waitForRelease = false;
            if (!waitForRelease && attack.WasPressedThisFrame()) TryPunch();
            if (!IsPunching) return;

            float elapsed = Time.time - punchStartedAt;
            if (pendingImpact && elapsed >= impact)
            {
                pendingImpact = false;
                Strike();
            }
            if (elapsed >= duration)
            {
                ResetPunch();
                return;
            }

            float extension = elapsed < windup ? Mathf.SmoothStep(0f, -0.15f, elapsed / windup) :
                elapsed < impact ? Mathf.SmoothStep(-0.15f, 1f, (elapsed - windup) / (impact - windup)) :
                Mathf.SmoothStep(1f, 0f, (elapsed - impact) / (duration - impact));
            bool cross = punchingHand == rightHand;
            var contact = cross ? new Vector3(0.025f, -0.08f, 0.88f) : new Vector3(-0.065f, -0.075f, 0.94f);
            var contactRotation = cross ? Quaternion.Euler(2f, -10f, 27f) : Quaternion.Euler(-4f, 5f, -22f);
            punchingHand.localPosition = Vector3.LerpUnclamped(restPosition, contact, extension);
            punchingHand.localRotation = Quaternion.SlerpUnclamped(restRotation, contactRotation, extension);
            PoseForearm();
        }

        public bool TryPunch()
        {
            if (!CanPunch || IsPunching || Time.time < nextPunchTime) return false;

            bool cross = nextIsRight;
            punchingHand = cross ? rightHand : leftHand;
            punchingForearm = cross ? rightForearm : leftForearm;
            nextIsRight = !nextIsRight;
            restPosition = punchingHand.localPosition;
            restRotation = punchingHand.localRotation;
            forearmRestPosition = punchingForearm.localPosition;
            forearmRestRotation = punchingForearm.localRotation;
            forearmRestScale = punchingForearm.localScale;
            elbow = transform.InverseTransformPoint(punchingForearm.TransformPoint(Vector3.down));
            wristInHandSpace = punchingHand.InverseTransformPoint(punchingForearm.TransformPoint(Vector3.up));
            windup = cross ? 0.065f : 0.025f;
            impact = cross ? 0.16f : 0.105f;
            duration = cross ? 0.39f : 0.29f;
            punchStartedAt = Time.time;
            nextPunchTime = Time.time + duration;
            pendingImpact = true;
            if (swingSound != null) audioSource.PlayOneShot(swingSound, 0.5f);
            return true;
        }

        private void PoseForearm()
        {
            // Keep the elbow down at the guard position while the wrist follows the punch.
            var wrist = transform.InverseTransformPoint(punchingHand.TransformPoint(wristInHandSpace));
            var direction = wrist - elbow;
            punchingForearm.localPosition = punchingHand.InverseTransformPoint(transform.TransformPoint((elbow + wrist) * 0.5f));
            punchingForearm.localRotation = Quaternion.Inverse(punchingHand.localRotation) *
                Quaternion.FromToRotation(Vector3.up, direction);
            punchingForearm.localScale = new Vector3(forearmRestScale.x, direction.magnitude * 0.5f, forearmRestScale.z);
        }

        private void Strike()
        {
            var camera = player.PlayerCamera;
            if (camera == null || !FirstPersonMeleeHit.TryFind(camera.transform, player.transform, reach, out var hit)) return;

            if (hitSound != null) audioSource.PlayOneShot(hitSound, 0.65f);
            if (hit.collider.GetComponentInParent<BreakableGlass>() != null) return;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target is Health targetHealth) targetHealth.TakeDamage(damage, player.transform);
            else target?.TakeDamage(damage);
        }

        private void ResetPunch()
        {
            if (!IsPunching) return;
            punchingHand.localPosition = restPosition;
            punchingHand.localRotation = restRotation;
            punchingForearm.localPosition = forearmRestPosition;
            punchingForearm.localRotation = forearmRestRotation;
            punchingForearm.localScale = forearmRestScale;
            punchingHand = null;
            punchingForearm = null;
            pendingImpact = false;
        }
    }
}

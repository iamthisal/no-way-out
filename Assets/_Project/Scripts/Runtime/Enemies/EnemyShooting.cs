using Lockdown.Combat;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyShooting : MonoBehaviour
    {
        [SerializeField] private Transform gunPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private GameObject muzzleFlash;
        [SerializeField] private LineRenderer tracer;
        [SerializeField] private AudioSource shotAudio;
        [SerializeField, Min(0.1f)] private float damage = 10f;
        [SerializeField, Min(1f)] private float shootingRange = 4f;
        [SerializeField, Min(0.1f)] private float shotInterval = 1.2f;
        [SerializeField, Min(0f)] private float firstShotDelay = 0.8f;

        private Health health;
        private FirstPersonController target;
        private Health targetHealth;
        private float nextTargetSearch;
        private float nextShotTime;
        private float flashEndsAt;
        private bool hadVisibleTarget;

        public int ShotsFired { get; private set; }
        public float ShootingRange => shootingRange;

        private void Awake() { health = GetComponent<Health>(); }

        private void Update()
        {
            if (Time.time >= flashEndsAt) HideFlash();
            if (health.IsDead) { HideFlash(); return; }
            if ((target == null || !target.gameObject.activeInHierarchy) && Time.time >= nextTargetSearch)
            {
                target = FindAnyObjectByType<FirstPersonController>();
                targetHealth = target != null ? target.GetComponent<Health>() : null;
                nextTargetSearch = Time.time + 1f;
            }
            bool visible = HasVisibleTarget();
            if (visible)
            {
                if (gunPivot != null) gunPivot.LookAt(AimPoint());
                if (!hadVisibleTarget) nextShotTime = Mathf.Max(nextShotTime, Time.time + firstShotDelay);
                TryFire();
            }
            hadVisibleTarget = visible;
        }

        private Vector3 AimPoint() => target.transform.position + Vector3.up * 1.2f;

        private bool HasVisibleTarget()
        {
            if (target == null || !target.gameObject.activeInHierarchy || targetHealth == null || targetHealth.IsDead || muzzle == null)
                return false;
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Vector3 direction = AimPoint() - origin;
            return direction.sqrMagnitude <= shootingRange * shootingRange && HitsPlayer(origin, direction, out _);
        }

        private bool HitsPlayer(Vector3 origin, Vector3 direction, out RaycastHit nearest)
        {
            nearest = default;
            float nearestDistance = float.PositiveInfinity;
            // Ignore our own body, but keep the nearest wall or other character blocking the shot.
            foreach (var hit in Physics.RaycastAll(origin, direction.normalized, direction.magnitude + 0.35f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearestDistance) continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            return nearest.collider != null && nearest.collider.GetComponentInParent<Health>() == targetHealth;
        }

        public bool TryFire()
        {
            if (!isActiveAndEnabled || health.IsDead || Time.time < nextShotTime || !HasVisibleTarget()) return false;
            if (gunPivot != null) gunPivot.LookAt(AimPoint());
            if (!HitsPlayer(muzzle.position, AimPoint() - muzzle.position, out var hit)) return false;
            nextShotTime = Time.time + shotInterval;
            ShotsFired++;
            flashEndsAt = Time.time + 0.08f;
            if (muzzleFlash != null) muzzleFlash.SetActive(true);
            if (tracer != null)
            {
                tracer.SetPosition(0, muzzle.position);
                tracer.SetPosition(1, hit.point);
                tracer.enabled = true;
            }
            if (shotAudio != null && shotAudio.clip != null) shotAudio.PlayOneShot(shotAudio.clip);
            targetHealth.TakeDamage(damage);
            return true;
        }

        private void OnDisable() { HideFlash(); hadVisibleTarget = false; }

        private void HideFlash()
        {
            if (muzzleFlash != null) muzzleFlash.SetActive(false);
            if (tracer != null) tracer.enabled = false;
        }
    }
}

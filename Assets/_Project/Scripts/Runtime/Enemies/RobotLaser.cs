using Lockdown.Combat;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.Enemies
{
    /// <summary>
    /// A security robot's eye laser. It locks its aim and charges, its eye glowing white, before it fires,
    /// so a player who moves in time is missed. Only the player is hurt: the beam does not break glass.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotLaser : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Transform muzzle;
        [SerializeField] private LineRenderer beam;
        [SerializeField] private Renderer eye;
        [SerializeField] private AudioSource zap;
        [SerializeField, Min(0.1f)] private float damage = 8f;
        [SerializeField, Min(1f)] private float range = 12f;
        [SerializeField, Min(0f)] private float chargeTime = 0.45f;
        [SerializeField, Min(0.1f)] private float cooldown = 1.3f;
        [SerializeField] private Color restingColor = new Color(0.1f, 0.4f, 1f);
        [SerializeField] private Color huntingColor = new Color(1f, 0.08f, 0.05f);
        [SerializeField] private Color chargingColor = new Color(1f, 0.95f, 0.9f);
        private Vector3 lockedTarget;
        private float firesAt = -1f;
        private float readyAt;
        private float beamUntil;
        private bool hunting;
        private bool dead;
        private Color shown = Color.clear;
        private MaterialPropertyBlock block;

        public bool IsCharging => firesAt >= 0f;
        public int ShotsFired { get; private set; }
        public int Hits { get; private set; }
        public float Damage => damage;
        public float ChargeTime => chargeTime;
        public Vector3 LockedTarget => lockedTarget;

        // Red while hunting, blue while resting on the charger.
        public bool Hunting { get => hunting; set => hunting = value; }

        private void Awake() { if (beam != null) beam.enabled = false; }

        public bool TryCharge(Vector3 target)
        {
            if (dead || !isActiveAndEnabled || IsCharging || Time.time < readyAt) return false;
            lockedTarget = target;
            firesAt = Time.time + chargeTime;
            return true;
        }

        public void Cancel() { firesAt = -1f; }

        // A destroyed robot's eye goes dark.
        public void Shutdown()
        {
            dead = true;
            Cancel();
            if (beam != null) beam.enabled = false;
            ShowEye(Color.black);
        }

        private void Update()
        {
            if (dead) return;
            if (IsCharging && Time.time >= firesAt) Fire();
            if (beam != null && beam.enabled && Time.time >= beamUntil) beam.enabled = false;
            ShowEye(IsCharging ? chargingColor : hunting ? huntingColor : restingColor);
        }

        private void Fire()
        {
            firesAt = -1f;
            readyAt = Time.time + cooldown;
            ShotsFired++;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position;
            Vector3 direction = (lockedTarget - origin).normalized;
            Vector3 end = origin + direction * range;
            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(origin, direction, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearestDistance) continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            if (nearest.collider != null)
            {
                end = nearest.point;
                var player = nearest.collider.GetComponentInParent<FirstPersonController>();
                var health = player != null ? player.GetComponent<Health>() : null;
                if (health != null && !health.IsDead)
                {
                    health.TakeDamage(damage);
                    Hits++;
                }
            }
            if (beam != null)
            {
                beam.SetPosition(0, origin);
                beam.SetPosition(1, end);
                beam.enabled = true;
                beamUntil = Time.time + 0.08f;
            }
            if (zap != null && zap.clip != null) zap.PlayOneShot(zap.clip);
        }

        private void ShowEye(Color color)
        {
            if (eye == null || color == shown) return;
            shown = color;
            block ??= new MaterialPropertyBlock();
            eye.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(EmissionColor, color * 3f);
            eye.SetPropertyBlock(block);
        }
    }
}

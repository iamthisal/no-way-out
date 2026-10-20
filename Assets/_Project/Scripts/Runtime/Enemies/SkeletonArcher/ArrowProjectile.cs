using System.Collections.Generic;
using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.Enemies
{
    // A visible arrow with travel time and drop. It damages only the player and sticks in everything else.
    [DisallowMultipleComponent]
    public sealed class ArrowProjectile : MonoBehaviour
    {
        private const int MaxStuckArrows = 16;
        private static readonly Queue<ArrowProjectile> Stuck = new Queue<ArrowProjectile>();
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [SerializeField, Min(0.005f)] private float radius = 0.035f;
        [SerializeField, Min(0.5f)] private float lifetime = 4f;
        [SerializeField, Min(0f)] private float penetration = 0.07f;
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private AudioSource flightAudio;
        [SerializeField] private AudioClip impactSound;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

        private Vector3 velocity;
        private float gravity;
        private float damage;
        private Transform owner;
        private float age;

        public bool IsFlying { get; private set; }
        public bool IsStuck { get; private set; }
        public bool HitPlayer { get; private set; }
        public Collider StuckIn { get; private set; }
        public Vector3 Velocity => velocity;

        public void Launch(Vector3 position, Vector3 launchVelocity, float drop, float arrowDamage, Transform shooter)
        {
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(launchVelocity));
            velocity = launchVelocity;
            gravity = drop;
            damage = arrowDamage;
            owner = shooter;
            age = 0f;
            IsFlying = true;
            if (trail != null) { trail.Clear(); trail.emitting = true; }
            if (flightAudio != null && flightAudio.clip != null) flightAudio.Play();
        }

        private void Update() { if (IsFlying) Step(Time.deltaTime); }

        public void Step(float deltaTime)
        {
            if (!IsFlying || deltaTime <= 0f) return;
            age += deltaTime;
            if (age > lifetime) { Destroy(gameObject); IsFlying = false; return; }
            Vector3 start = transform.position;
            Vector3 previous = velocity;
            velocity += Vector3.down * (gravity * deltaTime);
            Vector3 move = (previous + velocity) * (0.5f * deltaTime);
            float distance = move.magnitude;
            if (distance < 1e-5f) return;
            Vector3 direction = move / distance;
            int count = Physics.SphereCastNonAlloc(start, radius, direction, Hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                // A sphere that starts inside a collider reports distance 0 and no contact point.
                if (Hits[i].distance <= 0f && Hits[i].point == Vector3.zero) continue;
                if (owner != null && Hits[i].transform.IsChildOf(owner)) continue;
                if (nearest < 0 || Hits[i].distance < Hits[nearest].distance) nearest = i;
            }
            if (nearest >= 0)
            {
                Land(Hits[nearest], direction);
                return;
            }
            transform.SetPositionAndRotation(start + move, Quaternion.LookRotation(velocity));
        }

        private void Land(RaycastHit hit, Vector3 direction)
        {
            IsFlying = false;
            if (flightAudio != null) flightAudio.Stop();
            if (impactSound != null) AudioSource.PlayClipAtPoint(impactSound, hit.point, 0.8f);
            if (hit.collider.GetComponentInParent<FirstPersonController>() != null)
            {
                HitPlayer = true;
                hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
            IsStuck = true;
            StuckIn = hit.collider;
            transform.SetPositionAndRotation(hit.point + direction * penetration, Quaternion.LookRotation(direction));
            // Follow moving bodies; scaled greybox walls would distort a child, so arrows stay unparented there.
            Vector3 scale = hit.collider.transform.lossyScale;
            if (hit.rigidbody != null || (Mathf.Abs(scale.x - scale.y) < 0.01f && Mathf.Abs(scale.y - scale.z) < 0.01f))
                transform.SetParent(hit.collider.transform, true);
            if (trail != null) trail.emitting = false;
            Stuck.Enqueue(this);
            while (Stuck.Count > MaxStuckArrows)
            {
                var oldest = Stuck.Dequeue();
                if (oldest != null) Destroy(oldest.gameObject);
            }
        }
    }
}

using UnityEngine;

namespace Lockdown.Weapons
{
    /// <summary>
    /// A temporary dark mark where a shot landed. It follows the surface it hit (a door
    /// swinging, an enemy walking) without being parented to it, so scaled walls do not
    /// stretch it, then shrinks away at the end of its lifetime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletHole : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField, Min(0f)] private float shrinkTime = 0.5f;

        private Transform surface;
        private bool attached;
        private Vector3 localPoint;
        private Quaternion localRotation;
        private Vector3 baseScale;
        private float spawnTime;

        public float Lifetime => lifetime;
        public Transform Surface => surface;

        private void Awake()
        {
            baseScale = transform.localScale;
            spawnTime = Time.time;
        }

        /// <summary>Keeps the hole at its current pose relative to <paramref name="target"/>.</summary>
        public void Attach(Transform target)
        {
            surface = target;
            attached = target != null;
            if (!attached) return;
            localPoint = target.InverseTransformPoint(transform.position);
            localRotation = Quaternion.Inverse(target.rotation) * transform.rotation;
        }

        private void LateUpdate()
        {
            float age = Time.time - spawnTime;
            if (age >= lifetime || (attached && surface == null))
            {
                Destroy(gameObject);
                return;
            }
            if (attached)
                transform.SetPositionAndRotation(surface.TransformPoint(localPoint), surface.rotation * localRotation);
            float remaining = lifetime - age;
            transform.localScale = shrinkTime > 0f && remaining < shrinkTime ? baseScale * (remaining / shrinkTime) : baseScale;
        }
    }
}

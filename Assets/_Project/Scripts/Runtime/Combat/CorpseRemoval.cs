using System.Collections;
using UnityEngine;

namespace Lockdown.Combat
{
    /// <summary>
    /// Removes a dead character: it lies still for a moment, sinks into the floor and is destroyed.
    /// Paused time does not count.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class CorpseRemoval : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float delay = 5f;
        [SerializeField, Min(0f)] private float sinkDuration = 1.5f;
        [SerializeField, Min(0f)] private float sinkDepth = 0.9f;
        private Health health;

        public bool IsRemoving { get; private set; }
        public float TotalTime => delay + sinkDuration;

        public void Configure(float removeDelay, float sinkSeconds, float depth)
        {
            delay = Mathf.Max(0f, removeDelay);
            sinkDuration = Mathf.Max(0f, sinkSeconds);
            sinkDepth = Mathf.Max(0f, depth);
        }

        private void Awake() { health = GetComponent<Health>(); }

        private void OnEnable() { health.OnDeath.AddListener(Begin); }

        private void OnDisable() { health.OnDeath.RemoveListener(Begin); }

        private void Start() { if (health.IsDead) Begin(); }

        private void Begin()
        {
            if (IsRemoving) return;
            IsRemoving = true;
            StartCoroutine(Remove());
        }

        private IEnumerator Remove()
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            // Loose physics pieces, such as the archer's bones, must sink with the body instead of resting on the floor.
            foreach (var piece in GetComponentsInChildren<Rigidbody>(true))
            {
                piece.interpolation = RigidbodyInterpolation.None;
                piece.collisionDetectionMode = CollisionDetectionMode.Discrete;
                piece.isKinematic = true;
            }
            foreach (var shape in GetComponentsInChildren<Collider>(true)) shape.enabled = false;
            Vector3 start = transform.position;
            for (float elapsed = 0f; elapsed < sinkDuration; elapsed += Time.deltaTime)
            {
                transform.position = start + Vector3.down * (sinkDepth * elapsed / sinkDuration);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}

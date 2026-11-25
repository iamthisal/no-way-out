using Lockdown.Interfaces;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>Heavy scenery the player can reposition by walking into it.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class PushableCrate : MonoBehaviour, IInteractable
    {
        [SerializeField, Min(0.1f)] private float pushAcceleration = 18f;
        [SerializeField, Min(0.1f)] private float maximumSpeed = 1.8f;
        [SerializeField, Min(0.1f)] private float mass = 35f;

        private Rigidbody body;

        public string InteractionLabel => "Walk into crate to push";

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.linearDamping = 5f;
            body.angularDamping = 8f;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        }

        private void FixedUpdate()
        {
            if (body == null) return;
            var horizontalVelocity = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
            if (horizontalVelocity.sqrMagnitude <= maximumSpeed * maximumSpeed) return;
            horizontalVelocity = horizontalVelocity.normalized * maximumSpeed;
            body.linearVelocity = new Vector3(horizontalVelocity.x, body.linearVelocity.y, horizontalVelocity.z);
        }

        /// <summary>Called by the player controller when it collides with this crate.</summary>
        public void Push(Vector3 direction)
        {
            if (body == null || direction.sqrMagnitude < 0.001f) return;
            direction.y = 0f;
            body.AddForce(direction.normalized * pushAcceleration, ForceMode.Acceleration);
        }

        // Movement is intentionally contact-based; the interaction prompt only explains the control.
        public bool TryInteract(GameObject interactor) => false;
    }
}

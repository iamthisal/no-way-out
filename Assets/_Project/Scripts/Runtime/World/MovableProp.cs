using System;
using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class MovableProp : MonoBehaviour, IInteractable
    {
        public static event Action<MovableProp, Vector3> DistractionLanded;

        private Rigidbody body;
        private BoxCollider shape;
        private PlayerInteraction holder;
        private AudioSource impactAudio;
        private Vector3 parkedPosition;
        private Quaternion parkedRotation;
        private float gripDistance;
        private float targetYaw;
        private float dragSpeed;
        private float throwExpires;
        private bool slidesAlongWall;

        public bool CanThrow { get; private set; }
        internal bool CanRotate => !slidesAlongWall && !CanThrow;
        public string InteractionLabel => slidesAlongWall ? "Grab packing crate (or walk into its side)" :
            CanThrow ? "Pick up " + gameObject.name : "Grab " + gameObject.name;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            shape = GetComponent<BoxCollider>();
            parkedPosition = body.position;
            parkedRotation = body.rotation;
        }

        internal void Configure(float mass, float speed, bool throwable, AudioClip landingSound)
        {
            CanThrow = throwable;
            dragSpeed = speed;
            body.mass = mass;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping = CanThrow ? 0.5f : 5f;
            body.angularDamping = 6f;

            if (CanThrow)
            {
                impactAudio = gameObject.AddComponent<AudioSource>();
                impactAudio.playOnAwake = false;
                impactAudio.spatialBlend = 1f;
                impactAudio.maxDistance = 24f;
                impactAudio.clip = landingSound;
                return;
            }

            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = shape.center;
            obstacle.size = shape.size;
            obstacle.carving = true;
            obstacle.carvingMoveThreshold = 0.08f;
            obstacle.carvingTimeToStationary = 0.25f;
        }

        public bool TryInteract(GameObject interactor)
        {
            var interaction = interactor != null ? interactor.GetComponent<PlayerInteraction>() : null;
            if (!isActiveAndEnabled || holder != null || interaction == null || !interaction.TryGrabProp(this))
                return false;

            holder = interaction;
            throwExpires = 0f;
            float clearance = new Vector2(shape.size.x, shape.size.z).magnitude * 0.5f + 0.44f;
            var offset = Vector3.ProjectOnPlane(body.position - holder.transform.position, Vector3.up);
            gripDistance = Mathf.Clamp(offset.magnitude, clearance, Mathf.Max(clearance, 2.35f));
            targetYaw = body.rotation.eulerAngles.y;
            body.useGravity = !CanThrow;
            return true;
        }

        internal void AllowSidewaysPush()
        {
            slidesAlongWall = true;
            body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation;
        }

        internal void PushFromSide(Vector3 direction, Vector3 normal)
        {
            if (!slidesAlongWall || holder != null || direction.z * normal.z > -0.25f) return;
            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, direction.z * dragSpeed);
        }

        internal bool UseAlternate(Camera camera)
        {
            if (!CanThrow)
            {
                if (!slidesAlongWall) targetYaw += 90f;
                return false;
            }

            // A wedged box stays in the world, with no teleport to the player's hand.
            if ((body.position - CarryPoint(camera)).sqrMagnitude > 0.6f * 0.6f) return false;
            Release();
            body.linearVelocity = camera.transform.forward * 8f + Vector3.up * 1.4f;
            body.angularVelocity = camera.transform.right * 4f;
            throwExpires = Time.time + 4f;
            return true;
        }

        internal void Release()
        {
            holder = null;
            body.useGravity = true;
            body.linearVelocity = new Vector3(0f, Mathf.Min(0f, body.linearVelocity.y), 0f);
            body.angularVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (body.position.y < parkedPosition.y - 4f)
            {
                if (holder != null) holder.ReleaseProp();
                throwExpires = 0f;
                body.position = parkedPosition;
                body.rotation = parkedRotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                return;
            }

            if (holder == null) return;
            var camera = holder.PlayerCamera;
            if (camera == null || !holder.CanInteract ||
                (body.position - holder.transform.position).sqrMagnitude > 3.5f * 3.5f)
            {
                holder.ReleaseProp();
                return;
            }

            Vector3 target = CanThrow ? CarryPoint(camera) : holder.transform.position + holder.transform.forward * gripDistance;
            Vector3 offset = target - body.position;
            if (!CanThrow) offset.y = 0f;
            if (slidesAlongWall) offset.x = 0f;
            Vector3 velocity = Vector3.ClampMagnitude(offset * 9f, dragSpeed);
            if (!CanThrow)
            {
                velocity.y = body.linearVelocity.y;
                float angle = Mathf.DeltaAngle(body.rotation.eulerAngles.y, targetYaw);
                body.angularVelocity = new Vector3(0f, Mathf.Clamp(angle * Mathf.Deg2Rad * 7f, -2.8f, 2.8f), 0f);
            }
            body.linearVelocity = velocity;
        }

        private static Vector3 CarryPoint(Camera camera) => camera.transform.TransformPoint(0.28f, -0.27f, 0.92f);

        private void OnCollisionEnter(Collision collision)
        {
            if (throwExpires <= Time.time || collision.relativeVelocity.sqrMagnitude < 0.64f ||
                collision.collider.GetComponentInParent<PlayerInteraction>() != null) return;
            throwExpires = 0f;
            if (impactAudio.clip != null) impactAudio.Play();
            DistractionLanded?.Invoke(this, collision.GetContact(0).point);
        }

        private void OnDisable()
        {
            if (holder != null) holder.ReleaseProp();
            throwExpires = 0f;
        }
    }
}

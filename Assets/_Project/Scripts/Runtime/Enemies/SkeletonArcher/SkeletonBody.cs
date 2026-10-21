using System;
using System.Collections;
using System.Collections.Generic;
using Lockdown.Combat;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Enemies
{
    // Owns the rigid bone pieces: locational damage, falling apart when defeated,
    // and gathering itself together from a pile of bones.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class SkeletonBody : MonoBehaviour
    {
        private static readonly int CrouchState = Animator.StringToHash("Crouch");

        [SerializeField] private Animator animator;
        [SerializeField] private SkeletonHitbox[] pieces = new SkeletonHitbox[0];
        [SerializeField] private Renderer bowString;
        [SerializeField] private AudioClip collapseSound;
        [SerializeField] private AudioClip assembleSound;
        [Header("Damage multipliers")]
        [SerializeField, Min(0f)] private float headMultiplier = 2f;
        [SerializeField, Min(0f)] private float gearMultiplier = 0.35f;
        [Header("Falling apart")]
        [SerializeField, Min(0f)] private float headshotDelay = 0.22f;
        [SerializeField, Min(0f)] private float scatterSpeed = 1.4f;
        [SerializeField, Min(0f)] private float skullLaunchSpeed = 5.5f;
        [Header("Bone pile")]
        [SerializeField, Min(0.1f)] private float pileRadius = 0.5f;
        [SerializeField, Min(0.2f)] private float assembleDuration = 1.6f;

        private readonly RaycastHit[] floorHits = new RaycastHit[8];
        private Health health;
        private FirstPersonController player;
        private Transform[] bones;
        private Vector3[] restPositions;
        private Quaternion[] restRotations;
        private Vector3[] pilePositions;
        private Quaternion[] pileRotations;
        private float[] assembleDelays;
        private float assembleStart;
        private Action assembled;

        public event Action<SkeletonHitbox, float> Hit;
        public IReadOnlyList<SkeletonHitbox> Pieces => pieces;
        public SkeletonHitbox LastHit { get; private set; }
        public bool IsCollapsed { get; private set; }
        public bool IsDormant { get; private set; }
        public bool IsAssembling { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            bones = new Transform[pieces.Length];
            restPositions = new Vector3[pieces.Length];
            restRotations = new Quaternion[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i].transform;
                bones[i] = piece.parent;
                restPositions[i] = piece.localPosition;
                restRotations[i] = piece.localRotation;
            }
        }

        private void OnEnable() { health.OnDeath.AddListener(Collapse); }

        private void OnDisable() { health.OnDeath.RemoveListener(Collapse); }

        public void ReceiveHit(SkeletonHitbox piece, float amount)
        {
            if (piece == null || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            if (IsCollapsed)
            {
                // Shooting loose bones knocks them around.
                var body = piece.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic) body.AddForce((AwayFromPlayer() + Vector3.up * 0.4f) * (amount * 0.06f), ForceMode.Impulse);
                return;
            }
            LastHit = piece;
            Hit?.Invoke(piece, amount);
            float multiplier = piece.Region == SkeletonRegion.Head ? headMultiplier
                : piece.Region == SkeletonRegion.Gear ? gearMultiplier : 1f;
            health.TakeDamage(amount * multiplier);
        }

        public void Collapse()
        {
            if (IsCollapsed) return;
            IsCollapsed = true;
            IsAssembling = false;
            IsDormant = false;
            if (animator != null) animator.enabled = false;
            if (bowString != null) bowString.enabled = false;
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            if (collapseSound != null) AudioSource.PlayClipAtPoint(collapseSound, transform.position + Vector3.up, 0.9f);
            Vector3 away = AwayFromPlayer();
            bool headshot = LastHit != null && LastHit.Region == SkeletonRegion.Head;
            if (headshot)
            {
                foreach (var piece in pieces)
                    if (piece.name.Contains("Skull"))
                        Detach(piece, away * skullLaunchSpeed + Vector3.up * 2.2f, UnityEngine.Random.onUnitSphere * 14f);
            }
            if (headshot && headshotDelay > 0f) StartCoroutine(CrumbleAfter(headshotDelay, away));
            else Crumble(away);
        }

        private IEnumerator CrumbleAfter(float delay, Vector3 away)
        {
            // The headless body stands frozen for a moment before it gives way.
            yield return new WaitForSeconds(delay);
            Crumble(away);
        }

        private void Crumble(Vector3 away)
        {
            foreach (var piece in pieces)
            {
                if (piece == null || piece.GetComponent<Rigidbody>() != null) continue;
                var renderer = piece.GetComponent<Renderer>();
                // The nocked arrow is hidden between shots; there is nothing to drop.
                if (renderer != null && !renderer.enabled) { Destroy(piece.gameObject); continue; }
                Vector3 velocity = away * (scatterSpeed * UnityEngine.Random.Range(0.25f, 1f))
                    + Vector3.up * UnityEngine.Random.Range(0f, 0.7f) + UnityEngine.Random.insideUnitSphere * 0.5f;
                Detach(piece, velocity, UnityEngine.Random.insideUnitSphere * 7f);
            }
        }

        private void Detach(SkeletonHitbox piece, Vector3 velocity, Vector3 spin)
        {
            piece.transform.SetParent(transform, true);
            var collider = piece.GetComponent<Collider>();
            if (collider != null) collider.enabled = true;
            var body = piece.gameObject.AddComponent<Rigidbody>();
            Vector3 size = collider != null ? collider.bounds.size : Vector3.one * 0.1f;
            body.mass = Mathf.Clamp(size.x * size.y * size.z * 600f, 0.08f, 2.5f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxAngularVelocity = 25f;
            // Neighbouring bones overlap; separate them gently instead of letting them burst apart.
            body.maxDepenetrationVelocity = 0.6f;
            body.linearVelocity = velocity;
            body.angularVelocity = spin;
        }

        public void ScatterToPile(Vector3 centre, int seed)
        {
            if (IsCollapsed) return;
            IsDormant = true;
            if (animator != null) animator.enabled = false;
            if (bowString != null) bowString.enabled = false;
            var random = new System.Random(seed);
            float floor = FloorHeight(centre);
            pilePositions = new Vector3[pieces.Length];
            pileRotations = new Quaternion[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i].transform;
                var filter = piece.GetComponent<MeshFilter>();
                var local = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.001f);
                Vector3 size = Vector3.Scale(local.size, piece.lossyScale);
                Vector3 longest = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
                // Lay each bone flat with its long axis horizontal, scattered around the centre.
                var rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f)
                    * Quaternion.AngleAxis((float)random.NextDouble() * 360f, Vector3.right)
                    * Quaternion.FromToRotation(longest, Vector3.right);
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float radius = pileRadius * Mathf.Sqrt((float)random.NextDouble()) * (size.magnitude > 0.3f ? 0.5f : 1f);
                piece.SetPositionAndRotation(centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius, rotation);
                // Lowest mesh corner from the transform itself; renderer bounds can lag a moved transform.
                float bottom = float.PositiveInfinity;
                for (int corner = 0; corner < 8; corner++)
                {
                    var offset = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    bottom = Mathf.Min(bottom, piece.TransformPoint(local.center + Vector3.Scale(local.extents, offset)).y);
                }
                piece.position += Vector3.up * (floor + 0.004f * i - bottom);
                pilePositions[i] = piece.position;
                pileRotations[i] = piece.rotation;
            }
        }

        public void Assemble(Action done)
        {
            if (IsCollapsed || IsAssembling) return;
            if (!IsDormant) { done?.Invoke(); return; }
            IsAssembling = true;
            assembled = done;
            if (animator != null)
            {
                animator.enabled = true;
                animator.Play(CrouchState, 0, 0f);
                animator.Update(0f);
            }
            float floor = FloorHeight(transform.position);
            assembleDelays = new float[pieces.Length];
            for (int i = 0; i < pieces.Length; i++)
            {
                // Feet gather first and the skull lands last.
                float height = bones[i].TransformPoint(restPositions[i]).y - floor;
                assembleDelays[i] = assembleDuration * 0.35f * Mathf.Clamp01(height / 1.2f);
            }
            assembleStart = Time.time;
            if (assembleSound != null) AudioSource.PlayClipAtPoint(assembleSound, transform.position + Vector3.up * 0.4f, 0.9f);
        }

        private void LateUpdate()
        {
            if (IsDormant && !IsAssembling && pilePositions != null)
            {
                // The pieces still hang from the rig's bones; hold them in the pile whatever the bones do.
                for (int i = 0; i < pieces.Length; i++) pieces[i].transform.SetPositionAndRotation(pilePositions[i], pileRotations[i]);
                return;
            }
            if (!IsAssembling) return;
            float elapsed = Time.time - assembleStart;
            float travel = assembleDuration * 0.65f;
            bool finished = true;
            for (int i = 0; i < pieces.Length; i++)
            {
                float k = Mathf.Clamp01((elapsed - assembleDelays[i]) / travel);
                if (k < 1f) finished = false;
                float eased = k * k * (3f - 2f * k);
                Vector3 target = bones[i].TransformPoint(restPositions[i]);
                Quaternion targetRotation = bones[i].rotation * restRotations[i];
                Vector3 position = Vector3.Lerp(pilePositions[i], target, eased) + Vector3.up * (Mathf.Sin(eased * Mathf.PI) * 0.25f);
                pieces[i].transform.SetPositionAndRotation(position, Quaternion.Slerp(pileRotations[i], targetRotation, eased));
            }
            if (!finished) return;
            for (int i = 0; i < pieces.Length; i++)
                pieces[i].transform.SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
            IsAssembling = false;
            IsDormant = false;
            if (bowString != null) bowString.enabled = true;
            var callback = assembled;
            assembled = null;
            callback?.Invoke();
        }

        private Vector3 AwayFromPlayer()
        {
            if (player == null) player = FindAnyObjectByType<FirstPersonController>();
            Vector3 away = player != null ? transform.position - player.transform.position : -transform.forward;
            away.y = 0f;
            return away.sqrMagnitude > 1e-4f ? away.normalized : -transform.forward;
        }

        private float FloorHeight(Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up, Vector3.down, floorHits, 3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
                if (!floorHits[i].transform.IsChildOf(transform) && floorHits[i].point.y > best && floorHits[i].point.y <= point.y + 0.5f)
                    best = floorHits[i].point.y;
            return float.IsNegativeInfinity(best) ? point.y : best;
        }
    }
}

using System.Collections.Generic;
using Lockdown.AI.Core.Ambusher;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    // Unity side of the Ambusher: senses the player, answers the brain's cover queries with
    // NavMesh samples and raycasts, then drives movement, the bow and the animation states.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public sealed class SkeletonArcherAgent : MonoBehaviour, IAmbusherWorld
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int LocomotionState = Animator.StringToHash("Locomotion");
        private static readonly int CrouchState = Animator.StringToHash("Crouch");
        private static readonly int HitState = Animator.StringToHash("Hit");
        private static readonly List<SkeletonArcherAgent> Active = new List<SkeletonArcherAgent>();

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private SkeletonBow bow;
        [SerializeField] private SkeletonBody body;

        [Header("Senses")]
        [SerializeField, Min(1f)] private float sightRange = 18f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 140f;
        [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
        [SerializeField, Min(0f)] private float playerEyeHeight = 1.55f;
        [SerializeField, Min(0f)] private float playerChestHeight = 1.2f;
        [SerializeField] private LayerMask sightLayers = Physics.DefaultRaycastLayers;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 2f;
        [SerializeField, Min(0.1f)] private float retreatSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float huntSpeed = 2.8f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.45f;
        [SerializeField, Min(0.1f)] private float stallTime = 0.8f;
        [SerializeField, Min(90f)] private float aimTurnSpeed = 540f;

        [Header("Cover search")]
        [SerializeField, Min(2f)] private float territoryRadius = 14f;
        [SerializeField, Min(2f)] private float searchRadius = 11f;
        [SerializeField, Min(0.5f)] private float sampleSpacing = 1.5f;
        [SerializeField, Min(0.2f)] private float crouchHeight = 1f;
        [SerializeField, Min(0.2f)] private float standHeight = 1.5f;
        [SerializeField, Min(1f)] private float idealRange = 9f;
        [SerializeField, Min(1f)] private float maxRange = 16f;

        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private readonly List<AmbusherCandidate> candidates = new List<AmbusherCandidate>();
        private readonly List<AmbusherFlankCandidate> flanks = new List<AmbusherFlankCandidate>();
        private NavMeshAgent agent;
        private Health health;
        private NavMeshPath path;
        private AmbusherBrain brain;
        private AmbusherCoverScorer scorer;
        private FirstPersonController player;
        private Vector3 home;
        private Vector3 lastPlayerPosition;
        private Vector3 playerVelocity;
        private float nextPlayerSearch;
        private float stalled;
        private float hitUntil;
        private bool tookDamage;
        private bool arrived;
        private int baseState;

        public AmbusherBrain Brain => brain;
        public AmbusherState State => brain != null ? brain.State : AmbusherState.Unaware;
        public bool SeesPlayer { get; private set; }
        public Vector3 Home => home;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            path = new NavMeshPath();
            home = transform.position;
            brain = new AmbusherBrain(home);
            scorer = new AmbusherCoverScorer { IdealRange = idealRange, MaxRange = maxRange };
        }

        private void OnEnable()
        {
            Active.Add(this);
            health.OnDamaged.AddListener(OnDamaged);
            health.OnDeath.AddListener(OnDeath);
            if (body != null) body.Hit += OnBodyHit;
            baseState = 0;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            health.OnDamaged.RemoveListener(OnDamaged);
            health.OnDeath.RemoveListener(OnDeath);
            if (body != null) body.Hit -= OnBodyHit;
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
        }

        private void Start()
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(transform.position, out var hit, 1f, filter)) agent.Warp(hit.position);
            home = transform.position;
            brain = new AmbusherBrain(home, brain.Settings);
            lastPlayerPosition = home;
        }

        private void Update()
        {
            if (health.IsDead || brain.State == AmbusherState.Dead) return;
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            FindPlayer();
            var senses = Sense(deltaTime);
            brain.Tick(senses, deltaTime, this);
            tookDamage = false;
            // The brain may have chosen a new post this frame.
            arrived = AmbusherBrain.Flat(transform.position, brain.Goal) <= arrivalDistance;
            Move(deltaTime);
            Shoot(deltaTime);
            Animate();
        }

        private AmbusherSenses Sense(float deltaTime)
        {
            var senses = new AmbusherSenses
            {
                Position = transform.position,
                PlayerDistance = float.PositiveInfinity,
                TookDamage = tookDamage,
                ShotsFired = bow != null ? bow.ShotsFired : 0,
                BowCommitted = bow != null && bow.IsCommitted,
                NavigationStalled = stalled > stallTime || (agent.hasPath && !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete)
            };
            if (senses.NavigationStalled) stalled = 0f;
            arrived = AmbusherBrain.Flat(transform.position, brain.Goal) <= arrivalDistance;
            senses.ArrivedAtGoal = arrived;
            SeesPlayer = false;
            if (player == null) return senses;

            Vector3 position = player.transform.position;
            Vector3 velocity = (position - lastPlayerPosition) / deltaTime;
            velocity.y = 0f;
            playerVelocity = Vector3.Lerp(playerVelocity, velocity, 1f - Mathf.Exp(-deltaTime * 10f));
            lastPlayerPosition = position;
            senses.PlayerPosition = position;
            senses.PlayerDistance = Vector3.Distance(transform.position, position);
            SeesPlayer = CanSee(position);
            senses.SeesPlayer = SeesPlayer;
            Vector3 toArcher = transform.position - position;
            toArcher.y = 0f;
            senses.ClosingSpeed = toArcher.sqrMagnitude > 1e-4f ? Vector3.Dot(playerVelocity, toArcher.normalized) : 0f;
            return senses;
        }

        private bool CanSee(Vector3 playerPosition)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = playerPosition + Vector3.up * playerChestHeight;
            Vector3 offset = target - eye;
            if (offset.sqrMagnitude > sightRange * sightRange) return false;
            Vector3 flat = new Vector3(offset.x, 0f, offset.z);
            // Once alerted he tracks all round; unaware he only notices what is in front or very close.
            if (!brain.Alerted && flat.magnitude > 4f && Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f) return false;
            return LineClear(eye, target);
        }

        private bool LineClear(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance < 1e-3f) return true;
            int count = Physics.RaycastNonAlloc(from, offset / distance, sightHits, distance, sightLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i].transform;
                if (hit.IsChildOf(transform) || (player != null && hit.IsChildOf(player.transform))) continue;
                return false;
            }
            return true;
        }

        private void Move(float deltaTime)
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            if (brain.State == AmbusherState.Unaware) { agent.isStopped = true; return; }
            if (brain.State == AmbusherState.Retreat && bow != null && bow.IsActive) bow.Cancel();
            bool holdForBow = bow != null && bow.IsActive;
            agent.speed = brain.State == AmbusherState.Retreat ? retreatSpeed : brain.State == AmbusherState.Hunt ? huntSpeed : walkSpeed;
            if (holdForBow || arrived)
            {
                agent.isStopped = true;
                agent.ResetPath();
                stalled = 0f;
                // Stationary: face the player (aiming) or where the player was last seen (hiding).
                agent.updateRotation = false;
                Vector3 look = (SeesPlayer && player != null ? player.transform.position : brain.LastKnown) - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 1e-3f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look),
                        (holdForBow ? aimTurnSpeed : aimTurnSpeed * 0.4f) * deltaTime);
                return;
            }
            agent.updateRotation = true;
            agent.isStopped = false;
            Vector3 goal = brain.Goal;
            if (!agent.hasPath || (agent.destination - goal).sqrMagnitude > 0.04f) agent.SetDestination(goal);
            bool slow = !agent.pathPending && agent.velocity.sqrMagnitude < 0.01f;
            stalled = slow ? stalled + deltaTime : 0f;
        }

        private void Shoot(float deltaTime)
        {
            if (bow == null) return;
            Vector3 aim = player != null ? player.transform.position + Vector3.up * playerChestHeight : brain.LastKnown;
            bow.Tick(brain.WantsToShoot, aim, playerVelocity, deltaTime);
        }

        private void Animate()
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            float speed = agent.isActiveAndEnabled ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedParameter, speed, 0.12f, Time.deltaTime);
            if (bow != null && bow.IsActive) { baseState = 0; return; }
            int desired = Time.time < hitUntil ? HitState
                : brain.State == AmbusherState.Hide && arrived ? CrouchState : LocomotionState;
            if (desired == baseState) return;
            baseState = desired;
            animator.CrossFadeInFixedTime(desired, desired == HitState ? 0.05f : 0.22f);
        }

        private void FindPlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy) return;
            if (Time.time < nextPlayerSearch) return;
            player = FindAnyObjectByType<FirstPersonController>();
            if (player != null) lastPlayerPosition = player.transform.position;
            nextPlayerSearch = Time.time + 1f;
        }

        private void OnDamaged()
        {
            tookDamage = true;
            if (!health.IsDead && (bow == null || !bow.IsCommitted)) hitUntil = Time.time + 0.33f;
        }

        private void OnBodyHit(SkeletonHitbox piece, float amount)
        {
            if (piece.Region == SkeletonRegion.BowArm && bow != null) bow.SpoilAim(4f);
        }

        private void OnDeath()
        {
            brain.Kill();
            if (bow != null) bow.Disarm();
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            enabled = false;
        }

        // ---------------------------------------------------------------- IAmbusherWorld

        public bool IsHidden(Vector3 point, Vector3 threat) =>
            !LineClear(threat + Vector3.up * playerEyeHeight, point + Vector3.up * crouchHeight);

        public bool TryChoosePost(Vector3 self, Vector3 threat, bool flee, AmbusherPost current, Vector3? failedPost, out AmbusherPost post)
        {
            GatherCandidates(self, threat);
            return scorer.TryChoose(candidates, self, threat, flee, current.Cover, failedPost, PathLength, out post);
        }

        public bool TryChooseFlank(Vector3 self, Vector3 lastKnown, int attempt, out Vector3 point)
        {
            flanks.Clear();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 target = lastKnown + Vector3.up * playerChestHeight;
            // Rings around where the player was last seen; each point is tested for a view of that spot.
            foreach (float radius in new[] { 3.5f, 5f, 6.5f, 8f })
                for (int i = 0; i < 16; i++)
                {
                    float angle = i * Mathf.PI / 8f;
                    Vector3 sample = lastKnown + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (AmbusherBrain.Flat(sample, home) > territoryRadius + 3f) continue;
                    if (!NavMesh.SamplePosition(sample, out var hit, 0.6f, filter) || Mathf.Abs(hit.position.y - self.y) > 1f) continue;
                    flanks.Add(new AmbusherFlankCandidate
                    {
                        Point = hit.position,
                        SeesLastKnown = LineClear(hit.position + Vector3.up * standHeight, target),
                        TravelDistance = AmbusherBrain.Flat(self, hit.position),
                        InsideTerritory = AmbusherBrain.Flat(hit.position, home) <= territoryRadius
                    });
                }
            return scorer.TryChooseFlank(flanks, self, lastKnown, attempt, PathLength, out point);
        }

        private void GatherCandidates(Vector3 self, Vector3 threat)
        {
            candidates.Clear();
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 threatEye = threat + Vector3.up * playerEyeHeight;
            Vector3 threatChest = threat + Vector3.up * playerChestHeight;
            int steps = Mathf.CeilToInt(searchRadius / sampleSpacing);
            for (int x = -steps; x <= steps; x++)
            for (int z = -steps; z <= steps; z++)
            {
                Vector3 sample = self + new Vector3(x, 0f, z) * sampleSpacing;
                if (AmbusherBrain.Flat(sample, self) > searchRadius || AmbusherBrain.Flat(sample, home) > territoryRadius + 3f) continue;
                if (!NavMesh.SamplePosition(sample, out var hit, 0.6f, filter) || Mathf.Abs(hit.position.y - self.y) > 1f) continue;
                Vector3 cover = hit.position;
                bool duplicate = false;
                foreach (var existing in candidates)
                    if ((existing.Cover - cover).sqrMagnitude < 0.5f) { duplicate = true; break; }
                if (duplicate) continue;
                var candidate = new AmbusherCandidate
                {
                    Cover = cover,
                    Peek = cover,
                    TravelDistance = AmbusherBrain.Flat(self, cover),
                    InsideTerritory = AmbusherBrain.Flat(cover, home) <= territoryRadius,
                    HiddenAtCover = !LineClear(threatEye, cover + Vector3.up * crouchHeight)
                };
                if (candidate.HiddenAtCover) FindPeek(ref candidate, threatChest, filter);
                foreach (var other in Active)
                    if (other != this && other.brain != null && other.brain.State != AmbusherState.Dead
                        && AmbusherBrain.Flat(other.brain.Post.Cover, cover) < 3f) { candidate.Reserved = true; break; }
                candidates.Add(candidate);
            }
        }

        // A peek point beside the cover where standing up gives a clear shot at the player.
        private void FindPeek(ref AmbusherCandidate candidate, Vector3 threatChest, NavMeshQueryFilter filter)
        {
            foreach (float radius in new[] { 0.9f, 1.6f })
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector3 probe = candidate.Cover + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(probe, out var hit, 0.4f, filter)) continue;
                    if (!LineClear(hit.position + Vector3.up * standHeight, threatChest)) continue;
                    candidate.Peek = hit.position;
                    candidate.ClearShotAtPeek = true;
                    return;
                }
        }

        private float PathLength(Vector3 target)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (!NavMesh.CalculatePath(transform.position, target, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                return float.PositiveInfinity;
            float length = 0f;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            return length;
        }

        private void OnDrawGizmosSelected()
        {
            if (brain == null) return;
            Gizmos.color = new Color(0.3f, 0.9f, 1f);
            Gizmos.DrawWireSphere(brain.Post.Cover + Vector3.up * 0.1f, 0.3f);
            Gizmos.color = new Color(1f, 0.5f, 0.2f);
            Gizmos.DrawWireSphere(brain.Post.Peek + Vector3.up * 0.1f, 0.2f);
            Gizmos.DrawLine(brain.Post.Cover, brain.Post.Peek);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(brain.LastKnown + Vector3.up * 0.1f, 0.25f);
            if (brain.State != AmbusherState.Hunt) return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(brain.HuntPoint + Vector3.up * 0.1f, 0.3f);
            Gizmos.DrawLine(brain.HuntPoint + Vector3.up * 1.5f, brain.LastKnown + Vector3.up * 1.2f);
        }
    }
}

using System.Collections.Generic;
using Lockdown.AI.Core.Mummy;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using Lockdown.Weapons;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    // Unity side of a mummy: sight and gunshot hearing, alerting the other mummies, NavMesh movement
    // with spread-out approach lanes, the bite, and the animation and shuffle sound.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(MummyBody))]
    public sealed class MummyAgent : MonoBehaviour, IMummyWorld
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");
        private static readonly int LocomotionState = Animator.StringToHash("Locomotion");
        private static readonly int BiteState = Animator.StringToHash("Bite");
        private static readonly int HitState = Animator.StringToHash("Hit");
        private static readonly List<MummyAgent> Active = new List<MummyAgent>();

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource voice;
        [SerializeField] private AudioSource shuffle;
        [SerializeField] private AudioClip groanSound;
        [SerializeField] private AudioClip biteSound;
        [SerializeField] private Vector3[] patrolRoute = new Vector3[0];
        // Lane used when several mummies chase together, so they spread out instead of queuing.
        [SerializeField, Range(0, 3)] private int lane;

        [Header("Senses")]
        [SerializeField, Min(1f)] private float sightRange = 16f;
        [SerializeField, Range(1f, 360f)] private float fieldOfView = 120f;
        [SerializeField, Min(0f)] private float eyeHeight = 1.6f;
        [SerializeField, Min(0f)] private float playerChestHeight = 1.2f;
        [SerializeField, Min(0f)] private float hearingRange = 18f;
        [SerializeField, Min(0f)] private float alertRadius = 20f;
        [SerializeField] private LayerMask sightLayers = Physics.DefaultRaycastLayers;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 1f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 2.9f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.6f;
        [SerializeField, Min(0.1f)] private float stallTime = 1.2f;
        [SerializeField, Min(0f)] private float laneSpacing = 1.1f;

        [Header("Bite")]
        [SerializeField, Min(0.1f)] private float biteRange = 1.35f;
        [SerializeField, Min(0.1f)] private float biteReach = 1.75f;
        [SerializeField, Min(0f)] private float biteDamage = 15f;
        [SerializeField, Min(0.1f)] private float biteCooldown = 1.25f;
        [SerializeField, Min(0f)] private float biteImpact = 0.3f;
        [SerializeField, Min(0.1f)] private float biteDuration = 0.6f;

        private readonly RaycastHit[] sightHits = new RaycastHit[16];
        private NavMeshAgent agent;
        private MummyBody body;
        private NavMeshPath path;
        private MummyBrain brain;
        private FirstPersonController player;
        private Health playerHealth;
        private FirstPersonWeapon[] weapons = new FirstPersonWeapon[0];
        private int[] shotsSeen = new int[0];
        private float nextPlayerSearch;
        private float stalled;
        private float biteImpactAt = -1f;
        private float biteEndsAt;
        private float nextBite;
        private float nextGroan;
        // A bite or flinch plays over walking; this is when the animator goes back to locomotion.
        private float actionEndsAt = -1f;
        private bool arrived;

        public MummyBrain Brain => brain;
        public MummyState State => brain != null ? brain.State : MummyState.Dormant;
        public bool SeesPlayer { get; private set; }
        public bool IsBiting => Time.time < biteEndsAt;
        public int BitesLanded { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            body = GetComponent<MummyBody>();
            path = new NavMeshPath();
            brain = new MummyBrain(patrolRoute);
        }

        private void OnEnable()
        {
            Active.Add(this);
            body.Downed += OnDowned;
            body.Released += OnReleased;
            body.Risen += OnRisen;
            body.Destroyed += OnDestroyed;
            body.Damaged += OnDamaged;
            if (shuffle != null && shuffle.clip != null) { shuffle.volume = 0f; shuffle.Play(); }
        }

        private void OnDisable()
        {
            Active.Remove(this);
            body.Downed -= OnDowned;
            body.Released -= OnReleased;
            body.Risen -= OnRisen;
            body.Destroyed -= OnDestroyed;
            body.Damaged -= OnDamaged;
            if (shuffle != null) shuffle.Stop();
            Halt();
        }

        private void Start()
        {
            if (!agent.isActiveAndEnabled) return;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            if (NavMesh.SamplePosition(transform.position, out var hit, 1f, filter)) agent.Warp(hit.position);
            // A mummy placed already standing starts on patrol; entombed ones wait for their sarcophagus.
            if (body.Life == MummyLife.Up) brain.Wake(transform.position);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;
            FindPlayer();
            if (!body.IsUp)
            {
                actionEndsAt = -1f;
                SeesPlayer = false;
                UpdateSound(0f);
                if (animator != null && animator.isActiveAndEnabled) animator.SetFloat(SpeedParameter, 0f);
                return;
            }
            var senses = Sense(deltaTime);
            brain.Tick(senses, deltaTime, this);
            if (brain.SpottedThisTick) AlertOthers(brain.LastKnown);
            arrived = Flat(transform.position, brain.Goal) <= arrivalDistance;
            Bite();
            Move(deltaTime);
            Animate();
        }

        private MummySenses Sense(float deltaTime)
        {
            var senses = new MummySenses
            {
                Position = transform.position,
                ArrivedAtGoal = arrived,
                NavigationStalled = stalled > stallTime || (agent.isActiveAndEnabled && agent.hasPath && !agent.pathPending
                    && agent.pathStatus != NavMeshPathStatus.PathComplete)
            };
            if (senses.NavigationStalled) stalled = 0f;
            SeesPlayer = false;
            if (player == null || playerHealth == null || playerHealth.IsDead) return senses;
            Vector3 position = player.transform.position;
            senses.PlayerPosition = position;
            SeesPlayer = CanSee(position);
            senses.SeesPlayer = SeesPlayer;
            // A gunshot gives the shooter's position away to every mummy within earshot.
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i] == null || weapons[i].ShotsFired == shotsSeen[i]) continue;
                shotsSeen[i] = weapons[i].ShotsFired;
                if (Vector3.Distance(transform.position, position) <= hearingRange) { senses.HeardNoise = true; senses.NoisePosition = position; }
            }
            return senses;
        }

        private bool CanSee(Vector3 playerPosition)
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = playerPosition + Vector3.up * playerChestHeight;
            Vector3 offset = target - eye;
            if (offset.sqrMagnitude > sightRange * sightRange) return false;
            Vector3 flat = new Vector3(offset.x, 0f, offset.z);
            // While chasing he keeps track all round; otherwise only what is ahead of him or very close.
            if (brain.State != MummyState.Chase && flat.magnitude > 2.5f && Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f) return false;
            float distance = offset.magnitude;
            int count = Physics.RaycastNonAlloc(eye, offset / distance, sightHits, distance, sightLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = sightHits[i].transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(player.transform) || hit.GetComponentInParent<MummyBody>() != null) continue;
                return false;
            }
            return true;
        }

        private void AlertOthers(Vector3 position)
        {
            foreach (var other in Active)
                if (other != this && other.body.IsUp && Vector3.Distance(other.transform.position, transform.position) <= alertRadius)
                    other.brain.Alert(position);
            if (voice != null && groanSound != null) voice.PlayOneShot(groanSound);
            nextGroan = Time.time + Random.Range(4f, 7f);
        }

        private void Bite()
        {
            if (player == null || playerHealth == null) return;
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (biteImpactAt > 0f && Time.time >= biteImpactAt)
            {
                biteImpactAt = -1f;
                // The bite lands only if the player is still within reach in front of him.
                if (distance <= biteReach && Vector3.Angle(transform.forward, toPlayer) <= 70f && !playerHealth.IsDead)
                {
                    playerHealth.TakeDamage(biteDamage);
                    BitesLanded++;
                    if (voice != null && biteSound != null) voice.PlayOneShot(biteSound);
                }
            }
            if (IsBiting || brain.State != MummyState.Chase || !SeesPlayer || Time.time < nextBite || distance > biteRange) return;
            biteImpactAt = Time.time + biteImpact;
            biteEndsAt = Time.time + biteDuration;
            nextBite = Time.time + biteCooldown;
            PlayAction(BiteState, 0.08f, biteDuration);
        }

        private void Move(float deltaTime)
        {
            if (!agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            agent.speed = brain.State == MummyState.Chase ? chaseSpeed : patrolSpeed;
            if (IsBiting)
            {
                Halt();
                Face(player.transform.position, 720f, deltaTime);
                return;
            }
            agent.updateRotation = true;
            agent.isStopped = false;
            Vector3 goal = brain.Goal;
            if (brain.State == MummyState.Chase && SeesPlayer)
            {
                // Far from the player each mummy takes its own lane, so a group closes in from several sides.
                Vector3 toPlayer = goal - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.magnitude > 4f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, toPlayer.normalized);
                    var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
                    if (NavMesh.SamplePosition(goal + side * ((lane - 1.5f) * laneSpacing), out var laneHit, 1f, filter)) goal = laneHit.position;
                }
                else if (toPlayer.magnitude < biteRange * 0.8f) { Halt(); Face(goal, 540f, deltaTime); return; }
            }
            if (!agent.hasPath || (agent.destination - goal).sqrMagnitude > 0.09f) agent.SetDestination(goal);
            stalled = !arrived && !agent.pathPending && agent.velocity.sqrMagnitude < 0.01f ? stalled + deltaTime : 0f;
        }

        private void PlayAction(int state, float fade, float duration)
        {
            if (animator == null || !animator.isActiveAndEnabled) return;
            animator.CrossFadeInFixedTime(state, fade);
            actionEndsAt = Time.time + duration;
        }

        private void Animate()
        {
            float speed = agent.isActiveAndEnabled ? agent.velocity.magnitude : 0f;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.SetFloat(SpeedParameter, speed, 0.15f, Time.deltaTime);
                if (actionEndsAt > 0f && Time.time >= actionEndsAt)
                {
                    actionEndsAt = -1f;
                    animator.CrossFadeInFixedTime(LocomotionState, 0.15f);
                }
            }
            UpdateSound(speed);
            if (brain.State == MummyState.Chase && Time.time >= nextGroan)
            {
                nextGroan = Time.time + Random.Range(4f, 7f);
                if (voice != null && groanSound != null) voice.PlayOneShot(groanSound, 0.7f);
            }
        }

        private void UpdateSound(float speed)
        {
            if (shuffle == null) return;
            shuffle.volume = 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, chaseSpeed, speed));
            shuffle.pitch = 0.85f + 0.3f * Mathf.InverseLerp(0f, chaseSpeed, speed);
        }

        private void Face(Vector3 point, float degreesPerSecond, float deltaTime)
        {
            agent.updateRotation = false;
            Vector3 look = point - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 1e-3f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(look), degreesPerSecond * deltaTime);
        }

        private void Halt()
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
            stalled = 0f;
        }

        private void FindPlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy) return;
            if (Time.time < nextPlayerSearch) return;
            nextPlayerSearch = Time.time + 1f;
            player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            playerHealth = player.GetComponent<Health>();
            weapons = player.GetComponentsInChildren<FirstPersonWeapon>(true);
            shotsSeen = new int[weapons.Length];
            for (int i = 0; i < weapons.Length; i++) shotsSeen[i] = weapons[i].ShotsFired;
        }

        private void OnDowned()
        {
            brain.KnockDown();
            biteImpactAt = -1f;
            biteEndsAt = 0f;
            Halt();
        }

        private void OnReleased() => brain.Wake(transform.position);

        private void OnRisen() => brain.GetUp(transform.position);

        private void OnDestroyed()
        {
            brain.Kill();
            if (shuffle != null) shuffle.Stop();
            enabled = false;
        }

        // Being shot tells him roughly where the shooter is.
        private void OnDamaged(float amount)
        {
            if (player != null && brain.State != MummyState.Chase) brain.Alert(player.transform.position);
            if (!IsBiting && body.IsUp && body.Health > 0f) PlayAction(HitState, 0.05f, 0.33f);
        }

        public bool TryChooseSearchPoint(Vector3 around, int index, out Vector3 point)
        {
            point = around;
            if (!agent.isActiveAndEnabled) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            // Golden-angle spiral: successive search points cover every side of the last known position.
            for (int attempt = 0; attempt < 6; attempt++)
            {
                int k = index + attempt;
                float angle = k * 2.39996f;
                float radius = 2.5f + (k % 3) * 1.5f;
                Vector3 probe = around + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!NavMesh.SamplePosition(probe, out var hit, 1f, filter)) continue;
                if (!NavMesh.CalculatePath(transform.position, hit.position, filter, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                point = hit.position;
                return true;
            }
            return false;
        }

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}

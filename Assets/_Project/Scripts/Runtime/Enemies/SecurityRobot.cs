using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Enemies
{
    /// <summary>
    /// A small security robot that rolls on its sphere belly. It rests on its charger in the Security room
    /// until the alarm sounds, then powers up and rolls to wherever the alarm reported a noise or a
    /// sighting, and searches the halls around it. When it sees the player it keeps its distance and
    /// fires its eye laser. When the alarm is switched off it rolls home to its charger. Once destroyed
    /// it stays down.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(RobotLaser))]
    public sealed class SecurityRobot : MonoBehaviour
    {
        public enum Mode { Docked, PoweringUp, Hunting, Searching, Attacking, Returning, Destroyed }

        [SerializeField] private Transform belly;
        [SerializeField] private Transform head;
        [SerializeField, Min(0.05f)] private float bellyRadius = 0.35f;
        [SerializeField, Min(1f)] private float sightRange = 12f;
        [SerializeField, Range(10f, 360f)] private float fieldOfView = 160f;
        [SerializeField, Min(0.1f)] private float eyeHeight = 0.76f;
        [SerializeField, Min(0f)] private float powerUpTime = 1f;
        [SerializeField, Min(1f)] private float searchRadius = 8f;
        [SerializeField, Min(1f)] private float keepDistance = 6f;
        [SerializeField, Min(0f)] private float loseSightAfter = 1.5f;
        [SerializeField] private AudioSource hum;
        [SerializeField] private AudioSource powerUp;
        [SerializeField] private AudioSource breakdown;

        private NavMeshAgent agent;
        private Health health;
        private RobotLaser laser;
        private MuseumAlarm alarm;
        private FirstPersonController player;
        private float nextPlayerSearch;
        private Vector3 dockPosition;
        private Quaternion dockRotation;
        private Vector3 target;
        private Vector3 lastSeen;
        private float movesAt;
        private float lastSeenAt;
        private float nextRepath;
        private float rollAngle;

        public Mode State { get; private set; } = Mode.Docked;
        // Out of its charger, so the bay door has to be open for it.
        public bool IsAway => State != Mode.Docked && State != Mode.Destroyed;
        public Vector3 Target => target;
        public Vector3 DockPosition => dockPosition;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            laser = GetComponent<RobotLaser>();
            dockPosition = transform.position;
            dockRotation = transform.rotation;
        }

        private void OnEnable() { health.OnDeath.AddListener(BreakDown); }

        private void Start()
        {
            alarm = MuseumAlarm.Current;
            if (alarm == null) return;
            alarm.Alerted += OnAlert;
            alarm.Silenced += OnSilenced;
            if (alarm.IsOn) OnAlert(alarm.LastAlert);
        }

        private void OnDisable()
        {
            health.OnDeath.RemoveListener(BreakDown);
            laser.Cancel();
        }

        private void OnDestroy()
        {
            if (alarm == null) return;
            alarm.Alerted -= OnAlert;
            alarm.Silenced -= OnSilenced;
        }

        private void OnAlert(Vector3 where)
        {
            if (!isActiveAndEnabled || State == Mode.Destroyed) return;
            target = where;
            if (State == Mode.Docked || State == Mode.Returning)
            {
                State = Mode.PoweringUp;
                movesAt = Time.time + powerUpTime;
                laser.Hunting = true;
                if (powerUp != null && powerUp.clip != null) powerUp.Play();
                if (hum != null && hum.clip != null && !hum.isPlaying) hum.Play();
            }
            else if (State != Mode.PoweringUp && State != Mode.Attacking)
            {
                State = Mode.Hunting;
                Go(target);
            }
        }

        private void OnSilenced()
        {
            if (State == Mode.Destroyed) return;
            laser.Cancel();
            laser.Hunting = false;
            State = Mode.Returning;
            Go(dockPosition);
        }

        private void Update()
        {
            if (State == Mode.Docked || State == Mode.Destroyed || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
            FindPlayer();
            float distance = 0f;
            bool sees = State != Mode.Returning && State != Mode.PoweringUp &&
                PlayerSight.Sees(transform, transform.position + Vector3.up * eyeHeight, transform.forward, sightRange, fieldOfView, player, out distance);
            switch (State)
            {
                case Mode.PoweringUp:
                    if (Time.time >= movesAt) { State = Mode.Hunting; Go(target); }
                    break;
                case Mode.Hunting:
                case Mode.Searching:
                    if (sees) { State = Mode.Attacking; break; }
                    if (Arrived()) { State = Mode.Searching; Go(SearchPoint()); }
                    else if (Time.time >= nextRepath) Go(agent.destination);
                    break;
                case Mode.Attacking:
                    if (sees)
                    {
                        lastSeen = player.transform.position;
                        lastSeenAt = Time.time;
                        Engage(distance);
                    }
                    else if (Time.time - lastSeenAt > loseSightAfter)
                    {
                        laser.Cancel();
                        State = Mode.Hunting;
                        target = lastSeen;
                        Go(target);
                    }
                    break;
                case Mode.Returning:
                    if (Arrived(0.5f)) Dock();
                    else if (Time.time >= nextRepath) Go(dockPosition);
                    break;
            }
            Roll();
        }

        // Close in to laser range, then hold position facing the player and fire.
        private void Engage(float distance)
        {
            if (distance > keepDistance)
            {
                if (Time.time >= nextRepath) Go(lastSeen);
            }
            else if (agent.hasPath) agent.ResetPath();
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 360f * Time.deltaTime);
            laser.TryCharge(player.transform.position + Vector3.up * 1.1f);
        }

        private void Go(Vector3 destination)
        {
            nextRepath = Time.time + 1.5f;
            if (!NavMesh.SamplePosition(destination, out var hit, 2f, agent.areaMask)) return;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }

        private bool Arrived(float slack = 0.6f) =>
            !agent.pathPending && agent.hasPath ? agent.remainingDistance <= agent.stoppingDistance + slack : !agent.pathPending;

        private Vector3 SearchPoint()
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 offset = Random.insideUnitCircle * searchRadius;
                var candidate = target + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out var hit, 1.5f, agent.areaMask)) return hit.position;
            }
            return target;
        }

        private void Dock()
        {
            agent.ResetPath();
            agent.Warp(dockPosition);
            transform.rotation = dockRotation;
            State = Mode.Docked;
            if (hum != null) hum.Stop();
        }

        private void BreakDown()
        {
            State = Mode.Destroyed;
            laser.Shutdown();
            if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
            if (hum != null) hum.Stop();
            if (breakdown != null && breakdown.clip != null) breakdown.Play();
            // It slumps onto its side, head lolling.
            if (head != null) head.localRotation = Quaternion.Euler(55f, 0f, 0f);
            transform.rotation *= Quaternion.Euler(0f, 0f, 35f);
        }

        private void FindPlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy) return;
            if (Time.time < nextPlayerSearch) return;
            nextPlayerSearch = Time.time + 1f;
            player = FindAnyObjectByType<FirstPersonController>();
        }

        // The belly turns with the ground it covers.
        private void Roll()
        {
            if (belly == null) return;
            float speed = Vector3.Dot(agent.velocity, transform.forward);
            rollAngle += speed * Time.deltaTime / bellyRadius * Mathf.Rad2Deg;
            belly.localRotation = Quaternion.AngleAxis(rollAngle, Vector3.right);
        }
    }
}

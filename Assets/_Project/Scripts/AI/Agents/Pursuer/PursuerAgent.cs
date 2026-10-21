using Lockdown.Combat;
using Lockdown.Enemies;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent), typeof(Health), typeof(PursuerNavigation))]
    public sealed class PursuerAgent : MonoBehaviour
    {
        // Preserve the original Patrol/Chase enum values for existing scene data.
        public enum PursuerState { Patrol, Chase, Investigate, Attack, Search, Return, Dead }
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField] private Transform playerTarget;
        [SerializeField] private PursuerSettings settings = new PursuerSettings();
        private NavMeshAgent agent;
        private PursuerNavigation navigation;
        private PursuerPerception perception;
        private PursuerFormation formation;
        private PursuerPresentation presentation;
        private EnemyShooting weapon;
        private Health health;
        private PursuerAttackCycle combat;
        private readonly PursuerInvestigation investigation = new PursuerInvestigation();
        private readonly PursuerSearch search = new PursuerSearch();
        private Vector3 spawn, searchPoint, formationPoint;
        private bool searchingPoint, hasMemory, hasFlank;
        private int patrolIndex;
        private float lastSeenAt = float.NegativeInfinity, nextFormation, retryAt;
        private static readonly ProfilerMarker DecisionMarker = new ProfilerMarker(ProfilerCategory.Scripts, "Pursuer.Decision");
        public bool IsPlayerVisible { get; private set; }
        public PursuerState State { get; private set; } = PursuerState.Patrol;
        public Vector3 LastKnownPlayerPosition { get; private set; }
        public string DecisionReason { get; private set; } = "Following patrol route";
        public PursuerSettings Settings => settings;
        public Transform Target => playerTarget;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            navigation = GetComponent<PursuerNavigation>();
            perception = GetComponent<PursuerPerception>();
            if (perception == null) perception = gameObject.AddComponent<PursuerPerception>();
            formation = GetComponent<PursuerFormation>();
            if (formation == null) formation = gameObject.AddComponent<PursuerFormation>();
            perception.Configure(settings);
            health = GetComponent<Health>();
            weapon = GetComponent<EnemyShooting>();
            presentation = GetComponent<PursuerPresentation>();
            combat = new PursuerAttackCycle(settings);
            spawn = transform.position;
        }
        private void OnEnable()
        {
            if (health != null) health.OnDeath.AddListener(Die);
            if (State != PursuerState.Dead)
            {
                if (perception != null) perception.enabled = true;
                if (formation != null) formation.enabled = true;
            }
        }
        private void Start()
        {
            if (!navigation.BindToNavMesh()) Debug.LogWarning("Pursuer needs a reachable baked NavMesh near its spawn.", this);
        }

        private void Update()
        {
            using (DecisionMarker.Auto()) Decide();
        }
        private void Decide()
        {
            if (Time.deltaTime <= 0f || State == PursuerState.Dead) return;
            if (health.IsDead) { Die(); return; }
            formation.SetTarget(playerTarget);
            bool tracking = State == PursuerState.Chase || State == PursuerState.Attack;
            IsPlayerVisible = perception.CanSee(playerTarget, tracking);
            if (IsPlayerVisible)
            {
                LastKnownPlayerPosition = playerTarget.position;
                lastSeenAt = Time.time; hasMemory = true;
                perception.ClearClue();
                perception.RaiseAlert(LastKnownPlayerPosition);
                if (Time.time >= nextFormation)
                {
                    nextFormation = Time.time + 0.5f;
                    hasFlank = formation.TryFlank(LastKnownPlayerPosition, navigation.CanReach, out formationPoint);
                }
                bool slotReady = !hasFlank || GroundDistance(transform.position, formationPoint) <= 0.7f;
                bool canAttack = slotReady && GroundDistance(transform.position, playerTarget.position) <= settings.attackRange
                    && weapon != null && weapon.CanAttackTarget(playerTarget);
                SetState(canAttack ? PursuerState.Attack : PursuerState.Chase,
                    canAttack ? "Visible target in clear firing range" : hasFlank ? "Moving to a reserved flank" : "Following visible player");
            }
            else
            {
                hasFlank = false;
                if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy
                    || (playerTarget.TryGetComponent<Health>(out var targetHealth) && targetHealth.IsDead)) hasMemory = false;
                if (tracking)
                {
                    if (hasMemory && Time.time - lastSeenAt < settings.sightMemoryDuration)
                        SetState(PursuerState.Chase, "Following last confirmed position");
                    else BeginSearch(LastKnownPlayerPosition);
                }
                // Active pursuit/search is not overwritten by lower-priority sounds or alerts.
                if (State != PursuerState.Chase && State != PursuerState.Attack && State != PursuerState.Search
                    && perception.TryTakeClue(out var clue))
                {
                    investigation.Begin(clue, Time.time, settings.investigateTimeout);
                    SetState(PursuerState.Investigate, "Investigating a noise or ally alert");
                }
                else if (State == PursuerState.Search) perception.ClearClue();
            }
            if (!navigation.IsReady) { combat.Cancel(Time.time); return; }
            if (State != PursuerState.Attack) { combat.Cancel(Time.time); presentation?.SetWarning(false); }
            switch (State)
            {
                case PursuerState.Patrol: Patrol(); break;
                case PursuerState.Return: ReturnToPatrol(); break;
                case PursuerState.Investigate:
                    if (investigation.Finished(GroundDistance(transform.position, investigation.Position) < 0.7f,
                        Time.time, settings.inspectionDuration)) BeginSearch(investigation.Position);
                    else if (GroundDistance(transform.position, investigation.Position) < 0.7f) { navigation.Stop(); LookAround(); }
                    else if (!Move(investigation.Position, settings.patrolSpeed, 0.4f)) BeginSearch(investigation.Position);
                    break;
                case PursuerState.Chase:
                    Vector3 goal = IsPlayerVisible && hasFlank ? formationPoint : LastKnownPlayerPosition;
                    float stop = IsPlayerVisible && hasFlank ? 0.35f : 1.2f;
                    if (GroundDistance(transform.position, goal) <= stop) navigation.Stop();
                    else Move(goal, settings.chaseSpeed, stop);
                    if (IsPlayerVisible && !hasFlank) Face(LastKnownPlayerPosition);
                    break;
                case PursuerState.Attack:
                    navigation.Stop(); Face(LastKnownPlayerPosition);
                    combat.Tick(Time.time, IsPlayerVisible && weapon.CanAttackTarget(playerTarget),
                        () => weapon.TryFireControlled(playerTarget));
                    presentation?.SetWarning(combat.IsAiming);
                    break;
                case PursuerState.Search: Search(); break;
            }
        }
        private bool Move(Vector3 point, float speed, float stop)
        {
            agent.stoppingDistance = stop;
            var status = navigation.Status();
            if (status == PursuerRouteStatus.Blocked && Time.time < retryAt) return false;
            if (Time.time >= retryAt) retryAt = Time.time + 0.5f;
            return navigation.MoveTo(point, speed);
        }
        private void Patrol()
        {
            if (patrolPoints.Length == 0) { navigation.Stop(); return; }
            var point = patrolPoints[patrolIndex];
            if (point == null || GroundDistance(transform.position, point.position) < 0.6f)
            { navigation.Stop(); patrolIndex = (patrolIndex + 1) % patrolPoints.Length; return; }
            if (!Move(point.position, settings.patrolSpeed, 0.35f)) patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
        private void ReturnToPatrol()
        {
            Vector3 point = spawn;
            if (patrolPoints.Length > 0)
            {
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < patrolPoints.Length; i++)
                    if (patrolPoints[i] != null && GroundDistance(transform.position, patrolPoints[i].position) < nearest
                        && navigation.CanReach(patrolPoints[i].position))
                    { nearest = GroundDistance(transform.position, patrolPoints[i].position); patrolIndex = i; point = patrolPoints[i].position; }
            }
            if (GroundDistance(transform.position, point) < 0.7f || !Move(point, settings.patrolSpeed, 0.35f))
                SetState(PursuerState.Patrol, "Patrol route resumed");
        }
        private void BeginSearch(Vector3 center)
        {
            search.Begin(center, Time.time, settings.searchDuration, settings.searchRadius);
            searchingPoint = false; hasMemory = false;
            SetState(PursuerState.Search, "Lost contact; checking reachable nearby positions");
        }
        private void Search()
        {
            if (search.Expired(Time.time)) { SetState(PursuerState.Return, "Search timer expired"); return; }
            if (searchingPoint)
            {
                if (GroundDistance(transform.position, searchPoint) < 0.6f || navigation.Status() == PursuerRouteStatus.Blocked)
                { navigation.Stop(); searchingPoint = false; }
                else { if (!Move(searchPoint, settings.patrolSpeed, 0.35f)) searchingPoint = false; return; }
            }
            if (search.TryNext(navigation.CanReach, out searchPoint)) searchingPoint = Move(searchPoint, settings.patrolSpeed, 0.35f);
            else { navigation.Stop(); LookAround(); }
        }
        private void SetState(PursuerState state, string reason)
        {
            DecisionReason = reason;
            if (State == state) return;
            navigation.Stop(); combat.Cancel(Time.time);
            State = state;
        }
        private void Face(Vector3 point)
        {
            Vector3 direction = point - transform.position; direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
        }
        private void LookAround() { transform.Rotate(0f, 65f * Time.deltaTime, 0f); }
        private static float GroundDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
        private void Die()
        {
            if (State == PursuerState.Dead) return;
            State = PursuerState.Dead; DecisionReason = "Health depleted"; IsPlayerVisible = false;
            hasMemory = false; combat.Cancel(Time.time); navigation.Stop();
            if (formation != null) formation.enabled = false;
            if (perception != null) perception.enabled = false;
            if (weapon != null) weapon.enabled = false;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            agent.enabled = false;
        }
        private void OnDisable()
        {
            if (health != null) health.OnDeath.RemoveListener(Die);
            IsPlayerVisible = false; hasMemory = false; lastSeenAt = float.NegativeInfinity;
            if (navigation != null) navigation.Stop();
            if (perception != null) perception.enabled = false;
            if (formation != null) formation.enabled = false;
            combat?.Cancel(Time.time);
            presentation?.SetWarning(false);
            if (State != PursuerState.Dead) State = PursuerState.Patrol;
        }
    }
}

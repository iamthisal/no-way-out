using Lockdown.Combat;
using System.Collections.Generic;
using Lockdown.Agents.Pursuer;
using UnityEngine;
using Unity.Profiling;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PursuerController))]
    public sealed class PursuerAgent : MonoBehaviour
    {
        // Preserve the original Patrol/Chase enum values for existing scene data.
        public enum PursuerState { Patrol, Chase, Investigate, Attack, Search, Return, Dead, Approach, Arrest, Intercept, AdvanceCover, Caught }
        [SerializeField] private Transform[] patrolPoints = new Transform[0];
        [SerializeField] private Transform playerTarget;
        [SerializeField] private PursuerSettings settings = new PursuerSettings();
        private PursuerController controller;
        private PursuerPerception perception;
        private PursuerFormation formation;
        private PursuerAttackCycle combat;
        private readonly PursuerInvestigation investigation = new PursuerInvestigation();
        private readonly PursuerSearch search = new PursuerSearch();
        private readonly PursuerArrest arrest = new PursuerArrest();
        private readonly PursuerInterception interception = new PursuerInterception();
        private Vector3 spawn, searchPoint, interceptPoint, coverPoint, formationPoint, lostGoal;
        private List<Vector3> lostAreas = new List<Vector3>();
        private bool searchingPoint, hasMemory, hasIntercept, retaliating, hasFlank, wasFleeing, lostPlanReady;
        private int patrolIndex;
        private float lastSeenAt = float.NegativeInfinity, nextPlan, coverUntil, nextCover, nextFormation;
        private float attackedAt = float.NegativeInfinity;
        private float searchPointDeadline, scanUntil;
        private static readonly ProfilerMarker DecisionMarker = new ProfilerMarker(ProfilerCategory.Scripts, "Pursuer.Decision");
        public bool IsPlayerVisible { get; private set; }
        public PursuerState State { get; private set; } = PursuerState.Patrol;
        public Vector3 LastKnownPlayerPosition { get; private set; }
        public string DecisionReason { get; private set; } = "Following patrol route";
        public PursuerSettings Settings => settings;
        public Transform Target => playerTarget;
        public bool IsRetaliating => retaliating;
        public float ArrestProgress => arrest.Progress;
        public Vector3 InterceptPoint => interceptPoint;
        public Vector3 ObservedVelocity => interception.Velocity;
        public Vector3 SearchDestination => searchingPoint ? searchPoint : lostGoal;
        public bool HasReachedSearchArea => search.HasReachedArea;

        private void Awake()
        {
            controller = GetComponent<PursuerController>();
            perception = GetComponent<PursuerPerception>();
            if (perception == null) perception = gameObject.AddComponent<PursuerPerception>();
            formation = GetComponent<PursuerFormation>();
            if (formation == null) formation = gameObject.AddComponent<PursuerFormation>();
            perception.Configure(settings);
            combat = new PursuerAttackCycle(settings);
            spawn = transform.position;
        }
        private void OnEnable()
        {
            if (controller != null) { controller.Died += Die; controller.Attacked += Attacked; }
            if (State != PursuerState.Dead)
            {
                if (perception != null) perception.enabled = true;
                if (formation != null) formation.enabled = true;
            }
        }
        private void Start()
        {
            if (!controller.BindToNavMesh()) Debug.LogWarning("Pursuer needs a reachable baked NavMesh near its spawn.", this);
        }

        private void Update()
        {
            using (DecisionMarker.Auto()) Decide();
        }
        private void Decide()
        {
            if (Time.deltaTime <= 0f || State == PursuerState.Dead) return;
            if (controller.IsDead) { Die(); return; }
            if (!TargetAvailable())
            {
                IsPlayerVisible = false; hasMemory = hasIntercept = false; arrest.Reset(); interception.Reset();
                combat.Cancel(Time.time); controller.SetWarning(false); controller.ShowNotice(null);
                if (controller.TargetCaptured(playerTarget))
                { SetState(PursuerState.Caught, "Thief handcuffed; encounter ended"); controller.Stop(); return; }
                if (IsContactState()) SetState(PursuerState.Return, "Target no longer available");
            }
            formation.SetTarget(playerTarget);
            bool tracking = IsContactState();
            IsPlayerVisible = perception.CanSee(playerTarget, tracking);
            if (IsPlayerVisible)
            {
                LastKnownPlayerPosition = playerTarget.position;
                lastSeenAt = Time.time; hasMemory = true;
                lostPlanReady = false;
                interception.Observe(LastKnownPlayerPosition, Time.time);
                perception.ClearClue();
                perception.RaiseAlert(LastKnownPlayerPosition);
                SelectContactAction();
            }
            else
            {
                interception.LoseSight(); arrest.Reset(); controller.ShowNotice(null);
                if (tracking)
                {
                    if (!lostPlanReady && hasMemory)
                    {
                        Vector3 velocity = interception.IsFresh(Time.time, settings.predictionLifetime) ? interception.Velocity : Vector3.zero;
                        lostAreas = PursuerCornerSearch.Build(transform.position, LastKnownPlayerPosition, velocity,
                            settings, controller.TryRouteLength, controller.IsScreened);
                        lostGoal = lostAreas[0]; lostPlanReady = true;
                    }
                    if (hasMemory && Time.time - lastSeenAt < settings.sightMemoryDuration)
                        SetState(GroundDistance(lostGoal, LastKnownPlayerPosition) > 0.5f ? PursuerState.Intercept : PursuerState.Chase,
                            "Taking a complete route to the likely escape corner or last confirmed position");
                    else BeginPursuitSearch();
                }
                // Active pursuit/search is not overwritten by lower-priority sounds or alerts.
                if (!IsContactState() && State != PursuerState.Search
                    && perception.TryTakeClue(out var clue))
                {
                    investigation.Begin(clue, Time.time, settings.investigateTimeout);
                    SetState(PursuerState.Investigate, "Investigating a noise or ally alert");
                }
                else if (State == PursuerState.Search) perception.ClearClue();
            }
            if (!controller.IsReady) { combat.Cancel(Time.time); return; }
            if (State != PursuerState.Attack && State != PursuerState.AdvanceCover)
            { combat.Cancel(Time.time); controller.SetWarning(false); }
            switch (State)
            {
                case PursuerState.Patrol: Patrol(); break;
                case PursuerState.Return: ReturnToPatrol(); break;
                case PursuerState.Investigate:
                    if (investigation.Finished(GroundDistance(transform.position, investigation.Position) < 0.7f,
                        Time.time, settings.inspectionDuration)) BeginSearch(investigation.Position);
                    else if (GroundDistance(transform.position, investigation.Position) < 0.7f) { controller.Stop(); controller.LookAround(); }
                    else if (!Move(investigation.Position, settings.patrolSpeed, 0.4f)) BeginSearch(investigation.Position);
                    break;
                case PursuerState.Chase:
                    Advance(IsPlayerVisible ? LastKnownPlayerPosition : lostGoal, settings.chaseSpeed,
                        !IsPlayerVisible ? 0.4f : retaliating ? settings.combatStopDistance : settings.arrestRange * 0.75f);
                    break;
                case PursuerState.Approach:
                    controller.ShowNotice("Museum guard approaching — run to avoid arrest");
                    Advance(LastKnownPlayerPosition, settings.approachSpeed, settings.arrestRange * 0.75f);
                    break;
                case PursuerState.Intercept:
                    if (IsPlayerVisible) controller.ShowNotice("Guard is cutting off your escape");
                    Advance(IsPlayerVisible ? interceptPoint : lostGoal, settings.chaseSpeed, 0.35f);
                    break;
                case PursuerState.Arrest:
                    controller.Stop(); controller.Face(LastKnownPlayerPosition);
                    bool complete = arrest.Tick(IsPlayerVisible && controller.InArrestReach(playerTarget, settings.arrestRange), Time.deltaTime, settings.arrestDuration);
                    controller.ShowNotice("Handcuffing — move away to escape!", arrest.Progress);
                    if (complete && controller.TryCapture(playerTarget, settings.arrestRange))
                    { SetState(PursuerState.Caught, "Thief handcuffed; encounter ended"); controller.ShowNotice(null); }
                    break;
                case PursuerState.Attack:
                    controller.ShowNotice("Guard returning fire — break line of sight!");
                    Advance(hasFlank ? formationPoint : LastKnownPlayerPosition, settings.approachSpeed, hasFlank ? 0.35f : settings.combatStopDistance);
                    ShootIfClear();
                    break;
                case PursuerState.AdvanceCover:
                    controller.ShowNotice("Guard advancing through cover");
                    Advance(coverPoint, settings.chaseSpeed, 0.3f); ShootIfClear();
                    break;
                case PursuerState.Search: Search(); break;
            }
        }
        private bool TargetAvailable() => playerTarget != null && playerTarget.gameObject.activeInHierarchy
            && (!playerTarget.TryGetComponent<Health>(out var targetHealth) || !targetHealth.IsDead)
            && !controller.TargetCaptured(playerTarget);
        private bool IsContactState() => State == PursuerState.Chase || State == PursuerState.Attack
            || State == PursuerState.Approach || State == PursuerState.Arrest || State == PursuerState.Intercept || State == PursuerState.AdvanceCover;

        private void SelectContactAction()
        {
            if (retaliating)
            {
                arrest.Reset(); hasIntercept = false;
                if (Time.time >= nextFormation)
                {
                    nextFormation = Time.time + settings.replanInterval;
                    hasFlank = formation.TryFlank(LastKnownPlayerPosition, controller.CanReach, out formationPoint);
                }
                if (State == PursuerState.AdvanceCover && Time.time < coverUntil
                    && controller.RouteStatus() != PursuerRouteStatus.Blocked) return;
                if (Time.time >= nextCover && Time.time - attackedAt < 1.5f)
                {
                    nextCover = Time.time + settings.coverCooldown;
                    if (PursuerAdvanceCover.TryChoose(transform.position, LastKnownPlayerPosition,
                        controller.TryRouteLength, controller.IsScreened, out coverPoint))
                    {
                        coverUntil = Time.time + settings.coverDuration;
                        SetState(PursuerState.AdvanceCover, "Taking brief forward cover after being hit"); return;
                    }
                }
                SetState(GroundDistance(transform.position, LastKnownPlayerPosition) <= settings.attackRange && controller.CanAttack(playerTarget)
                    ? PursuerState.Attack : PursuerState.Chase, "Player attacked this guard; returning fire while closing distance");
                return;
            }
            if (controller.InArrestReach(playerTarget, settings.arrestRange))
            { SetState(PursuerState.Arrest, "Within clear reach; attempting handcuffs"); return; }
            arrest.Reset();
            bool fleeing = interception.Velocity.magnitude >= settings.fleeingSpeed;
            if (Time.time >= nextPlan || (fleeing && !wasFleeing))
            {
                nextPlan = Time.time + settings.replanInterval;
                hasIntercept = interception.TryChoose(transform.position, Time.time, settings, controller.TryRouteLength, out interceptPoint);
            }
            wasFleeing = fleeing;
            if (!fleeing) hasIntercept = false;
            SetState(hasIntercept ? PursuerState.Intercept : fleeing ? PursuerState.Chase : PursuerState.Approach,
                hasIntercept ? "A reachable prediction saves route distance and can beat the fleeing thief" : "Closing for arrest; running alone never authorizes shooting");
        }
        private void Advance(Vector3 goal, float speed, float stop)
        {
            if (GroundDistance(transform.position, goal) <= stop) controller.Stop();
            else Move(goal, speed, stop);
            if (IsPlayerVisible) controller.Face(LastKnownPlayerPosition);
        }
        private void ShootIfClear()
        {
            combat.Tick(Time.time, retaliating && IsPlayerVisible && controller.CanAttack(playerTarget), () => controller.Fire(playerTarget));
            controller.SetWarning(combat.IsAiming);
        }
        private void Attacked(Transform source)
        {
            if (State == PursuerState.Dead || source == null || playerTarget == null || !source.IsChildOf(playerTarget)) return;
            retaliating = true; attackedAt = Time.time; arrest.Reset(); hasIntercept = false; lostPlanReady = false;
            // This is one impact-time snapshot, not permission to read an unseen moving target later.
            LastKnownPlayerPosition = source.position; lastSeenAt = Time.time; hasMemory = true;
            controller.Face(LastKnownPlayerPosition);
            SetState(PursuerState.Chase, "Player hit this guard; armed retaliation authorized");
        }
        private bool Move(Vector3 point, float speed, float stop)
        {
            return controller.MoveTo(point, speed, stop);
        }
        private void Patrol()
        {
            if (patrolPoints.Length == 0) { controller.Stop(); return; }
            var point = patrolPoints[patrolIndex];
            if (point == null || GroundDistance(transform.position, point.position) < 0.6f)
            { controller.Stop(); patrolIndex = (patrolIndex + 1) % patrolPoints.Length; return; }
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
                        && controller.CanReach(patrolPoints[i].position))
                    { nearest = GroundDistance(transform.position, patrolPoints[i].position); patrolIndex = i; point = patrolPoints[i].position; }
            }
            if (GroundDistance(transform.position, point) < 0.7f || !Move(point, settings.patrolSpeed, 0.35f))
                SetState(PursuerState.Patrol, "Patrol route resumed");
        }
        private void BeginSearch(Vector3 center)
        {
            search.Begin(center, Time.time, settings.searchDuration, settings.searchRadius);
            searchingPoint = false; hasMemory = false; scanUntil = 0f;
            SetState(PursuerState.Search, "Lost contact; checking reachable nearby positions");
        }
        private void BeginPursuitSearch()
        {
            if (!lostPlanReady) lostAreas = new List<Vector3> { LastKnownPlayerPosition };
            search.BeginApproach(lostAreas, Time.time, settings.searchDuration, settings.searchRadius, settings.lostTravelTimeout);
            searchingPoint = false; hasMemory = false; scanUntil = 0f;
            SetState(PursuerState.Search, "Travelling to predicted escape areas before starting the inspection timer");
        }
        private void Search()
        {
            if (search.Expired(Time.time))
            {
                SetState(PursuerState.Return, search.HasReachedArea ? "Completed area search; returning to patrol" : "Search routes could not be reached within the travel budget"); return;
            }
            if (Time.time < scanUntil)
            {
                controller.Stop(); controller.LookAround(360f / settings.searchScanDuration);
                DecisionReason = "Scanning the escape corner before checking the next hiding area"; return;
            }
            if (searchingPoint)
            {
                if (GroundDistance(transform.position, searchPoint) < 0.6f)
                {
                    controller.Stop(); searchingPoint = false;
                    search.Arrived(searchPoint, Time.time); scanUntil = Time.time + settings.searchScanDuration; return;
                }
                if (controller.RouteStatus() == PursuerRouteStatus.Blocked || Time.time >= searchPointDeadline)
                { controller.Stop(); searchingPoint = false; }
                else { if (!Move(searchPoint, search.HasReachedArea ? settings.patrolSpeed : settings.chaseSpeed, 0.35f)) searchingPoint = false; return; }
            }
            if (search.TryNext(controller.CanReach, out searchPoint))
            {
                searchPointDeadline = Time.time + (search.HasReachedArea ? settings.searchPointTimeout : settings.lostTravelTimeout);
                searchingPoint = Move(searchPoint, search.HasReachedArea ? settings.patrolSpeed : settings.chaseSpeed, 0.35f);
                DecisionReason = search.HasReachedArea ? "Checking another likely hiding area" : "Closing on the predicted escape area; inspection time has not started";
            }
            else { controller.Stop(); controller.LookAround(); }
        }
        private void SetState(PursuerState state, string reason)
        {
            DecisionReason = reason;
            if (State == state) return;
            controller.Stop(); combat.Cancel(Time.time);
            if (state != PursuerState.Arrest) arrest.Reset();
            State = state;
        }
        private static float GroundDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }
        private void Die()
        {
            if (State == PursuerState.Dead) return;
            State = PursuerState.Dead; DecisionReason = "Health depleted"; IsPlayerVisible = false;
            hasMemory = false; combat.Cancel(Time.time);
            if (formation != null) formation.enabled = false;
            if (perception != null) perception.enabled = false;
            controller.DisableBody();
        }
        private void OnDisable()
        {
            if (controller != null) { controller.Died -= Die; controller.Attacked -= Attacked; }
            IsPlayerVisible = false; hasMemory = false; lastSeenAt = float.NegativeInfinity;
            if (controller != null) controller.Stop();
            if (perception != null) perception.enabled = false;
            if (formation != null) formation.enabled = false;
            combat?.Cancel(Time.time);
            arrest.Reset(); interception.Reset(); hasIntercept = wasFleeing = lostPlanReady = false; scanUntil = 0f;
            if (controller != null) controller.SetWarning(false);
            if (controller != null) controller.ShowNotice(null);
            if (State != PursuerState.Dead) State = PursuerState.Patrol;
        }
    }
}

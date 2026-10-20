using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HoundHearing), typeof(HoundNavigation), typeof(Health))]
    public sealed class HoundAgent : MonoBehaviour
    {
        [SerializeField] private HoundSettings settings;
        [SerializeField] private UnityEvent onStateChanged = new UnityEvent();
        [Header("Runtime information")]
        [SerializeField] private HoundState state;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private float targetScore;

        private HoundHearing hearing;
        private HoundNavigation navigation;
        private Health health;
        private HoundDecision decision;
        private Vector3 home;
        private double nextIdleMove;
        private double nextBindAttempt;
        private int followingClue;
        private int searchStep;
        private bool announcedMissingMesh;

        public HoundState State => decision != null ? decision.State : state;
        public HoundClue CurrentClue => decision?.Target;
        public bool CanBite => isActiveAndEnabled && !health.IsDead
            && (State == HoundState.Track || State == HoundState.Sniff || State == HoundState.Search);
        public HoundNavigation Navigation => navigation;
        public HoundSettings Settings => settings != null ? settings : hearing.Settings;

        private void Awake()
        {
            hearing = GetComponent<HoundHearing>();
            navigation = GetComponent<HoundNavigation>();
            health = GetComponent<Health>();
            if (settings == null) settings = hearing.Settings;
            decision = settings != null ? settings.CreateDecision(hearing.Memory) : new HoundDecision(hearing.Memory);
            navigation.StuckTimeout = settings != null ? settings.stuckTimeout : 2.5f;
        }

        private void OnEnable()
        {
            health.OnDeath.AddListener(Die);
            decision.Reset();
            home = transform.position;
            followingClue = 0;
            nextIdleMove = Time.timeAsDouble + 0.5;
            if (health.IsDead) Die();
        }

        private void OnDisable()
        {
            health.OnDeath.RemoveListener(Die);
            navigation.Stop();
            decision.Reset();
            followingClue = 0;
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f || health.IsDead) return;
            if (!navigation.IsReady)
            {
                if (Time.timeAsDouble < nextBindAttempt) return;
                nextBindAttempt = Time.timeAsDouble + 1;
                if (!navigation.BindToNavMesh())
                {
                    if (!announcedMissingMesh)
                        Debug.LogWarning("Hound needs an enabled agent and a baked NavMesh within 0.65 metres of its feet.", this);
                    announcedMissingMesh = true;
                    return;
                }
                announcedMissingMesh = false;
                home = transform.position;
            }

            HoundRouteStatus route = navigation.Status();
            HoundState previousState = decision.State;
            decision.Tick(transform.position, Time.timeAsDouble, Time.deltaTime, route, navigation.CanReach);
            state = decision.State;
            HoundClue clue = decision.Target;
            targetScore = hearing.Memory.Score(clue, transform.position, Time.timeAsDouble);
            if (clue != null) targetPosition = clue.Sound.Position;
            if (previousState != state)
            {
                navigation.Stop();
                followingClue = 0;
                searchStep = 0;
                nextIdleMove = Time.timeAsDouble;
                onStateChanged.Invoke();
            }

            switch (state)
            {
                case HoundState.Track:
                    if (clue != null && followingClue != clue.Id)
                    {
                        followingClue = clue.Id;
                        navigation.MoveTo(clue.Sound.Position, settings != null ? settings.trackingSpeed : 4.8f);
                    }
                    break;
                case HoundState.Sniff:
                    transform.Rotate(0f, 90f * Time.deltaTime, 0f);
                    break;
                case HoundState.Search:
                    Investigate(route);
                    break;
                case HoundState.Wander:
                    Wander(route);
                    break;
            }
        }

        private void Wander(HoundRouteStatus route)
        {
            if (navigation.IsTravelling) return;
            if (route == HoundRouteStatus.Arrived || route == HoundRouteStatus.Blocked)
            {
                navigation.Stop();
                nextIdleMove = Time.timeAsDouble + (settings != null ? settings.wanderPause : 1.2f);
            }
            if (Time.timeAsDouble < nextIdleMove) return;
            float radius = settings != null ? settings.wanderRadius : 6f;
            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 point = home + new Vector3(offset.x, 0f, offset.y);
                if ((point - transform.position).sqrMagnitude < 1f) continue;
                if (navigation.MoveTo(point, settings != null ? settings.wanderSpeed : 2.4f)) return;
            }
            navigation.Stop();
            nextIdleMove = Time.timeAsDouble + 1;
        }

        private void Investigate(HoundRouteStatus route)
        {
            if (navigation.IsTravelling) return;
            if (route == HoundRouteStatus.Arrived || route == HoundRouteStatus.Blocked)
            {
                navigation.Stop();
                nextIdleMove = Time.timeAsDouble + 0.3;
            }
            if (Time.timeAsDouble < nextIdleMove) return;
            float radius = settings != null ? settings.searchRadius : 2.5f;
            // Bounded radial probes cover different directions instead of repeatedly picking the same point.
            for (int i = 0; i < 4; i++)
            {
                float angle = (++searchStep * 137.5f) * Mathf.Deg2Rad;
                Vector3 point = decision.SearchCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (navigation.MoveTo(point, settings != null ? settings.wanderSpeed : 2.4f)) return;
            }
            navigation.Stop();
            nextIdleMove = Time.timeAsDouble + 0.5;
        }

        private void Die()
        {
            decision.Kill();
            state = HoundState.Dead;
            targetScore = 0;
            navigation.Stop();
            hearing.enabled = false;
            var motor = GetComponent<UnityEngine.AI.NavMeshAgent>();
            motor.enabled = false;
            foreach (Collider body in GetComponentsInChildren<Collider>()) body.enabled = false;
            onStateChanged.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || CurrentClue == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(CurrentClue.Sound.Position, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, CurrentClue.Sound.Position);
        }
    }
}

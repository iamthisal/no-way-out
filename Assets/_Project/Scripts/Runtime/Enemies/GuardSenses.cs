using System;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.Enemies
{
    /// <summary>
    /// The patrolling guard's eyes and ears. While he can see the player a "?" fills, faster up close. When
    /// it is full he whistles, raises the alarm and chases with his cap rifle until he has not seen the
    /// player for a while, then goes back to his patrol. Breaking glass within earshot brings him to look.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WanderingEnemy), typeof(EnemyShooting), typeof(Health))]
    public sealed class GuardSenses : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float sightRange = 10f;
        [SerializeField, Range(10f, 180f)] private float fieldOfView = 110f;
        [SerializeField, Min(0.5f)] private float eyeHeight = 1.6f;
        // Seconds of steady sight that fill the meter, close by and at the edge of his sight range.
        [SerializeField, Min(0.05f)] private float noticeTimeNear = 0.35f;
        [SerializeField, Min(0.05f)] private float noticeTimeFar = 1.4f;
        [SerializeField, Min(0f)] private float suspicionDecay = 0.6f;
        [SerializeField, Min(0f)] private float giveUpAfter = 8f;
        // He has just come on shift: no spotting in the first moments of the level.
        [SerializeField, Min(0f)] private float startGrace = 2f;
        [SerializeField, Min(0f)] private float hearingRange = 30f;
        // How long he looks around where the glass broke before going back to his route.
        [SerializeField, Min(0f)] private float searchTime = 5f;
        [SerializeField] private AudioSource whistle;

        private WanderingEnemy legs;
        private EnemyShooting rifle;
        private Health health;
        private FirstPersonController player;
        private float nextPlayerSearch;
        private float awakeAt;
        private float lastSeenAt;
        private float nextPursuitUpdate;
        private Vector3 lastSeen;
        private Vector3 noise;
        private float searchUntil;
        private float investigationEndsAt;
        private BreakableGlass[] glasses = new BreakableGlass[0];
        private UnityAction[] listeners = new UnityAction[0];
        private GUIStyle markStyle;

        public float Suspicion { get; private set; }
        public bool IsChasing { get; private set; }
        public bool IsInvestigating { get; private set; }
        public bool CanSeePlayer { get; private set; }
        public Vector3 LastSeen => lastSeen;
        public Vector3 Noise => noise;
        public event Action Spotted;

        private void Awake()
        {
            legs = GetComponent<WanderingEnemy>();
            rifle = GetComponent<EnemyShooting>();
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            awakeAt = Time.time + startGrace;
            health.OnDamaged.AddListener(OnShot);
        }

        private void Start()
        {
            glasses = FindObjectsByType<BreakableGlass>(FindObjectsInactive.Include);
            listeners = new UnityAction[glasses.Length];
            for (int i = 0; i < glasses.Length; i++)
            {
                var glass = glasses[i];
                listeners[i] = () => Hear(glass.transform.position);
                glass.OnBroken.AddListener(listeners[i]);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < glasses.Length; i++) if (glasses[i] != null) glasses[i].OnBroken.RemoveListener(listeners[i]);
        }

        // Walk over to a noise and search there, unless he is already chasing the player.
        public void Hear(Vector3 point)
        {
            if (health.IsDead || !isActiveAndEnabled || !legs.isActiveAndEnabled || IsChasing) return;
            if (Vector3.Distance(transform.position, point) > hearingRange) return;
            IsInvestigating = true;
            noise = point;
            searchUntil = 0f;
            investigationEndsAt = Time.time + 30f;
            legs.Pursue(point);
        }

        private void OnDisable()
        {
            health.OnDamaged.RemoveListener(OnShot);
            // Without its eyes the guard's rifle behaves as it did before he had any.
            rifle.HoldFire = false;
        }

        private void Update()
        {
            // A switched-off walker (as in the plain shooting tests) leaves the rifle to itself.
            if (health.IsDead || !legs.isActiveAndEnabled)
            {
                CanSeePlayer = false;
                rifle.HoldFire = false;
                return;
            }
            FindPlayer();
            float distance = 0f;
            CanSeePlayer = Time.time >= awakeAt && SeesPlayer(out distance);
            if (!IsChasing)
            {
                if (CanSeePlayer) Suspicion += Time.deltaTime / Mathf.Lerp(noticeTimeNear, noticeTimeFar, distance / sightRange);
                else Suspicion -= Time.deltaTime * suspicionDecay;
                Suspicion = Mathf.Clamp01(Suspicion);
                if (Suspicion >= 1f) Spot();
                else if (IsInvestigating) Investigate();
            }
            else if (CanSeePlayer)
            {
                lastSeen = player.transform.position;
                lastSeenAt = Time.time;
                if (Time.time >= nextPursuitUpdate) { legs.Pursue(lastSeen); nextPursuitUpdate = Time.time + 0.25f; }
            }
            else if (Time.time - lastSeenAt >= giveUpAfter) GiveUp();
            rifle.HoldFire = !IsChasing;
        }

        private void Investigate()
        {
            Vector3 offset = noise - transform.position;
            offset.y = 0f;
            if (searchUntil <= 0f && (offset.magnitude < 2.5f || Time.time >= investigationEndsAt)) searchUntil = Time.time + searchTime;
            if (searchUntil <= 0f) return;
            // Turn slowly on the spot, looking for whoever broke the glass.
            transform.Rotate(0f, 70f * Time.deltaTime, 0f);
            if (Time.time < searchUntil) return;
            IsInvestigating = false;
            legs.ResumePatrol();
        }

        private void FindPlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy) return;
            if (Time.time < nextPlayerSearch) return;
            nextPlayerSearch = Time.time + 1f;
            player = FindAnyObjectByType<FirstPersonController>();
        }

        private bool SeesPlayer(out float distance) =>
            PlayerSight.Sees(transform, transform.position + Vector3.up * eyeHeight, transform.forward, sightRange, fieldOfView, player, out distance);

        private void Spot()
        {
            IsChasing = true;
            IsInvestigating = false;
            Suspicion = 1f;
            lastSeen = player != null ? player.transform.position : transform.position;
            lastSeenAt = Time.time;
            nextPursuitUpdate = 0f;
            legs.Pursue(lastSeen);
            if (whistle != null && whistle.clip != null) whistle.Play();
            if (MuseumAlarm.Current != null) MuseumAlarm.Current.Raise("The guard spotted you", lastSeen);
            Spotted?.Invoke();
        }

        private void GiveUp()
        {
            IsChasing = false;
            Suspicion = 0f;
            legs.ResumePatrol();
        }

        // Being shot gives the player away at once.
        private void OnShot()
        {
            if (health.IsDead || !legs.isActiveAndEnabled) return;
            FindPlayer();
            if (!IsChasing) Spot();
            else if (player != null) { lastSeen = player.transform.position; lastSeenAt = Time.time; }
        }

        private void OnGUI()
        {
            if (health.IsDead || (!IsChasing && Suspicion <= 0f) || player == null || player.PlayerCamera == null) return;
            Vector3 screen = player.PlayerCamera.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
            if (screen.z <= 0f || screen.z > 30f) return;
            markStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            markStyle.fontSize = IsChasing ? 34 : Mathf.RoundToInt(20f + 12f * Suspicion);
            markStyle.normal.textColor = IsChasing ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.85f, 0.2f, 0.35f + 0.65f * Suspicion);
            GUI.Label(new Rect(screen.x - 30f, Screen.height - screen.y - 30f, 60f, 60f), IsChasing ? "!" : "?", markStyle);
        }
    }
}

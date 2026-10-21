using System;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using UnityEngine;

namespace Lockdown.Enemies
{
    /// <summary>
    /// The patrolling guard's eyes. While he can see the player a "?" fills, faster up close. When it is
    /// full he whistles, raises the alarm and chases with his cap rifle until he has not seen the player
    /// for a while, then goes back to his patrol.
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
        [SerializeField] private AudioSource whistle;

        private WanderingEnemy legs;
        private EnemyShooting rifle;
        private Health health;
        private FirstPersonController player;
        private Health playerHealth;
        private float nextPlayerSearch;
        private float awakeAt;
        private float lastSeenAt;
        private float nextPursuitUpdate;
        private Vector3 lastSeen;
        private GUIStyle markStyle;

        public float Suspicion { get; private set; }
        public bool IsChasing { get; private set; }
        public bool CanSeePlayer { get; private set; }
        public Vector3 LastSeen => lastSeen;
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

        private void FindPlayer()
        {
            if (player != null && player.gameObject.activeInHierarchy) return;
            if (Time.time < nextPlayerSearch) return;
            nextPlayerSearch = Time.time + 1f;
            player = FindAnyObjectByType<FirstPersonController>();
            playerHealth = player != null ? player.GetComponent<Health>() : null;
        }

        private bool SeesPlayer(out float distance)
        {
            distance = 0f;
            if (player == null || !player.gameObject.activeInHierarchy || playerHealth == null || playerHealth.IsDead) return false;
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            // His head or his chest is enough.
            foreach (var point in new[] { player.PlayerCamera != null ? player.PlayerCamera.transform.position : player.transform.position + Vector3.up * 1.6f,
                                          player.transform.position + Vector3.up * 1.0f })
            {
                Vector3 direction = point - eye;
                if (direction.magnitude > sightRange) continue;
                Vector3 flat = direction;
                flat.y = 0f;
                if (Vector3.Angle(flatForward, flat) > fieldOfView * 0.5f) continue;
                if (!ClearLine(eye, direction)) continue;
                distance = direction.magnitude;
                return true;
            }
            return false;
        }

        // The first thing the line meets, other than the guard himself, must be the player.
        private bool ClearLine(Vector3 eye, Vector3 direction)
        {
            float nearest = float.PositiveInfinity;
            Collider first = null;
            foreach (var hit in Physics.RaycastAll(eye, direction.normalized, direction.magnitude + 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform) || hit.distance >= nearest) continue;
                nearest = hit.distance;
                first = hit.collider;
            }
            return first != null && first.GetComponentInParent<FirstPersonController>() == player;
        }

        private void Spot()
        {
            IsChasing = true;
            Suspicion = 1f;
            lastSeen = player != null ? player.transform.position : transform.position;
            lastSeenAt = Time.time;
            nextPursuitUpdate = 0f;
            legs.Pursue(lastSeen);
            if (whistle != null && whistle.clip != null) whistle.Play();
            if (MuseumAlarm.Current != null) MuseumAlarm.Current.Raise("The guard spotted you");
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

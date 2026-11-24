using System;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.Agents.Pursuer
{
    /// <summary>Executes commands from a brain. Contains no perception or behaviour selection.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PursuerNavigation), typeof(Health), typeof(NavMeshAgent))]
    public sealed class PursuerController : MonoBehaviour
    {
        private PursuerNavigation navigation;
        private NavMeshAgent agent;
        private Health health;
        private EnemyShooting weapon;
        private PursuerPresentation presentation;
        private float retryAt;
        public event Action Died;
        public event Action<Transform> Attacked;
        public bool IsReady => navigation != null && navigation.IsReady;
        public bool IsDead => health != null && health.IsDead;

        private void Awake()
        {
            navigation = GetComponent<PursuerNavigation>();
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<Health>();
            weapon = GetComponent<EnemyShooting>();
            presentation = GetComponent<PursuerPresentation>();
        }
        private void OnEnable()
        {
            if (health == null) return;
            health.OnDeath.AddListener(NotifyDeath); health.DamagedBy += NotifyAttack;
        }
        private void OnDisable()
        {
            if (health != null) { health.OnDeath.RemoveListener(NotifyDeath); health.DamagedBy -= NotifyAttack; }
            Stop(); SetWarning(false); ShowNotice(null);
        }
        private void NotifyAttack(Transform source) { if (!IsDead) Attacked?.Invoke(source); }
        private void NotifyDeath() { Died?.Invoke(); }
        public bool BindToNavMesh() => navigation.BindToNavMesh();
        public bool CanReach(Vector3 point) => navigation.CanReach(point);
        public bool TryRouteLength(Vector3 from, Vector3 to, out float length) => navigation.TryRouteLength(from, to, out length);
        public bool IsScreened(Vector3 point, Vector3 threat) => navigation.IsScreened(point, threat);
        public bool TargetCaptured(Transform target) => target != null && target.TryGetComponent<PlayerVitals>(out var vitals) && vitals.IsCaptured;
        public bool InArrestReach(Transform target, float range)
        {
            if (target == null || !target.gameObject.activeInHierarchy || TargetCaptured(target)) return false;
            Vector3 delta = target.position - transform.position;
            return Mathf.Abs(delta.y) < 1f && Vector3.ProjectOnPlane(delta, Vector3.up).magnitude <= range
                && navigation.HasClearLine(transform.position + Vector3.up * 1.2f, target.position + Vector3.up * 1.2f, target);
        }
        public bool TryCapture(Transform target, float range) => InArrestReach(target, range)
            && target.TryGetComponent<PlayerVitals>(out var vitals) && vitals.Capture();
        public void ShowNotice(string text, float progress = -1f) { if (presentation != null) presentation.ShowNotice(text, progress); }
        public PursuerRouteStatus RouteStatus() => navigation.Status();
        public bool MoveTo(Vector3 point, float speed, float stoppingDistance)
        {
            agent.stoppingDistance = stoppingDistance;
            var status = navigation.Status();
            if (status == PursuerRouteStatus.Blocked && Time.time < retryAt) return false;
            if (Time.time >= retryAt) retryAt = Time.time + 0.5f;
            return navigation.MoveTo(point, speed);
        }
        public void Stop() { if (navigation != null) navigation.Stop(); }
        public bool CanAttack(Transform target) => weapon != null && weapon.CanAttackTarget(target);
        public bool Fire(Transform target) => weapon != null && weapon.TryFireControlled(target);
        public void SetWarning(bool value) { if (presentation != null) presentation.SetWarning(value); }
        public void Face(Vector3 point)
        {
            Vector3 direction = point - transform.position; direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 360f * Time.deltaTime);
        }
        public void LookAround(float degreesPerSecond = 65f) { transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f); }
        public void DisableBody()
        {
            Stop(); SetWarning(false); ShowNotice(null);
            if (weapon != null) weapon.enabled = false;
            foreach (var body in GetComponentsInChildren<Collider>()) body.enabled = false;
            agent.enabled = false;
        }
    }
}

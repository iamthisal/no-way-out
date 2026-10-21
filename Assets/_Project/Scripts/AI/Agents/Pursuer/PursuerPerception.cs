using System;
using Lockdown.AI.Core.Hound;
using Lockdown.Combat;
using UnityEngine;

namespace Lockdown.AI.Agents
{
    [DisallowMultipleComponent]
    public sealed class PursuerPerception : MonoBehaviour
    {
        private static event Action<Vector3, Transform, float> Alert;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private PursuerSettings settings = new PursuerSettings();
        private Vector3 clue;
        private float clueAt = float.NegativeInfinity, nextAlert;
        private int cluePriority;
        public void Configure(PursuerSettings value) { settings = value; }
        private void OnEnable() { HoundNoiseBus.NoiseEmitted += Hear; Alert += ReceiveAlert; }
        private void OnDisable() { HoundNoiseBus.NoiseEmitted -= Hear; Alert -= ReceiveAlert; ClearClue(); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Alert = null; }

        public bool CanSee(Transform target, bool tracking)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            var health = target.GetComponent<Health>();
            if (health != null && health.IsDead) return false;
            Vector3 direction = target.position - transform.position;
            if (direction.sqrMagnitude > settings.detectionRange * settings.detectionRange) return false;
            if (!tracking && Vector3.Angle(transform.forward, Vector3.ProjectOnPlane(direction, Vector3.up)) > settings.fieldOfView * 0.5f) return false;
            return ClearLine(transform.position + Vector3.up * 1.2f, target.position + Vector3.up * 1.2f, target);
        }

        private bool ClearLine(Vector3 from, Vector3 to, Transform target)
        {
            Vector3 direction = to - from;
            int count = Physics.RaycastNonAlloc(from, direction.normalized, hits, direction.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!hits[i].transform.IsChildOf(transform) && (target == null || !hits[i].transform.IsChildOf(target))) return false;
            return true;
        }

        private void Hear(HoundSound sound, Transform source)
        {
            if (Time.timeScale <= 0f || !sound.IsValid || sound.EmittedAt > Time.timeAsDouble + 0.1
                || Time.timeAsDouble - sound.EmittedAt > settings.clueLifetime) return;
            if (source != null && (source.IsChildOf(transform) || source.GetComponentInParent<PursuerAgent>() != null)) return;
            float radius = Mathf.Min(settings.hearingRange, sound.Radius);
            if (!ClearLine(transform.position + Vector3.up, sound.Position + Vector3.up, source)) radius *= settings.obstructedHearing;
            if ((sound.Position - transform.position).sqrMagnitude > radius * radius) return;
            int priority = sound.Kind == HoundSoundKind.Gunshot ? 3 : sound.Kind == HoundSoundKind.Distraction ? 2 : 1;
            Remember(sound.Position, priority);
        }

        private void Remember(Vector3 position, int priority)
        {
            if (Time.time - clueAt <= settings.clueLifetime && priority < cluePriority) return;
            clue = position; clueAt = Time.time; cluePriority = priority;
        }
        public bool TryTakeClue(out Vector3 position)
        {
            position = clue;
            bool valid = Time.time - clueAt <= settings.clueLifetime;
            ClearClue(); return valid;
        }
        public void ClearClue() { clueAt = float.NegativeInfinity; cluePriority = 0; }
        public void RaiseAlert(Vector3 position)
        {
            if (Time.time < nextAlert) return;
            nextAlert = Time.time + settings.alertInterval;
            Alert?.Invoke(position, transform, settings.alertRange);
        }
        private void ReceiveAlert(Vector3 position, Transform sender, float radius)
        {
            if (Time.timeScale <= 0f || sender == transform || (position - transform.position).sqrMagnitude > radius * radius) return;
            // Alerts carry a position snapshot and are never rebroadcast by recipients.
            Remember(position, 4);
        }
    }
}

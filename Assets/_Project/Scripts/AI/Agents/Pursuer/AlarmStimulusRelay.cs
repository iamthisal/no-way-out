using System.Collections.Generic;
using Lockdown.AI.Core.Director;
using Lockdown.AI.Core.Hound;
using Lockdown.Enemies;
using Lockdown.World;
using UnityEngine;
using UnityEngine.AI;

namespace Lockdown.AI.Agents
{
    /// <summary>
    /// Turns the museum glass alarm into AI inputs: a loud sound every agent may hear, and one radio
    /// call to the best placed guard. It passes on where the glass broke, never where the player is.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlarmStimulusRelay : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float unansweredLifetime = 30f;
        [SerializeField, Min(0.2f)] private float retryInterval = 2f;
        private readonly List<PursuerAgent> guards = new List<PursuerAgent>();
        private readonly List<AlarmCandidate> facts = new List<AlarmCandidate>();
        private readonly List<Vector3> approaches = new List<Vector3>();
        private GlassAlarm alarm;
        private float raisedAt = float.NegativeInfinity, nextRetry;
        private bool unanswered;

        public Vector3 LatestPosition { get; private set; }
        public PursuerAgent Responder { get; private set; }
        public bool IsRinging => alarm != null && alarm.IsRinging;
        /// <summary>Nobody could go yet. Expires, because an old break is no longer a reason to send a guard.</summary>
        public bool IsUnanswered => unanswered && HasRecentAlarm;
        public bool HasRecentAlarm => Time.time - raisedAt <= unansweredLifetime;

        private void OnEnable() { Bind(); }

        private void OnDisable()
        {
            if (alarm != null) alarm.AlarmRaised -= OnAlarm;
            alarm = null; unanswered = false; Responder = null;
            raisedAt = float.NegativeInfinity;
        }

        private void Bind()
        {
            if (alarm != null) return;
            alarm = FindAnyObjectByType<GlassAlarm>();
            if (alarm != null) alarm.AlarmRaised += OnAlarm;
        }

        private void OnAlarm(Vector3 position)
        {
            LatestPosition = position;
            raisedAt = Time.time;
            unanswered = true;
            HoundNoiseBus.Emit(position, HoundSoundKind.Alarm, alarm.transform);
            TryAnswer();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f || Time.time < nextRetry) return;
            nextRetry = Time.time + retryInterval;
            Bind();
            // A busy guard may have become free, or the player may have opened a door.
            if (IsUnanswered) TryAnswer();
        }

        private void TryAnswer()
        {
            nextRetry = Time.time + retryInterval;
            guards.Clear(); facts.Clear(); approaches.Clear();
            foreach (var guard in FindObjectsByType<PursuerAgent>())
            {
                if (!guard.isActiveAndEnabled) continue;
                bool reachable = TryApproach(guard, LatestPosition, out var approach, out float length);
                guards.Add(guard);
                approaches.Add(approach);
                facts.Add(new AlarmCandidate { Busy = guard.IsBusy, Reachable = reachable, RouteLength = length });
            }
            int choice = AlarmDispatch.Choose(facts);
            if (choice >= 0) Answer(guards[choice], approaches[choice]);
        }

        /// <summary>Sends this guard to the latest alarm if it can walk there.</summary>
        public bool TryAnswerWith(PursuerAgent guard)
        {
            if (guard == null || !HasRecentAlarm || !TryApproach(guard, LatestPosition, out var approach, out _)) return false;
            Answer(guard, approach);
            return true;
        }

        private void Answer(PursuerAgent guard, Vector3 approach)
        {
            guard.ReceiveAlarm(approach);
            Responder = guard;
            unanswered = false;
        }

        // Glass sits inside a display case, off the walkable floor, so find a spot to stand beside it.
        private static bool TryApproach(PursuerAgent guard, Vector3 position, out Vector3 approach, out float length)
        {
            approach = position;
            length = float.PositiveInfinity;
            var body = guard.GetComponent<NavMeshAgent>();
            return body != null && body.isOnNavMesh
                && HoundInvestigator.TryFindApproach(guard.transform.position, position, body.agentTypeID, body.areaMask, out approach)
                && guard.TryRouteLength(approach, out length);
        }
    }
}

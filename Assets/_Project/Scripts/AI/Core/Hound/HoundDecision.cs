using System;
using UnityEngine;

namespace Lockdown.AI.Core.Hound
{
    public enum HoundState { Wander, Track, Sniff, Search, Dead }
    public enum HoundRouteStatus { Idle, Moving, Arrived, Blocked, Unavailable }

    // The decision layer knows sound snapshots and route results, not players, physics or models.
    public sealed class HoundDecision
    {
        private readonly HoundMemory memory;
        private readonly float threshold;
        private readonly float switchRatio;
        private readonly float decisionInterval;
        private readonly float sniffDuration;
        private readonly float searchDuration;
        private double nextDecision;
        private double phaseEndsAt;

        public HoundState State { get; private set; } = HoundState.Wander;
        public int TargetId { get; private set; }
        public HoundClue Target => memory.Find(TargetId);
        public Vector3 SearchCenter { get; private set; }

        public HoundDecision(HoundMemory memory, float threshold = 1.2f, float switchRatio = 1.3f,
            float decisionInterval = 0.5f, float sniffDuration = 3f, float searchDuration = 6f)
        {
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.threshold = Mathf.Max(0.01f, threshold);
            this.switchRatio = Mathf.Max(1f, switchRatio);
            this.decisionInterval = Mathf.Max(0.05f, decisionInterval);
            this.sniffDuration = Mathf.Max(0f, sniffDuration);
            this.searchDuration = Mathf.Max(0f, searchDuration);
        }

        public void Tick(Vector3 position, double now, float deltaTime, HoundRouteStatus route,
            Func<Vector3, bool> canReach)
        {
            if (State == HoundState.Dead || deltaTime <= 0f) return;
            memory.Prune(position, now);
            if (now >= nextDecision)
            {
                nextDecision = now + decisionInterval;
                HoundClue selected = memory.Select(position, now, TargetId, threshold, switchRatio, canReach);
                if (selected != null && selected.Id != TargetId)
                {
                    TargetId = selected.Id;
                    State = HoundState.Track;
                    // An arrival/failure result describes the previous route, not this new target.
                    return;
                }
            }

            if (State == HoundState.Track)
            {
                HoundClue target = Target;
                if (target == null || memory.Score(target, position, now) < threshold
                    || route == HoundRouteStatus.Blocked || route == HoundRouteStatus.Unavailable)
                    BeginSniff(position, now);
                else if (route == HoundRouteStatus.Arrived)
                    BeginSniff(target.Sound.Position, now);
            }
            else if (State == HoundState.Sniff && now >= phaseEndsAt)
            {
                State = HoundState.Search;
                phaseEndsAt = now + searchDuration;
            }
            else if (State == HoundState.Search && now >= phaseEndsAt)
                State = HoundState.Wander;
        }

        private void BeginSniff(Vector3 center, double now)
        {
            if (TargetId != 0) memory.Forget(TargetId);
            TargetId = 0;
            SearchCenter = center;
            State = HoundState.Sniff;
            phaseEndsAt = now + sniffDuration;
        }

        public void Kill()
        {
            memory.Clear();
            TargetId = 0;
            State = HoundState.Dead;
        }

        public void Reset()
        {
            memory.Clear();
            TargetId = 0;
            State = HoundState.Wander;
            nextDecision = 0;
            phaseEndsAt = 0;
        }
    }
}

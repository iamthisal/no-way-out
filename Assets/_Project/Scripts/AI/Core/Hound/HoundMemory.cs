using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.AI.Core.Hound
{
    public sealed class HoundClue
    {
        public int Id { get; }
        public HoundSound Sound { get; }
        internal HoundClue(int id, HoundSound sound) { Id = id; Sound = sound; }
    }

    public sealed class HoundMemory
    {
        private readonly List<HoundClue> clues;
        private readonly List<HoundClue> ranked;
        private readonly int capacity;
        private readonly float decay;
        private readonly float distanceScale;
        private readonly float memoryFloor;
        private readonly float maximumAge;
        private int nextId;

        public int Count => clues.Count;
        public HoundClue Latest => clues.Count > 0 ? clues[clues.Count - 1] : null;
        public float MaximumAge => maximumAge;

        public HoundMemory(int capacity = 64, float decay = 0.55f, float distanceScale = 5f,
            float memoryFloor = 0.08f, float maximumAge = 12f)
        {
            this.capacity = Math.Max(1, Math.Min(256, capacity));
            this.decay = ValidPositive(decay, 0.55f);
            this.distanceScale = ValidPositive(distanceScale, 5f);
            this.memoryFloor = ValidPositive(memoryFloor, 0.08f);
            this.maximumAge = ValidPositive(maximumAge, 12f);
            clues = new List<HoundClue>(this.capacity);
            ranked = new List<HoundClue>(this.capacity);
        }

        public bool Remember(HoundSound sound, double now)
        {
            if (!sound.IsValid || double.IsNaN(now) || double.IsInfinity(now)
                || sound.EmittedAt > now + 0.05 || now - sound.EmittedAt >= maximumAge) return false;
            if (clues.Count == capacity) clues.RemoveAt(0);
            clues.Add(new HoundClue(++nextId, sound));
            return true;
        }

        public float Score(HoundClue clue, Vector3 listener, double now)
        {
            if (clue == null) return 0f;
            double age = Math.Max(0.0, now - clue.Sound.EmittedAt);
            return (float)(clue.Sound.Loudness * Math.Exp(-decay * age)
                / (1f + Vector3.Distance(listener, clue.Sound.Position) / distanceScale));
        }

        public void Prune(Vector3 listener, double now)
        {
            for (int i = clues.Count - 1; i >= 0; i--)
                if (now - clues[i].Sound.EmittedAt >= maximumAge || Score(clues[i], listener, now) < memoryFloor)
                    clues.RemoveAt(i);
        }

        public HoundClue Find(int id)
        {
            for (int i = 0; i < clues.Count; i++) if (clues[i].Id == id) return clues[i];
            return null;
        }

        // Score first; perform expensive route queries only for candidates worth considering.
        public HoundClue Select(Vector3 listener, double now, int currentId, float threshold,
            float switchRatio, Func<Vector3, bool> canReach)
        {
            Prune(listener, now);
            HoundClue current = Find(currentId);
            float currentScore = Score(current, listener, now);
            ranked.Clear();
            ranked.AddRange(clues);
            ranked.Sort((a, b) =>
            {
                int result = Score(b, listener, now).CompareTo(Score(a, listener, now));
                return result != 0 ? result : a.Id.CompareTo(b.Id);
            });
            for (int i = 0; i < ranked.Count; i++)
            {
                HoundClue candidate = ranked[i];
                float score = Score(candidate, listener, now);
                if (score < threshold) break;
                if (current != null && currentScore >= threshold)
                {
                    if (candidate.Id == current.Id) return current;
                    if (score < currentScore * switchRatio) continue;
                }
                if (canReach != null && !canReach(candidate.Sound.Position))
                {
                    Forget(candidate.Id);
                    continue;
                }
                return candidate;
            }
            return currentScore >= threshold ? current : null;
        }

        public void Forget(int id)
        {
            for (int i = clues.Count - 1; i >= 0; i--) if (clues[i].Id == id) clues.RemoveAt(i);
        }

        public void Clear() { clues.Clear(); ranked.Clear(); }

        private static float ValidPositive(float value, float fallback) =>
            HoundSound.IsFinite(value) && value > 0f ? value : fallback;
    }
}

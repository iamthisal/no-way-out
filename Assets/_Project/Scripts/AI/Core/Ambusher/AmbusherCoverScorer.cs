using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.AI.Core.Ambusher
{
    // Facts about one cover/peek pair, gathered by the agent's raycasts and NavMesh queries.
    public struct AmbusherCandidate
    {
        public Vector3 Cover;
        public Vector3 Peek;
        // The player cannot see the crouched ambusher at the cover point.
        public bool HiddenAtCover;
        // Standing at the peek point, he has a clear line to the player.
        public bool ClearShotAtPeek;
        // Straight-line distance from the ambusher, used for the first ranking; the best few are re-scored by walking distance.
        public float TravelDistance;
        // Another ambusher already holds cover nearby.
        public bool Reserved;
        public bool InsideTerritory;
    }

    // A place to look for a player who has taken cover.
    public struct AmbusherFlankCandidate
    {
        public Vector3 Point;
        // Standing here he could see the player's last known position.
        public bool SeesLastKnown;
        public float TravelDistance;
        public bool InsideTerritory;
    }

    // Lower scores are better. The weights follow the prototype's guerrillaPost, rescaled to museum rooms.
    public sealed class AmbusherCoverScorer
    {
        public float IdealRange = 9f;
        public float MaxRange = 16f;
        public float MinFleeRange = 7f;
        // A retreat only needs to gain this much distance; long runs give the player time to close in.
        public float FleeGain = 2.5f;
        public float FlankAngle = 70f;
        public float FlankRange = 5.5f;
        public int PathChecks = 12;

        public float Score(in AmbusherCandidate candidate, Vector3 self, Vector3 threat, bool flee)
        {
            float range = AmbusherBrain.Flat(candidate.Peek, threat);
            float score = candidate.TravelDistance * 0.65f + Mathf.Abs(range - IdealRange);
            if (!candidate.ClearShotAtPeek) score += 65f;
            if (range > MaxRange) score += 80f;
            if (flee && range < Mathf.Max(MinFleeRange, AmbusherBrain.Flat(self, threat) + FleeGain)) score += 180f;
            if (candidate.Reserved) score += 60f;
            if (!candidate.InsideTerritory) score += 120f;
            return score;
        }

        public static bool IsEligible(in AmbusherCandidate candidate, Vector3 currentCover, Vector3? failedPost) =>
            candidate.HiddenAtCover && AmbusherBrain.Flat(candidate.Cover, currentCover) > 1f
            && (!failedPost.HasValue || AmbusherBrain.Flat(candidate.Cover, failedPost.Value) > 2f);

        // pathLength returns the walking distance, or +Infinity when the point is unreachable.
        public bool TryChoose(IReadOnlyList<AmbusherCandidate> candidates, Vector3 self, Vector3 threat, bool flee,
            Vector3 currentCover, Vector3? failedPost, Func<Vector3, float> pathLength, out AmbusherPost post)
        {
            post = default;
            var ranked = new List<(float score, int index)>();
            for (int i = 0; i < candidates.Count; i++)
                if (IsEligible(candidates[i], currentCover, failedPost))
                    ranked.Add((Score(candidates[i], self, threat, flee), i));
            ranked.Sort((a, b) => a.score.CompareTo(b.score));

            // Walls make straight lines misleading: re-score the best few by the real walking distance.
            bool found = false;
            AmbusherCandidate best = default;
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < ranked.Count && i < PathChecks; i++)
            {
                var candidate = candidates[ranked[i].index];
                float length = pathLength(candidate.Cover);
                if (float.IsInfinity(length)) continue;
                float score = ranked[i].score + (length - candidate.TravelDistance) * 0.65f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
                found = true;
            }

            float current = AmbusherBrain.Flat(self, threat);
            // A flight that ends beside him, or no farther from the player, is replaced by the farthest reachable escape.
            if (flee && (!found || AmbusherBrain.Flat(best.Cover, self) < 2f || AmbusherBrain.Flat(best.Cover, threat) < current + FleeGain)
                && TryEscape(candidates, threat, current + FleeGain, pathLength, out var escape))
            {
                post = new AmbusherPost(escape, escape);
                return true;
            }
            if (!found) return false;
            post = new AmbusherPost(best.Cover, best.Peek);
            return true;
        }

        // Prefer a point beside the last known position, about 70 degrees round from the
        // current line of sight, so he sees past the cover the player ducked behind.
        public float ScoreFlank(in AmbusherFlankCandidate candidate, Vector3 self, Vector3 lastKnown, int attempt)
        {
            Vector3 from = self - lastKnown;
            Vector3 to = candidate.Point - lastKnown;
            from.y = 0f;
            to.y = 0f;
            float desired = (attempt % 2 == 0 ? 1f : -1f) * FlankAngle;
            float angle = from.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(from, to, Vector3.up) : desired;
            float score = Mathf.Abs(Mathf.DeltaAngle(angle, desired)) * 0.15f + candidate.TravelDistance * 0.65f
                + Mathf.Abs(to.magnitude - FlankRange);
            if (!candidate.InsideTerritory) score += 120f;
            return score;
        }

        public bool TryChooseFlank(IReadOnlyList<AmbusherFlankCandidate> candidates, Vector3 self, Vector3 lastKnown, int attempt,
            Func<Vector3, float> pathLength, out Vector3 point)
        {
            point = default;
            var ranked = new List<(float score, int index)>();
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].SeesLastKnown && AmbusherBrain.Flat(candidates[i].Point, self) > 1.5f)
                    ranked.Add((ScoreFlank(candidates[i], self, lastKnown, attempt), i));
            ranked.Sort((a, b) => a.score.CompareTo(b.score));
            float bestScore = float.PositiveInfinity;
            for (int i = 0; i < ranked.Count && i < PathChecks; i++)
            {
                var candidate = candidates[ranked[i].index];
                float length = pathLength(candidate.Point);
                if (float.IsInfinity(length)) continue;
                float score = ranked[i].score + (length - candidate.TravelDistance) * 0.65f;
                if (score >= bestScore) continue;
                bestScore = score;
                point = candidate.Point;
            }
            return !float.IsPositiveInfinity(bestScore);
        }

        private bool TryEscape(IReadOnlyList<AmbusherCandidate> candidates, Vector3 threat, float minimum,
            Func<Vector3, float> pathLength, out Vector3 escape)
        {
            escape = default;
            var options = new List<Vector3>();
            foreach (var candidate in candidates)
                if (candidate.InsideTerritory && AmbusherBrain.Flat(candidate.Cover, threat) > minimum) options.Add(candidate.Cover);
            options.Sort((a, b) => AmbusherBrain.Flat(b, threat).CompareTo(AmbusherBrain.Flat(a, threat)));
            for (int i = 0; i < options.Count && i < PathChecks; i++)
            {
                if (float.IsInfinity(pathLength(options[i]))) continue;
                escape = options[i];
                return true;
            }
            return false;
        }
    }
}

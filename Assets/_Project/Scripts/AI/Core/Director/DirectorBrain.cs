using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.AI.Core.Director
{
    /// <summary>
    /// Utility-based director. It decides when, what and where to spawn. It never tells an enemy
    /// where the player is, and it never moves an enemy that already exists.
    /// </summary>
    public sealed class DirectorBrain
    {
        private readonly DirectorTuning tuning;
        private readonly DirectorIntensity intensity;
        private readonly DirectorPacing pacing;
        private readonly List<SpawnCandidate> candidates = new List<SpawnCandidate>();
        private float sinceIntensity, sinceDecision;

        public DirectorTuning Tuning => tuning;
        public float Stress { get; private set; }
        public float Intensity => intensity.Value;
        public DirectorPhase Phase => pacing.Phase;
        public float Target { get; private set; }
        public float Deficit => Phase == DirectorPhase.BuildUp ? Target - Intensity : 0f;
        public float Cooldown { get; private set; }

        public DirectorBrain(DirectorTuning tuning)
        {
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            intensity = new DirectorIntensity(tuning);
            pacing = new DirectorPacing(tuning);
            Cooldown = tuning.openingGrace;
        }

        /// <summary>Advances the internal state. Returns a decision on decision ticks, otherwise null.</summary>
        public DirectorDecision Tick(in DirectorObservation observation, float deltaTime, IDirectorWorld world)
        {
            if (!observation.CanAct || deltaTime <= 0f) return null;
            Cooldown = Mathf.Max(0f, Cooldown - deltaTime);
            sinceIntensity += deltaTime;
            if (sinceIntensity >= tuning.intensityInterval)
            {
                Stress = DirectorStress.Compute(observation, tuning);
                intensity.Tick(Stress, sinceIntensity);
                pacing.Tick(intensity.Value, sinceIntensity);
                sinceIntensity = 0f;
            }
            Target = pacing.Target(observation.LootFraction);
            sinceDecision += deltaTime;
            if (sinceDecision < tuning.decisionInterval) return null;
            sinceDecision = 0f;
            return Decide(observation, world);
        }

        /// <summary>Call only after an enemy really appeared; a failed spawn may be retried next tick.</summary>
        public void ConfirmSpawn() { Cooldown = tuning.spawnCooldown; }

        /// <summary>Whether another enemy may enter for a reason of its own, such as dogs answering broken glass.</summary>
        public bool Permits(SpawnKind kind, in EnemyCensus census) =>
            Phase == DirectorPhase.BuildUp && SpawnLimits.HasRoom(kind, census, tuning);

        private DirectorDecision Decide(in DirectorObservation observation, IDirectorWorld world)
        {
            var decision = new DirectorDecision { WaitUtility = tuning.waitUtility };
            if (Phase != DirectorPhase.BuildUp) { decision.Reason = Phase + ": no new spawns"; return decision; }
            if (Cooldown > 0f) { decision.Reason = "Cooling down"; return decision; }
            if (observation.Census.Total >= tuning.maximumTotal) { decision.Reason = "Museum is full"; return decision; }
            if (Deficit <= 0f && !observation.AlarmUnanswered) { decision.Reason = "Tension already meets the target"; return decision; }

            candidates.Clear();
            world.CollectCandidates(candidates);
            foreach (var candidate in candidates)
            {
                float point = SpawnPointScorer.Score(candidate, tuning);
                foreach (var kind in SpawnKinds.All)
                {
                    if (!SpawnLimits.HasRoom(kind, observation.Census, tuning)) continue;
                    if (SpawnPointFilter.Check(candidate, kind, observation.HealthFraction, tuning) != SpawnRejection.None) continue;
                    Rank(decision.Top, new SpawnOption
                    {
                        Kind = kind, PointId = candidate.PointId, RoomId = candidate.RoomId, Position = candidate.Position,
                        Utility = SpawnUtility.Compute(kind, Deficit, SpawnFit.Score(kind, candidate.Zone, observation),
                            point, observation, tuning)
                    });
                }
            }
            if (decision.Top.Count == 0) { decision.Reason = "No hidden, reachable spawn point"; return decision; }
            if (decision.Top[0].Utility <= tuning.waitUtility) { decision.Reason = "Waiting is worth more"; return decision; }
            decision.Spawn = true;
            decision.Choice = decision.Top[0];
            decision.Reason = observation.AlarmUnanswered && decision.Choice.Kind == SpawnKind.Guard
                ? "Answering the alarm" : "Raising tension towards the target";
            return decision;
        }

        private static void Rank(List<SpawnOption> top, SpawnOption option)
        {
            int index = top.Count;
            while (index > 0 && option.Utility > top[index - 1].Utility) index--;
            if (index >= 3) return;
            top.Insert(index, option);
            if (top.Count > 3) top.RemoveAt(3);
        }
    }
}

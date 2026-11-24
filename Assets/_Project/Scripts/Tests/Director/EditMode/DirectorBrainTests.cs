using System.Collections.Generic;
using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class DirectorBrainTests
    {
        private sealed class FakeWorld : IDirectorWorld
        {
            public readonly List<SpawnCandidate> Points = new List<SpawnCandidate>();
            public int Queries;
            public void CollectCandidates(List<SpawnCandidate> results) { Queries++; results.AddRange(Points); }

            public FakeWorld With(int id, bool visible = false, bool reachable = true, float alarm = float.PositiveInfinity)
            {
                Points.Add(new SpawnCandidate
                {
                    PointId = id, RoomId = "Room" + id, Allowed = SpawnKindSet.All, Zone = SpawnZone.OpenGallery, InView = visible,
                    Reachable = reachable, ClearSpace = true, RouteLength = 20f, SecondsSinceUsed = float.PositiveInfinity, AlarmDistance = alarm
                });
                return this;
            }
        }

        private static DirectorTuning NoGrace() => new DirectorTuning { openingGrace = 0f };
        private static DirectorObservation Calm() => new DirectorObservation { CanAct = true, HealthFraction = 1f, LootFraction = 0.6f };

        private static DirectorDecision Run(DirectorBrain brain, DirectorObservation seen, IDirectorWorld world, float seconds)
        {
            DirectorDecision last = null;
            for (float time = 0f; time < seconds; time += 0.25f) last = brain.Tick(seen, 0.25f, world) ?? last;
            return last;
        }

        [Test]
        public void ACalmPlayerWithLootGetsAGuardAtAHiddenReachablePoint()
        {
            var world = new FakeWorld().With(1, visible: true).With(2, reachable: false).With(3);
            var decision = Run(new DirectorBrain(NoGrace()), Calm(), world, 2f);
            Assert.That(decision.Spawn, Is.True);
            Assert.That(decision.Choice.Kind, Is.EqualTo(SpawnKind.Guard));
            Assert.That(decision.Choice.PointId, Is.EqualTo(3));
        }

        [Test]
        public void NothingHappensDuringTheOpeningGrace()
        {
            var world = new FakeWorld().With(1);
            var brain = new DirectorBrain(new DirectorTuning());
            Assert.That(Run(brain, Calm(), world, 18f).Spawn, Is.False);
            Assert.That(Run(brain, Calm(), world, 6f).Spawn, Is.True);
        }

        [Test]
        public void PausedOrFinishedGamesNeverDecide()
        {
            var world = new FakeWorld().With(1);
            Assert.That(Run(new DirectorBrain(NoGrace()), new DirectorObservation { HealthFraction = 1f }, world, 10f), Is.Null);
            Assert.That(world.Queries, Is.Zero);
        }

        [Test]
        public void APeakAndTheRelaxAfterItNeverSpawn()
        {
            var world = new FakeWorld().With(1);
            var brain = new DirectorBrain(NoGrace());
            var fight = new DirectorObservation { CanAct = true, RecentDamage = 1f, EngagedEnemies = 4, Seen = 1f, HealthFraction = 0.2f, AlarmUnanswered = true };
            Run(brain, fight, world, 4f);
            Assert.That(brain.Phase, Is.EqualTo(DirectorPhase.Peak));
            int before = world.Queries;
            Assert.That(Run(brain, fight, world, 4f).Spawn, Is.False);
            Run(brain, fight, world, 20f);
            Assert.That(brain.Phase, Is.EqualTo(DirectorPhase.Relax));
            Assert.That(Run(brain, fight, world, 10f).Spawn, Is.False);
            Assert.That(world.Queries, Is.EqualTo(before), "No world queries while spawning is off.");
        }

        [Test]
        public void SpawningWaitsForTheCooldownButAFailedSpawnDoesNot()
        {
            var world = new FakeWorld().With(1);
            var brain = new DirectorBrain(NoGrace());
            Assert.That(Run(brain, Calm(), world, 2f).Spawn, Is.True);
            Assert.That(Run(brain, Calm(), world, 2f).Spawn, Is.True, "Unconfirmed spawns may be retried.");
            brain.ConfirmSpawn();
            Assert.That(Run(brain, Calm(), world, 8f).Spawn, Is.False);
            Assert.That(Run(brain, Calm(), world, 4f).Spawn, Is.True);
        }

        [Test]
        public void CapsRemoveTypesFromTheOptions()
        {
            var seen = Calm();
            seen.Census = new EnemyCensus { Guards = 2 };
            var decision = Run(new DirectorBrain(NoGrace()), seen, new FakeWorld().With(1), 2f);
            foreach (var option in decision.Top) Assert.That(option.Kind, Is.Not.EqualTo(SpawnKind.Guard));
            seen.Census = new EnemyCensus { Guards = 2, Hounds = 4 };
            Assert.That(Run(new DirectorBrain(NoGrace()), seen, new FakeWorld().With(1), 2f).Spawn, Is.False);
        }

        [Test]
        public void NoUsablePointMeansWaiting()
        {
            var decision = Run(new DirectorBrain(NoGrace()), Calm(), new FakeWorld().With(1, visible: true), 2f);
            Assert.That(decision.Spawn, Is.False);
            Assert.That(decision.Top, Is.Empty);
        }

        [Test]
        public void AnUnansweredAlarmBringsAGuardFromThePointNearestToIt()
        {
            var seen = new DirectorObservation { CanAct = true, HealthFraction = 1f, AlarmUnanswered = true, Noise = 1f };
            var decision = Run(new DirectorBrain(NoGrace()), seen, new FakeWorld().With(1, alarm: 25f).With(2, alarm: 4f), 2f);
            Assert.That(decision.Choice.Kind, Is.EqualTo(SpawnKind.Guard));
            Assert.That(decision.Choice.PointId, Is.EqualTo(2));
        }

        [Test]
        public void DecisionsKeepTheBestThreeOptionsInOrder()
        {
            var decision = Run(new DirectorBrain(NoGrace()), Calm(), new FakeWorld().With(1).With(2), 2f);
            Assert.That(decision.Top.Count, Is.EqualTo(3));
            Assert.That(decision.Top[0].Utility, Is.GreaterThanOrEqualTo(decision.Top[1].Utility));
            Assert.That(decision.Top[1].Utility, Is.GreaterThanOrEqualTo(decision.Top[2].Utility));
        }

        [Test]
        public void OtherSystemsMayOnlyAddEnemiesInBuildUpAndUnderTheCaps()
        {
            var brain = new DirectorBrain(NoGrace());
            Assert.That(brain.Permits(SpawnKind.Hound, new EnemyCensus()), Is.True);
            Assert.That(brain.Permits(SpawnKind.Hound, new EnemyCensus { Hounds = 4 }), Is.False);
            Run(brain, new DirectorObservation { CanAct = true, RecentDamage = 1f, EngagedEnemies = 4, Seen = 1f }, new FakeWorld(), 4f);
            Assert.That(brain.Permits(SpawnKind.Hound, new EnemyCensus()), Is.False);
        }
    }
}

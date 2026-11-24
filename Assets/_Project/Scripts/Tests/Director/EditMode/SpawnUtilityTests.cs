using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class SpawnUtilityTests
    {
        private readonly DirectorTuning tuning = new DirectorTuning();

        [Test]
        public void NoisyPlayersAttractHoundsAndQuietOnesDoNot()
        {
            Assert.That(SpawnFit.Score(SpawnKind.Hound, SpawnZone.None, new DirectorObservation()), Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(SpawnFit.Score(SpawnKind.Hound, SpawnZone.None, new DirectorObservation { Noise = 1f }), Is.EqualTo(1f));
        }

        [Test]
        public void ArchersPreferCorridorsAndGuardsPreferOpenGalleries()
        {
            var seen = new DirectorObservation();
            Assert.That(SpawnFit.Score(SpawnKind.Archer, SpawnZone.Corridor, seen),
                Is.GreaterThan(SpawnFit.Score(SpawnKind.Archer, SpawnZone.OpenGallery, seen)));
            Assert.That(SpawnFit.Score(SpawnKind.Guard, SpawnZone.OpenGallery, seen),
                Is.GreaterThan(SpawnFit.Score(SpawnKind.Guard, SpawnZone.Corridor, seen)));
        }

        [Test]
        public void TheWorkedExampleFromTheDesignHolds()
        {
            // A quiet, unhurt player carrying 60% of the loot in an empty museum: the deficit is 0.53.
            var seen = new DirectorObservation { HealthFraction = 1f, LootFraction = 0.6f };
            float guard = SpawnUtility.Compute(SpawnKind.Guard, 0.53f, SpawnFit.Score(SpawnKind.Guard, SpawnZone.OpenGallery, seen), 0.8f, seen, tuning);
            float hound = SpawnUtility.Compute(SpawnKind.Hound, 0.53f, SpawnFit.Score(SpawnKind.Hound, SpawnZone.OpenGallery, seen), 0.8f, seen, tuning);
            Assert.That(guard, Is.EqualTo(0.44f).Within(0.01f));
            Assert.That(hound, Is.EqualTo(0.27f).Within(0.01f));
            Assert.That(hound, Is.GreaterThan(tuning.waitUtility));
        }

        [Test]
        public void AFullerMuseumMakesAnotherSpawnWorthLess()
        {
            var empty = new DirectorObservation();
            var busy = new DirectorObservation { Census = new EnemyCensus { Hounds = 3 } };
            Assert.That(SpawnUtility.Compute(SpawnKind.Guard, 0.5f, 0.9f, 0.8f, busy, tuning),
                Is.EqualTo(0.5f * SpawnUtility.Compute(SpawnKind.Guard, 0.5f, 0.9f, 0.8f, empty, tuning)).Within(0.001f));
        }

        [Test]
        public void WithoutADeficitOnlyAnUnansweredAlarmIsWorthASpawn()
        {
            Assert.That(SpawnUtility.Compute(SpawnKind.Guard, 0f, 1f, 1f, new DirectorObservation(), tuning), Is.Zero);
            var alarm = new DirectorObservation { AlarmUnanswered = true };
            Assert.That(SpawnUtility.Compute(SpawnKind.Guard, 0f, 1f, 0.5f, alarm, tuning), Is.GreaterThan(tuning.waitUtility));
            Assert.That(SpawnUtility.Compute(SpawnKind.Hound, 0f, 1f, 1f, alarm, tuning), Is.Zero);
        }
    }
}

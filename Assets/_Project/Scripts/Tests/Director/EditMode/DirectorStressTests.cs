using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class DirectorStressTests
    {
        private readonly DirectorTuning tuning = new DirectorTuning();

        [Test]
        public void WeightsSumToOneSoFullPressureIsExactlyOne()
        {
            Assert.That(tuning.damageWeight + tuning.injuryWeight + tuning.engagedWeight + tuning.seenWeight, Is.EqualTo(1f).Within(0.001f));
            var worst = new DirectorObservation { RecentDamage = 1f, HealthFraction = 0f, EngagedEnemies = 9, Seen = 1f };
            Assert.That(DirectorStress.Compute(worst, tuning), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void AnUnseenHealthyPlayerHasNoStress()
        {
            Assert.That(DirectorStress.Compute(new DirectorObservation { HealthFraction = 1f, Noise = 1f }, tuning), Is.Zero);
        }

        [Test]
        public void RecentDamageMattersMoreThanBeingSeen()
        {
            float damage = DirectorStress.Compute(new DirectorObservation { HealthFraction = 1f, RecentDamage = 1f }, tuning);
            float seen = DirectorStress.Compute(new DirectorObservation { HealthFraction = 1f, Seen = 1f }, tuning);
            Assert.That(damage, Is.GreaterThan(seen));
        }
    }
}

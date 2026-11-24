using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class DespawnRuleTests
    {
        private readonly DirectorTuning tuning = new DirectorTuning();

        [Test]
        public void OnlyFarIdleLongUnseenEnemiesMayLeave()
        {
            Assert.That(DespawnRule.Allows(DirectorPhase.BuildUp, 50f, 25f, false, tuning), Is.True);
            Assert.That(DespawnRule.Allows(DirectorPhase.Relax, 50f, 25f, false, tuning), Is.True);
            Assert.That(DespawnRule.Allows(DirectorPhase.Peak, 50f, 25f, false, tuning), Is.False);
            Assert.That(DespawnRule.Allows(DirectorPhase.BuildUp, 40f, 25f, false, tuning), Is.False);
            Assert.That(DespawnRule.Allows(DirectorPhase.BuildUp, 50f, 10f, false, tuning), Is.False);
            Assert.That(DespawnRule.Allows(DirectorPhase.BuildUp, 50f, 25f, true, tuning), Is.False);
        }
    }
}

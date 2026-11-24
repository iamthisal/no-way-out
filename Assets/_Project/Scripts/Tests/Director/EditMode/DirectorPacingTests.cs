using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class DirectorPacingTests
    {
        private readonly DirectorTuning tuning = new DirectorTuning();

        [Test]
        public void HighIntensityStartsThePeakAndALullStartsRelax()
        {
            var pacing = new DirectorPacing(tuning);
            pacing.Tick(0.74f, 1f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.BuildUp));
            pacing.Tick(0.75f, 1f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.Peak));
            pacing.Tick(0.59f, 1f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.Relax));
        }

        [Test]
        public void APeakCannotLastForever()
        {
            var pacing = new DirectorPacing(tuning);
            pacing.Tick(1f, 1f);
            pacing.Tick(1f, 19.5f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.Peak));
            pacing.Tick(1f, 1f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.Relax));
        }

        [Test]
        public void RelaxNeedsBothTimeAndCalm()
        {
            var pacing = new DirectorPacing(tuning);
            pacing.Tick(1f, 1f); pacing.Tick(0.5f, 1f);
            pacing.Tick(0.1f, 24f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.Relax), "Calm alone is not enough.");
            pacing.Tick(0.5f, 10f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.Relax), "Time alone is not enough.");
            pacing.Tick(0.29f, 1f);
            Assert.That(pacing.Phase, Is.EqualTo(DirectorPhase.BuildUp));
        }

        [Test]
        public void OnlyBuildUpHasATargetAndItGrowsWithLoot()
        {
            var pacing = new DirectorPacing(tuning);
            Assert.That(pacing.Target(0f), Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(pacing.Target(0.6f), Is.EqualTo(0.53f).Within(0.001f));
            pacing.Tick(1f, 1f);
            Assert.That(pacing.Target(1f), Is.Zero);
        }
    }
}

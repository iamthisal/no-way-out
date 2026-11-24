using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class DirectorIntensityTests
    {
        [Test]
        public void RisesFastAndFallsSlowly()
        {
            var intensity = new DirectorIntensity(new DirectorTuning());
            for (int i = 0; i < 8; i++) intensity.Tick(1f, 0.25f);
            float peak = intensity.Value;
            Assert.That(peak, Is.GreaterThan(0.9f), "Two seconds of full stress should nearly fill the intensity.");
            for (int i = 0; i < 8; i++) intensity.Tick(0f, 0.25f);
            Assert.That(intensity.Value, Is.GreaterThan(peak * 0.8f), "Two quiet seconds should barely lower it.");
        }

        [Test]
        public void TheResultDoesNotDependOnTheTickLength()
        {
            var fine = new DirectorIntensity(new DirectorTuning());
            var coarse = new DirectorIntensity(new DirectorTuning());
            for (int i = 0; i < 20; i++) fine.Tick(0.8f, 0.05f);
            coarse.Tick(0.8f, 1f);
            Assert.That(fine.Value, Is.EqualTo(coarse.Value).Within(0.001f));
        }
    }
}

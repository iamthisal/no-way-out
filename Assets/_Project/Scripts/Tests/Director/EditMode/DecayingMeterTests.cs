using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class DecayingMeterTests
    {
        [Test]
        public void HitsAccumulateClampAndHalveEveryHalfLife()
        {
            var meter = new DecayingMeter(5f);
            meter.Add(0.5f); meter.Add(0.3f);
            Assert.That(meter.Value, Is.EqualTo(0.8f).Within(0.001f));
            meter.Add(9f);
            Assert.That(meter.Value, Is.EqualTo(1f));
            meter.Tick(5f);
            Assert.That(meter.Value, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void RaisingToALevelNeverAccumulatesOrLowersTheSignal()
        {
            var meter = new DecayingMeter(5f);
            meter.RaiseTo(0.6f); meter.RaiseTo(0.6f); meter.RaiseTo(0.2f);
            Assert.That(meter.Value, Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void InvalidInputIsIgnored()
        {
            var meter = new DecayingMeter(5f);
            meter.Add(float.NaN); meter.Add(-1f); meter.RaiseTo(float.NaN); meter.Tick(-1f);
            Assert.That(meter.Value, Is.Zero);
        }
    }
}

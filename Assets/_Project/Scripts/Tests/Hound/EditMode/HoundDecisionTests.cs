using Lockdown.AI.Core.Hound;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests.Hound
{
    public sealed class HoundDecisionTests
    {
        private HoundMemory memory;
        private HoundDecision decision;

        [SetUp]
        public void Setup() { memory = new HoundMemory(); decision = new HoundDecision(memory); }

        private void Emit(double now = 0, float loudness = 6f, Vector3 position = default) =>
            memory.Remember(new HoundSound(position, HoundSoundKind.Distraction, loudness, 24, now), now);

        private void Tick(double now, HoundRouteStatus route = HoundRouteStatus.Moving) =>
            decision.Tick(Vector3.zero, now, 0.02f, route, _ => true);

        [Test]
        public void NoSoundMeansWander()
        {
            Tick(0);
            Assert.That(decision.State, Is.EqualTo(HoundState.Wander));
        }

        [Test]
        public void FreshSoundStartsTrackingAndIgnoresOldRouteArrival()
        {
            Emit();
            Tick(0, HoundRouteStatus.Arrived);
            Assert.That(decision.State, Is.EqualTo(HoundState.Track));
            Assert.That(decision.Target.Sound.Position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ArrivalStartsSniffThenBoundedSearchThenWander()
        {
            Emit();
            Tick(0);
            Tick(0.1, HoundRouteStatus.Arrived);
            Assert.That(decision.State, Is.EqualTo(HoundState.Sniff));
            Assert.That(memory.Count, Is.Zero);
            Tick(3.2);
            Assert.That(decision.State, Is.EqualTo(HoundState.Search));
            Tick(9.3);
            Assert.That(decision.State, Is.EqualTo(HoundState.Wander));
        }

        [Test]
        public void BlockedRouteForgetsClueInsteadOfRetryingForever()
        {
            Emit(position: Vector3.right * 2);
            Tick(0);
            Tick(0.1, HoundRouteStatus.Blocked);
            Assert.That(decision.State, Is.EqualTo(HoundState.Sniff));
            Assert.That(decision.TargetId, Is.Zero);
            Assert.That(memory.Count, Is.Zero);
        }

        [Test]
        public void DecayedTargetStopsTracking()
        {
            Emit();
            Tick(0);
            Tick(4);
            Assert.That(decision.State, Is.EqualTo(HoundState.Sniff));
        }

        [Test]
        public void NewSoundInterruptsLocalSearch()
        {
            Emit();
            Tick(0);
            Tick(0.1, HoundRouteStatus.Arrived);
            Tick(3.2);
            Emit(4, position: Vector3.forward * 3);
            Tick(4);
            Assert.That(decision.State, Is.EqualTo(HoundState.Track));
            Assert.That(decision.Target.Sound.Position, Is.EqualTo(Vector3.forward * 3));
        }

        [Test]
        public void DecisionsAreThrottled()
        {
            Emit();
            Tick(0);
            int initial = decision.TargetId;
            Emit(0.1, 12f);
            Tick(0.2);
            Assert.That(decision.TargetId, Is.EqualTo(initial));
            Tick(0.5);
            Assert.That(decision.TargetId, Is.Not.EqualTo(initial));
        }

        [Test]
        public void DeathClearsMemoryAndCannotBeInterrupted()
        {
            Emit();
            Tick(0);
            decision.Kill();
            Assert.That(memory.Count, Is.Zero);
            Emit(1);
            Tick(1);
            Assert.That(decision.State, Is.EqualTo(HoundState.Dead));
            decision.Reset();
            Assert.That(decision.State, Is.EqualTo(HoundState.Wander));
            Assert.That(memory.Count, Is.Zero);
        }

        [Test]
        public void PausedTickDoesNotStartTracking()
        {
            Emit();
            decision.Tick(Vector3.zero, 0, 0, HoundRouteStatus.Idle, _ => true);
            Assert.That(decision.State, Is.EqualTo(HoundState.Wander));
        }
    }
}

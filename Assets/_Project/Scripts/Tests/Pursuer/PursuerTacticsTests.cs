using Lockdown.AI.Agents;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests
{
    public sealed class PursuerTacticsTests
    {
        [Test]
        public void ArrestRequiresContinuousReachAndEscapingResetsProgress()
        {
            var arrest = new PursuerArrest();
            Assert.That(arrest.Tick(true, 0.8f, 1.5f), Is.False);
            Assert.That(arrest.Progress, Is.GreaterThan(0.5f));
            Assert.That(arrest.Tick(false, 1f, 1.5f), Is.False);
            Assert.That(arrest.Progress, Is.Zero);
            Assert.That(arrest.Tick(true, 0.8f, 1.5f), Is.False);
            Assert.That(arrest.Tick(true, 0.7f, 1.5f), Is.True);
        }
        [Test]
        public void PausedTimeDoesNotAdvanceAnArrest()
        {
            var arrest = new PursuerArrest();
            Assert.That(arrest.Tick(true, 0f, 1.5f), Is.False);
            Assert.That(arrest.Tick(true, -1f, 1.5f), Is.False);
            Assert.That(arrest.Progress, Is.Zero);
        }
        [Test]
        public void ObservationsRejectTeleportsAndMovementAcrossHiddenIntervals()
        {
            var planner = MovingPlayer();
            Assert.That(planner.Velocity.x, Is.EqualTo(4f).Within(0.01f));
            planner.LoseSight(); planner.Observe(Vector3.right * 0.8f, 0.2f);
            Assert.That(planner.Velocity, Is.EqualTo(Vector3.zero));
            planner.Observe(Vector3.right * 100f, 0.3f);
            Assert.That(planner.Velocity, Is.EqualTo(Vector3.zero));
        }
        [Test]
        public void InterceptionChoosesACheaperRouteThatCanBeatThePlayer()
        {
            var planner = MovingPlayer();
            bool Route(Vector3 from, Vector3 to, out float length)
            {
                // Guard's current-target route detours around a display, but an ahead doorway is nearby.
                length = from.z < -1f ? (to.x < 0.5f ? 8f : 1.5f) : Vector3.Distance(from, to);
                return true;
            }
            Assert.That(planner.TryChoose(Vector3.back * 3f, 0.1f, new PursuerSettings(), Route, out var goal), Is.True);
            Assert.That(goal.x, Is.GreaterThan(planner.LastPosition.x));
        }
        [Test]
        public void AGuardBehindThePlayerDoesNotInventAShortcutOnAStraightRoute()
        {
            var planner = MovingPlayer();
            Assert.That(planner.TryChoose(Vector3.left * 5f, 0.1f, new PursuerSettings(), StraightRoute, out _), Is.False);
        }
        [Test]
        public void ExpiredPredictionAndUnreachableOrLateRoutesFallBackToChasing()
        {
            var planner = MovingPlayer(); var settings = new PursuerSettings();
            Assert.That(planner.TryChoose(Vector3.zero, 3f, settings, StraightRoute, out _), Is.False);
            bool Blocked(Vector3 from, Vector3 to, out float length) { length = 0f; return false; }
            Assert.That(planner.TryChoose(Vector3.zero, 0.1f, settings, Blocked, out _), Is.False);
            bool Late(Vector3 from, Vector3 to, out float length) { length = from.z < -1f ? 100f : 1f; return true; }
            Assert.That(planner.TryChoose(Vector3.back * 3f, 0.1f, settings, Late, out _), Is.False);
        }
        [Test]
        public void CoverMovesForwardAndRequiresSolidScreeningAndACompleteShortRoute()
        {
            Vector3 threat = Vector3.forward * 8f;
            Assert.That(PursuerAdvanceCover.TryChoose(Vector3.zero, threat, StraightRoute, (_, __) => true, out var goal), Is.True);
            Assert.That(Vector3.Distance(goal, threat), Is.LessThan(7.7f));
            Assert.That(Vector3.Distance(goal, Vector3.zero), Is.LessThanOrEqualTo(4.5f));
            Assert.That(PursuerAdvanceCover.TryChoose(Vector3.zero, threat, StraightRoute, (_, __) => false, out _), Is.False);
            bool LongRoute(Vector3 from, Vector3 to, out float length) { length = 20f; return true; }
            Assert.That(PursuerAdvanceCover.TryChoose(Vector3.zero, threat, LongRoute, (_, __) => true, out _), Is.False);
        }
        private static PursuerInterception MovingPlayer()
        {
            var planner = new PursuerInterception(); planner.Observe(Vector3.zero, 0f); planner.Observe(Vector3.right * 0.4f, 0.1f); return planner;
        }
        private static bool StraightRoute(Vector3 from, Vector3 to, out float length) { length = Vector3.Distance(from, to); return true; }
    }
}

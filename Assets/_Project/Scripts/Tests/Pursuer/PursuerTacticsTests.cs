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

        [Test]
        public void LostContactSearchGetsItsFullInspectionTimeAfterTravel()
        {
            var search = new PursuerSearch();
            search.BeginApproach(new[] { Vector3.right * 6f, Vector3.zero }, 0f, 18f, 4f, 12f);
            Assert.That(search.HasReachedArea, Is.False);
            Assert.That(search.Expired(11f), Is.False, "Walking to the area must not consume its inspection budget.");
            search.Arrived(Vector3.right * 6f, 11f);
            Assert.That(search.HasReachedArea, Is.True);
            Assert.That(search.Expired(28.9f), Is.False);
            Assert.That(search.Expired(29f), Is.True);
            search.Arrived(Vector3.zero, 28f);
            Assert.That(search.Expired(29f), Is.True, "Visiting another corner must not reset the total search budget forever.");
        }
        [Test]
        public void UnreachableSearchAreaStillHasABoundedTravelTimeout()
        {
            var search = new PursuerSearch(); search.BeginApproach(new[] { Vector3.right * 6f }, 0f, 18f, 4f, 12f);
            Assert.That(search.Expired(12f), Is.True);
            Assert.That(search.TryNext(_ => false, out _), Is.False);
        }
        [Test]
        public void SearchVisitsRankedCornersBeforeRadialPointsAndSkipsBlockedAreas()
        {
            var search = new PursuerSearch();
            var first = Vector3.right * 6f; var second = Vector3.forward * 3f;
            search.BeginApproach(new[] { first, second, Vector3.zero }, 0f, 18f, 4f, 12f);
            Assert.That(search.TryNext(point => point != first, out var result), Is.True);
            Assert.That(result, Is.EqualTo(second));
            search.Arrived(second, 1f);
            Assert.That(search.TryNext(_ => true, out result), Is.True);
            Assert.That(result, Is.EqualTo(Vector3.zero));
        }
        [Test]
        public void CornerHypothesesPreferScreenedForwardRoutesAndRetainLastSeenFallback()
        {
            var points = PursuerCornerSearch.Build(Vector3.left * 3f, Vector3.zero, Vector3.forward * 4f,
                new PursuerSettings(), StraightRoute, (_, to) => to.x > 0.5f);
            Assert.That(points[0].x, Is.GreaterThan(0.5f), "A reachable corner can outrank an exposed straight-line guess.");
            Assert.That(points, Does.Contain(Vector3.zero));
            Assert.That(points.Count, Is.LessThanOrEqualTo(4));
            foreach (var point in points) Assert.That(point.z, Is.GreaterThanOrEqualTo(-0.01f), "Do not invent a backward escape.");
        }
        [Test]
        public void InvalidCornerRoutesAndStationaryObservationsFallBackToLastSeen()
        {
            bool Blocked(Vector3 from, Vector3 to, out float length) { length = float.PositiveInfinity; return false; }
            var points = PursuerCornerSearch.Build(Vector3.zero, Vector3.forward, Vector3.forward * 4f,
                new PursuerSettings(), Blocked, (_, __) => true);
            Assert.That(points, Is.EqualTo(new[] { Vector3.forward }));
            points = PursuerCornerSearch.Build(Vector3.zero, Vector3.forward, Vector3.zero,
                new PursuerSettings(), StraightRoute, (_, __) => true);
            Assert.That(points, Is.EqualTo(new[] { Vector3.forward }));
        }
    }
}

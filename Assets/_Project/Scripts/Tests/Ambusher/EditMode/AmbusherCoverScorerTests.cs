using System.Collections.Generic;
using Lockdown.AI.Core.Ambusher;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests.Ambusher
{
    public sealed class AmbusherCoverScorerTests
    {
        private static readonly Vector3 Self = Vector3.zero;
        private static readonly Vector3 Threat = new Vector3(0f, 0f, 10f);
        private AmbusherCoverScorer scorer;

        [SetUp]
        public void Setup() { scorer = new AmbusherCoverScorer(); }

        private static AmbusherCandidate Candidate(Vector3 cover, bool hidden = true, bool clear = true, bool reserved = false, bool inside = true) =>
            new AmbusherCandidate
            {
                Cover = cover,
                Peek = cover + Vector3.right,
                HiddenAtCover = hidden,
                ClearShotAtPeek = clear,
                TravelDistance = AmbusherBrain.Flat(Self, cover),
                Reserved = reserved,
                InsideTerritory = inside
            };

        private static float Straight(Vector3 point) => AmbusherBrain.Flat(Self, point);

        [Test]
        public void PrefersHiddenCoverWithAClearShotNearTheIdealRange()
        {
            var good = Candidate(new Vector3(2f, 0f, 1f));
            var noShot = Candidate(new Vector3(-2f, 0f, 1f), clear: false);
            var tooClose = Candidate(new Vector3(2f, 0f, 8f));
            Assert.That(scorer.TryChoose(new List<AmbusherCandidate> { tooClose, noShot, good }, Self, Threat, false, Self, null, Straight, out var post), Is.True);
            Assert.That(post.Cover, Is.EqualTo(good.Cover));
            Assert.That(post.Peek, Is.EqualTo(good.Peek));
        }

        [Test]
        public void IgnoresVisibleCoverTheCurrentPostAndTheFailedPost()
        {
            var visible = Candidate(new Vector3(2f, 0f, 1f), hidden: false);
            var current = Candidate(new Vector3(0.5f, 0f, 0f));
            var failed = Candidate(new Vector3(-2f, 0f, 0f));
            var fallback = Candidate(new Vector3(4f, 0f, -3f));
            var list = new List<AmbusherCandidate> { visible, current, failed, fallback };
            Assert.That(scorer.TryChoose(list, Self, Threat, false, Self, failed.Cover, Straight, out var post), Is.True);
            Assert.That(post.Cover, Is.EqualTo(fallback.Cover));
        }

        [Test]
        public void ReservedCoverAndPostsOutsideTheTerritoryArePenalised()
        {
            var reserved = Candidate(new Vector3(2f, 0f, 1f), reserved: true);
            var outside = Candidate(new Vector3(-2f, 0f, 1f), inside: false);
            var free = Candidate(new Vector3(3f, 0f, -2f));
            Assert.That(scorer.Score(reserved, Self, Threat, false), Is.GreaterThan(scorer.Score(free, Self, Threat, false)));
            Assert.That(scorer.Score(outside, Self, Threat, false), Is.GreaterThan(scorer.Score(reserved, Self, Threat, false)));
        }

        [Test]
        public void FleeingRejectsPostsThatDoNotGainDistance()
        {
            var closer = Candidate(new Vector3(2f, 0f, 2f));
            var farther = Candidate(new Vector3(1f, 0f, -6f));
            Assert.That(scorer.TryChoose(new List<AmbusherCandidate> { closer, farther }, Self, Threat, true, Self, null, Straight, out var post), Is.True);
            Assert.That(post.Cover, Is.EqualTo(farther.Cover));
        }

        [Test]
        public void FleeingWithoutGoodCoverRunsToTheFarthestReachableEscape()
        {
            var nearCover = Candidate(new Vector3(1.5f, 0f, 0f));
            var openNear = Candidate(new Vector3(0f, 0f, -4f), hidden: false);
            var openFar = Candidate(new Vector3(0f, 0f, -9f), hidden: false);
            var list = new List<AmbusherCandidate> { nearCover, openNear, openFar };
            Assert.That(scorer.TryChoose(list, Self, Threat, true, Self, null, Straight, out var post), Is.True);
            Assert.That(post.Cover, Is.EqualTo(openFar.Cover));
            Assert.That(post.Peek, Is.EqualTo(openFar.Cover), "An escape point is not a firing post.");
        }

        private static AmbusherFlankCandidate Flank(Vector3 point, bool sees = true) =>
            new AmbusherFlankCandidate { Point = point, SeesLastKnown = sees, TravelDistance = Straight(point), InsideTerritory = true };

        [Test]
        public void FlankSearchAlternatesSidesAroundTheLastKnownPosition()
        {
            // He stands south of where the player vanished; flanks lie east and west of that spot.
            var east = Flank(Threat + new Vector3(6.5f, 0f, -2.5f));
            var west = Flank(Threat + new Vector3(-6.5f, 0f, -2.5f));
            var behind = Flank(Threat + new Vector3(0f, 0f, -7f));
            var list = new List<AmbusherFlankCandidate> { behind, east, west };
            Assert.That(scorer.TryChooseFlank(list, Self, Threat, 0, Straight, out var first), Is.True);
            Assert.That(scorer.TryChooseFlank(list, Self, Threat, 1, Straight, out var second), Is.True);
            Assert.That(new[] { first, second }, Is.EquivalentTo(new[] { east.Point, west.Point }));
            Assert.That(first, Is.Not.EqualTo(behind.Point), "Standing on the same line of sight would see nothing new.");
        }

        [Test]
        public void FlankMustSeeTheLastKnownPositionAndBeReachable()
        {
            var blind = Flank(Threat + new Vector3(6.5f, 0f, -2.5f), sees: false);
            var blocked = Flank(Threat + new Vector3(-6.5f, 0f, -2.5f));
            var open = Flank(Threat + new Vector3(5f, 0f, 5f));
            float Path(Vector3 point) => point == blocked.Point ? float.PositiveInfinity : Straight(point);
            Assert.That(scorer.TryChooseFlank(new List<AmbusherFlankCandidate> { blind, blocked, open }, Self, Threat, 0, Path, out var point), Is.True);
            Assert.That(point, Is.EqualTo(open.Point));
            Assert.That(scorer.TryChooseFlank(new List<AmbusherFlankCandidate> { blind, blocked }, Self, Threat, 0, Path, out _), Is.False);
        }

        [Test]
        public void WalkingDistanceDecidesBetweenNearbyOptions()
        {
            var acrossWall = Candidate(new Vector3(2f, 0f, 1f));
            var sameRoom = Candidate(new Vector3(-3f, 0f, 1.5f));
            Assert.That(scorer.Score(acrossWall, Self, Threat, false), Is.LessThan(scorer.Score(sameRoom, Self, Threat, false)),
                "In a straight line the post across the wall looks best.");
            float Path(Vector3 point) => point == acrossWall.Cover ? 15f : Straight(point);
            Assert.That(scorer.TryChoose(new List<AmbusherCandidate> { acrossWall, sameRoom }, Self, Threat, false, Self, null, Path, out var post), Is.True);
            Assert.That(post.Cover, Is.EqualTo(sameRoom.Cover));
        }

        [Test]
        public void UnreachableCandidatesAreSkipped()
        {
            var blocked = Candidate(new Vector3(2f, 0f, 1f));
            var reachable = Candidate(new Vector3(-3f, 0f, -1f));
            float Path(Vector3 point) => point == blocked.Cover ? float.PositiveInfinity : Straight(point);
            Assert.That(scorer.TryChoose(new List<AmbusherCandidate> { blocked, reachable }, Self, Threat, false, Self, null, Path, out var post), Is.True);
            Assert.That(post.Cover, Is.EqualTo(reachable.Cover));
            Assert.That(scorer.TryChoose(new List<AmbusherCandidate> { blocked }, Self, Threat, false, Self, null, Path, out _), Is.False);
        }
    }
}

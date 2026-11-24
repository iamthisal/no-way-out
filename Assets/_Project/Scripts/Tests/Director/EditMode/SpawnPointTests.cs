using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class SpawnPointTests
    {
        private readonly DirectorTuning tuning = new DirectorTuning();

        private static SpawnCandidate Good() => new SpawnCandidate
        {
            PointId = 1, RoomId = "Egypt", Allowed = SpawnKindSet.All, Zone = SpawnZone.OpenGallery, Reachable = true, ClearSpace = true,
            RouteLength = 20f, SecondsSinceUsed = float.PositiveInfinity, AlarmDistance = float.PositiveInfinity
        };

        private SpawnRejection Check(SpawnCandidate candidate, float health = 1f, SpawnKind kind = SpawnKind.Guard) =>
            SpawnPointFilter.Check(candidate, kind, health, tuning);

        [Test]
        public void AHiddenReachablePointPasses() { Assert.That(Check(Good()), Is.EqualTo(SpawnRejection.None)); }

        [Test]
        public void EveryHardRuleRejects()
        {
            var point = Good(); point.Allowed = SpawnKindSet.Hound;
            Assert.That(Check(point), Is.EqualTo(SpawnRejection.NotAllowedHere));
            point = Good(); point.InSafeRoom = true; Assert.That(Check(point), Is.EqualTo(SpawnRejection.SafeRoom));
            point = Good(); point.InView = true; Assert.That(Check(point), Is.EqualTo(SpawnRejection.Visible));
            point = Good(); point.HasLineOfSight = true; Assert.That(Check(point), Is.EqualTo(SpawnRejection.Visible));
            point = Good(); point.Reachable = false; Assert.That(Check(point), Is.EqualTo(SpawnRejection.Unreachable));
            point = Good(); point.RouteLength = 7.9f; Assert.That(Check(point), Is.EqualTo(SpawnRejection.Distance));
            point = Good(); point.RouteLength = 40.1f; Assert.That(Check(point), Is.EqualTo(SpawnRejection.Distance));
            point = Good(); point.ClearSpace = false; Assert.That(Check(point), Is.EqualTo(SpawnRejection.Crowded));
        }

        [Test]
        public void AWoundedPlayerIsNeverCutOffFromTheExit()
        {
            var point = Good(); point.OnExitRoute = true;
            Assert.That(Check(point, 0.29f), Is.EqualTo(SpawnRejection.BlocksWoundedPlayer));
            Assert.That(Check(point, 0.5f), Is.EqualTo(SpawnRejection.None));
        }

        [Test]
        public void ExitRoutesAllowASmallDetourOnly()
        {
            Assert.That(SpawnPointFilter.LiesOnRoute(10f, 12f, 20f, 1.15f), Is.True);
            Assert.That(SpawnPointFilter.LiesOnRoute(10f, 20f, 20f, 1.15f), Is.False);
            Assert.That(SpawnPointFilter.LiesOnRoute(10f, 12f, float.PositiveInfinity, 1.15f), Is.False);
        }

        [Test]
        public void DistanceComfortPeaksInTheMiddleBand()
        {
            Assert.That(SpawnPointScorer.DistanceComfort(20f, tuning), Is.EqualTo(1f));
            Assert.That(SpawnPointScorer.DistanceComfort(8f, tuning), Is.Zero);
            Assert.That(SpawnPointScorer.DistanceComfort(40f, tuning), Is.Zero);
            Assert.That(SpawnPointScorer.DistanceComfort(32.5f, tuning), Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void RecentlyUsedPointsLoseAndFreshOnesWin()
        {
            Assert.That(SpawnPointScorer.Novelty(0f, tuning), Is.Zero);
            Assert.That(SpawnPointScorer.Novelty(30f, tuning), Is.EqualTo(0.632f).Within(0.01f));
            Assert.That(SpawnPointScorer.Novelty(float.PositiveInfinity, tuning), Is.EqualTo(1f));
        }

        [Test]
        public void PointsNearTheAlarmAreMoreRelevant()
        {
            var near = Good(); near.AlarmDistance = 3f;
            var far = Good(); far.AlarmDistance = 25f;
            Assert.That(SpawnPointScorer.Score(near, tuning), Is.GreaterThan(SpawnPointScorer.Score(far, tuning)));
            Assert.That(SpawnPointScorer.Relevance(Good(), tuning), Is.Zero);
        }
    }
}

using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class SpawnLimitsTests
    {
        private readonly DirectorTuning tuning = new DirectorTuning();

        [Test]
        public void EachTypeStopsAtItsOwnCap()
        {
            var census = new EnemyCensus { Guards = 2, Hounds = 3 };
            Assert.That(SpawnLimits.HasRoom(SpawnKind.Guard, census, tuning), Is.False);
            Assert.That(SpawnLimits.HasRoom(SpawnKind.Hound, census, tuning), Is.True);
        }

        [Test]
        public void TheTotalCapBlocksEveryType()
        {
            var census = new EnemyCensus { Guards = 1, Archers = 1, Hounds = 4 };
            foreach (var kind in SpawnKinds.All) Assert.That(SpawnLimits.HasRoom(kind, census, tuning), Is.False, kind.ToString());
        }

        [Test]
        public void DormantEnemiesUseACapSlotButAddNoPressure()
        {
            var census = new EnemyCensus();
            census.Add(SpawnKind.Archer, true);
            Assert.That(census.Total, Is.EqualTo(1));
            Assert.That(SpawnLimits.FreeShare(census, tuning), Is.EqualTo(1f));
            census.Add(SpawnKind.Guard);
            Assert.That(SpawnLimits.FreeShare(census, tuning), Is.EqualTo(5f / 6f).Within(0.001f));
        }

        [Test]
        public void SpawnKindSetsContainOnlyTheirMembers()
        {
            var set = SpawnKindSet.Guard | SpawnKindSet.Hound;
            Assert.That(set.Contains(SpawnKind.Guard), Is.True);
            Assert.That(set.Contains(SpawnKind.Archer), Is.False);
            Assert.That(set.Contains(SpawnKind.Hound), Is.True);
        }
    }
}

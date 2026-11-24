using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class AlarmDispatchTests
    {
        [Test]
        public void TheNearestFreeGuardWhoCanWalkThereAnswers()
        {
            var guards = new[]
            {
                new AlarmCandidate { Reachable = true, RouteLength = 5f, Busy = true },
                new AlarmCandidate { Reachable = false, RouteLength = 1f },
                new AlarmCandidate { Reachable = true, RouteLength = 30f },
                new AlarmCandidate { Reachable = true, RouteLength = 20f }
            };
            Assert.That(AlarmDispatch.Choose(guards), Is.EqualTo(3));
        }

        [Test]
        public void NobodyAnswersWhenEveryGuardIsBusyOrCutOff()
        {
            Assert.That(AlarmDispatch.Choose(new AlarmCandidate[0]), Is.EqualTo(-1));
            Assert.That(AlarmDispatch.Choose(new[] { new AlarmCandidate { RouteLength = 2f } }), Is.EqualTo(-1));
        }
    }
}

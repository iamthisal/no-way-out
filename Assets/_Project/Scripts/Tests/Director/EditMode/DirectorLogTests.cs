using Lockdown.AI.Core.Director;
using NUnit.Framework;

namespace Lockdown.Tests.Director
{
    public sealed class DirectorLogTests
    {
        [Test]
        public void KeepsTheNewestEntriesOldestFirst()
        {
            var log = new DirectorLog(3);
            for (int i = 0; i < 5; i++) log.Add(new DirectorLog.Entry { Time = i });
            Assert.That(log.Count, Is.EqualTo(3));
            Assert.That(log[0].Time, Is.EqualTo(2f));
            Assert.That(log[2].Time, Is.EqualTo(4f));
        }

        [Test]
        public void CsvHasOneRowPerDecisionWithMatchingColumns()
        {
            var log = new DirectorLog(4);
            var decision = new DirectorDecision { Spawn = true, Reason = "Reason, quoted" };
            decision.Choice = new SpawnOption { Kind = SpawnKind.Guard, RoomId = "Egypt", PointId = 2, Utility = 0.44f };
            decision.Top.Add(decision.Choice);
            log.Add(new DirectorLog.Entry { Time = 1.5f, Decision = decision });
            string[] lines = log.ToCsv().TrimEnd('\n').Split('\n');
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[1], Does.StartWith("1.5,BuildUp,"));
            Assert.That(lines[1], Does.Contain("\"Guard@Egypt#2\",0.44"));
            Assert.That(lines[1].Replace("Reason, quoted", "").Split(',').Length, Is.EqualTo(lines[0].Split(',').Length));
        }
    }
}

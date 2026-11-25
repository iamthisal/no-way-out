using System;
using Lockdown.Agents.Pursuer;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests
{
    public sealed class GuardAlertTests
    {
        private static readonly GuardAlert[] LeastToMostUrgent =
        {
            GuardAlert.Approaching, GuardAlert.BlockingEscape, GuardAlert.AdvancingThroughCover,
            GuardAlert.ReturningFire, GuardAlert.Handcuffing,
        };

        [Test]
        public void MoreUrgentAlertsOutrankCalmerOnes()
        {
            for (int i = 0; i < LeastToMostUrgent.Length; i++)
            for (int j = 0; j < LeastToMostUrgent.Length; j++)
            {
                if (i == j) continue;
                bool outranks = GuardAlerts.Outranks(LeastToMostUrgent[i], -1f, LeastToMostUrgent[j], -1f);
                Assert.That(outranks, Is.EqualTo(i > j), LeastToMostUrgent[i] + " vs " + LeastToMostUrgent[j]);
            }
            Assert.That(GuardAlerts.Outranks(GuardAlert.Approaching, -1f, GuardAlert.None, -1f), Is.True);
        }

        [Test]
        public void TheArrestCloserToDoneKeepsTheBar()
        {
            Assert.That(GuardAlerts.Outranks(GuardAlert.Handcuffing, 0.8f, GuardAlert.Handcuffing, 0.3f), Is.True);
            Assert.That(GuardAlerts.Outranks(GuardAlert.Handcuffing, 0.3f, GuardAlert.Handcuffing, 0.8f), Is.False);
            Assert.That(GuardAlerts.Outranks(GuardAlert.Approaching, -1f, GuardAlert.Approaching, -1f), Is.False,
                "Equal alerts must not both claim the bar.");
        }

        [Test]
        public void EveryAlertHasATabAndAMessageCinzelCanDraw()
        {
            var font = Resources.Load<Font>("Fonts/Cinzel");
            Assert.That(font, Is.Not.Null);
            Assert.That(GuardAlerts.Message(GuardAlert.None), Is.Null, "No alert draws no bar.");
            foreach (GuardAlert alert in Enum.GetValues(typeof(GuardAlert)))
            {
                if (alert == GuardAlert.None) continue;
                string kicker = GuardAlerts.Kicker(alert), message = GuardAlerts.Message(alert);
                Assert.That(kicker, Is.Not.Empty, alert + " needs a tab.");
                Assert.That(message, Is.Not.Empty, alert + " needs a message.");
                foreach (char character in (kicker + message).ToUpperInvariant() + message)
                    if (character != ' ')
                        Assert.That(font.HasCharacter(character), Is.True, "Cinzel is missing '" + character + "' in " + alert + ".");
            }
            Assert.That(GuardAlerts.Kicker(GuardAlert.Handcuffing), Is.EqualTo("Arrest"));
        }
    }
}

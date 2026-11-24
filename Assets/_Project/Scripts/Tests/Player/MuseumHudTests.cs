using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests
{
    public sealed class MuseumHudTests
    {
        [Test]
        public void MuseumHudShipsCinzelWithEveryCharacterItDrawsAndItsLicence()
        {
            var font = Resources.Load<Font>("Fonts/Cinzel");
            Assert.That(font, Is.Not.Null, "The end screens, loot plaque and health bar load Cinzel from Resources/Fonts.");
            Assert.That(font.dynamic, Is.True, "The HUD scales its text, so the font must stay dynamic.");
            // End-screen titles, plaque labels, and loot values such as "$12,500".
            foreach (char character in "YOUGTCAESPDRMIHLyougtcaespdrmihl$,.0123456789")
                Assert.That(font.HasCharacter(character), Is.True, "Cinzel is missing '" + character + "'.");
            Assert.That(Resources.Load<TextAsset>("Fonts/Cinzel-OFL"), Is.Not.Null,
                "The SIL Open Font License must ship alongside Cinzel.");
        }
    }
}

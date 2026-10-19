using System;
using Lockdown.AI.Core.Hound;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests.Hound
{
    public sealed class HoundMemoryTests
    {
        private static HoundSound Noise(float loudness, Vector3 position = default, double time = 0) =>
            new HoundSound(position, HoundSoundKind.Custom, loudness, 50f, time);

        [Test]
        public void ScoreUsesLoudnessDistanceAndExponentialAge()
        {
            var memory = new HoundMemory();
            memory.Remember(Noise(12, Vector3.right * 5f), 0);
            Assert.That(memory.Score(memory.Latest, Vector3.zero, 0), Is.EqualTo(6f).Within(0.0001));
            Assert.That(memory.Score(memory.Latest, Vector3.zero, 2), Is.EqualTo(6 * Math.Exp(-1.1)).Within(0.0001));
        }

        [Test]
        public void CapacityEvictsOldestClue()
        {
            var memory = new HoundMemory(capacity: 2);
            memory.Remember(Noise(6), 0);
            int first = memory.Latest.Id;
            memory.Remember(Noise(7), 0);
            memory.Remember(Noise(8), 0);
            Assert.That(memory.Count, Is.EqualTo(2));
            Assert.That(memory.Find(first), Is.Null);
        }

        [Test]
        public void SoundStoresPositionSnapshot()
        {
            var memory = new HoundMemory();
            Vector3 position = new Vector3(4, 0, 2);
            memory.Remember(Noise(6, position), 0);
            position.x = 40;
            Assert.That(memory.Latest.Sound.Position.x, Is.EqualTo(4));
        }

        [Test]
        public void InvalidFutureAndExpiredSoundsAreRejected()
        {
            var memory = new HoundMemory();
            Assert.That(memory.Remember(Noise(float.NaN), 0), Is.False);
            Assert.That(memory.Remember(Noise(float.PositiveInfinity), 0), Is.False);
            Assert.That(memory.Remember(Noise(-1), 0), Is.False);
            Assert.That(memory.Remember(Noise(6, new Vector3(float.NaN, 0, 0)), 0), Is.False);
            Assert.That(memory.Remember(Noise(6, time: 2), 0), Is.False);
            Assert.That(memory.Remember(Noise(6), 12), Is.False);
            Assert.That(memory.Count, Is.Zero);
        }

        [Test]
        public void AgeAndMemoryFloorRemoveOldClues()
        {
            var memory = new HoundMemory(maximumAge: 20);
            memory.Remember(Noise(0.1f), 0);
            memory.Prune(Vector3.zero, 2);
            Assert.That(memory.Count, Is.Zero);
            memory.Remember(Noise(100, time: 2), 2);
            memory.Prune(Vector3.zero, 22);
            Assert.That(memory.Count, Is.Zero);
        }

        [Test]
        public void StrongerClueMustExceedSwitchRatio()
        {
            var memory = new HoundMemory();
            memory.Remember(Noise(6), 0);
            int current = memory.Latest.Id;
            memory.Remember(Noise(6.6f), 0);
            Assert.That(memory.Select(Vector3.zero, 0, current, 1.2f, 1.3f, _ => true).Id, Is.EqualTo(current));
            memory.Remember(Noise(8.4f), 0);
            Assert.That(memory.Select(Vector3.zero, 0, current, 1.2f, 1.3f, _ => true).Id, Is.EqualTo(memory.Latest.Id));
        }

        [Test]
        public void UnreachableBestClueIsForgottenAndReachableAlternativeIsChosen()
        {
            var memory = new HoundMemory();
            memory.Remember(Noise(6, Vector3.left), 0);
            int reachable = memory.Latest.Id;
            memory.Remember(Noise(12, Vector3.right), 0);
            int unreachable = memory.Latest.Id;
            Assert.That(memory.Select(Vector3.zero, 0, 0, 1.2f, 1.3f, p => p.x < 0).Id, Is.EqualTo(reachable));
            Assert.That(memory.Find(unreachable), Is.Null);
        }

        [Test]
        public void QuietStepsRemainBelowTrackingThreshold()
        {
            var memory = new HoundMemory();
            memory.Remember(HoundSoundProfiles.Create(HoundSoundKind.Walk, Vector3.zero, 0), 0);
            Assert.That(memory.Select(Vector3.zero, 0, 0, 1.2f, 1.3f, _ => true), Is.Null);
            memory.Remember(HoundSoundProfiles.Create(HoundSoundKind.Gunshot, Vector3.right * 10, 0), 0);
            Assert.That(memory.Select(Vector3.zero, 0, 0, 1.2f, 1.3f, _ => true).Sound.Kind, Is.EqualTo(HoundSoundKind.Gunshot));
        }

        [Test]
        public void ClearRemovesAllClues()
        {
            var memory = new HoundMemory();
            memory.Remember(Noise(6), 0);
            memory.Clear();
            Assert.That(memory.Count, Is.Zero);
            Assert.That(memory.Latest, Is.Null);
        }
    }
}

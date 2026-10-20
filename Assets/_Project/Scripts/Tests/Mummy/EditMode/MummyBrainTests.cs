using Lockdown.AI.Core.Mummy;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests.Mummy
{
    public sealed class MummyBrainTests
    {
        private sealed class FakeWorld : IMummyWorld
        {
            public int Requests;
            public bool TryChooseSearchPoint(Vector3 around, int index, out Vector3 point)
            {
                Requests++;
                point = around + new Vector3(index + 1f, 0f, 0f);
                return true;
            }
        }

        private const float Step = 0.1f;
        private static readonly Vector3[] Route = { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f) };
        private FakeWorld world;
        private MummyBrain brain;
        private MummySenses senses;

        [SetUp]
        public void Setup()
        {
            world = new FakeWorld();
            brain = new MummyBrain(Route);
            senses = new MummySenses { Position = new Vector3(9f, 0f, 1f), PlayerPosition = new Vector3(20f, 0f, 20f) };
        }

        private void Tick(float seconds = Step)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Step) brain.Tick(senses, Step, world);
        }

        [Test]
        public void DormantMummyIgnoresThePlayerUntilWoken()
        {
            senses.SeesPlayer = true;
            Tick(1f);
            Assert.That(brain.State, Is.EqualTo(MummyState.Dormant));
            brain.Wake(senses.Position);
            Assert.That(brain.State, Is.EqualTo(MummyState.Patrol));
        }

        [Test]
        public void PatrolJoinsTheNearestRoutePointAndWalksTheLoop()
        {
            brain.Wake(senses.Position);
            Assert.That(brain.Goal, Is.EqualTo(Route[1]));
            senses.ArrivedAtGoal = true;
            Tick();
            Assert.That(brain.Goal, Is.EqualTo(Route[2]));
            Tick();
            Assert.That(brain.Goal, Is.EqualTo(Route[0]), "The route loops.");
        }

        [Test]
        public void SightingStartsAChaseAndIsReportedOnce()
        {
            brain.Wake(senses.Position);
            senses.SeesPlayer = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(MummyState.Chase));
            Assert.That(brain.SpottedThisTick, Is.True);
            Assert.That(brain.Goal, Is.EqualTo(senses.PlayerPosition));
            Tick();
            Assert.That(brain.SpottedThisTick, Is.False, "Only the first sighting alerts the others.");
        }

        [Test]
        public void LostPlayerIsFollowedToTheLastKnownPositionThenSearched()
        {
            brain.Wake(senses.Position);
            senses.SeesPlayer = true;
            Tick();
            Vector3 lastSeen = senses.PlayerPosition;
            senses.SeesPlayer = false;
            senses.PlayerPosition = new Vector3(-50f, 0f, -50f);
            Tick(brain.Settings.LoseSightGrace - 0.3f);
            Assert.That(brain.State, Is.EqualTo(MummyState.Chase));
            Assert.That(brain.Goal, Is.EqualTo(lastSeen), "Without sight he heads for where the player was.");
            Tick(0.5f);
            Assert.That(brain.State, Is.EqualTo(MummyState.Search));
            senses.ArrivedAtGoal = true;
            Tick();
            Assert.That(world.Requests, Is.EqualTo(1));
            Assert.That(brain.Goal, Is.EqualTo(lastSeen + Vector3.right));
        }

        [Test]
        public void SearchGivesUpAfterFourteenSecondsAndRejoinsThePatrol()
        {
            brain.Wake(senses.Position);
            senses.SeesPlayer = true;
            Tick();
            senses.SeesPlayer = false;
            Tick(brain.Settings.LoseSightGrace + 0.2f);
            Assert.That(brain.State, Is.EqualTo(MummyState.Search));
            Tick(brain.Settings.SearchDuration - 0.5f);
            Assert.That(brain.State, Is.EqualTo(MummyState.Search));
            Tick(0.7f);
            Assert.That(brain.State, Is.EqualTo(MummyState.Patrol));
            Assert.That(brain.Goal, Is.EqualTo(Route[1]), "He rejoins at the route point nearest to him.");
        }

        [Test]
        public void GunshotsAndAlertsSendHimToInvestigate()
        {
            brain.Wake(senses.Position);
            senses.HeardNoise = true;
            senses.NoisePosition = new Vector3(4f, 0f, 4f);
            Tick();
            Assert.That(brain.State, Is.EqualTo(MummyState.Investigate));
            Assert.That(brain.Goal, Is.EqualTo(senses.NoisePosition));
            senses.HeardNoise = false;
            senses.ArrivedAtGoal = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(MummyState.Search));

            brain.Alert(new Vector3(-3f, 0f, 2f));
            Assert.That(brain.State, Is.EqualTo(MummyState.Investigate));
            senses.SeesPlayer = true;
            senses.ArrivedAtGoal = false;
            Tick();
            brain.Alert(Vector3.zero);
            Assert.That(brain.State, Is.EqualTo(MummyState.Chase), "An alert never interrupts a chase.");
        }

        [Test]
        public void KnockedDownMummyResumesTheHuntWhenItRises()
        {
            brain.Wake(senses.Position);
            senses.SeesPlayer = true;
            Tick();
            Vector3 lastSeen = senses.PlayerPosition;
            brain.KnockDown();
            Tick(2f);
            Assert.That(brain.State, Is.EqualTo(MummyState.Down), "No decisions while lying down.");
            senses.SeesPlayer = false;
            brain.GetUp(senses.Position);
            Assert.That(brain.State, Is.EqualTo(MummyState.Search));
            Assert.That(brain.Goal, Is.EqualTo(lastSeen));
        }

        [Test]
        public void MummyThatNeverSawThePlayerReturnsToPatrolAfterRising()
        {
            brain.Wake(senses.Position);
            brain.KnockDown();
            brain.GetUp(senses.Position);
            Assert.That(brain.State, Is.EqualTo(MummyState.Patrol));
        }

        [Test]
        public void DestroyedMummyStaysDead()
        {
            brain.Wake(senses.Position);
            brain.Kill();
            brain.GetUp(senses.Position);
            brain.Alert(Vector3.zero);
            senses.SeesPlayer = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(MummyState.Dead));
        }
    }
}

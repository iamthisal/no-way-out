using Lockdown.AI.Core.Ambusher;
using NUnit.Framework;
using UnityEngine;

namespace Lockdown.Tests.Ambusher
{
    public sealed class AmbusherBrainTests
    {
        private sealed class FakeWorld : IAmbusherWorld
        {
            public bool Hidden = true;
            public bool Available = true;
            public AmbusherPost Next = new AmbusherPost(new Vector3(6f, 0f, 0f), new Vector3(6f, 0f, 1f));
            public int Requests;
            public bool LastFlee;
            public Vector3? LastFailed;
            public bool FlankAvailable = true;
            public int FlankRequests;
            public int LastFlankAttempt = -1;

            public bool TryChooseFlank(Vector3 self, Vector3 lastKnown, int attempt, out Vector3 point)
            {
                FlankRequests++;
                LastFlankAttempt = attempt;
                point = lastKnown + new Vector3(attempt % 2 == 0 ? 5f : -5f, 0f, -2f);
                return FlankAvailable;
            }

            public bool TryChoosePost(Vector3 self, Vector3 threat, bool flee, AmbusherPost current, Vector3? failedPost, out AmbusherPost post)
            {
                Requests++;
                LastFlee = flee;
                LastFailed = failedPost;
                post = Next;
                return Available;
            }

            public bool IsHidden(Vector3 point, Vector3 threat) => Hidden;
        }

        private const float Step = 0.1f;
        private FakeWorld world;
        private AmbusherBrain brain;
        private AmbusherSenses senses;

        [SetUp]
        public void Setup()
        {
            world = new FakeWorld();
            brain = new AmbusherBrain(Vector3.zero);
            senses = new AmbusherSenses { Position = Vector3.zero, PlayerPosition = new Vector3(0f, 0f, 10f), PlayerDistance = 10f, ArrivedAtGoal = true };
        }

        private void Tick(float seconds = Step)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Step) brain.Tick(senses, Step, world);
        }

        private void TickUntil(AmbusherState state, int maxTicks = 100)
        {
            for (int i = 0; i < maxTicks && brain.State != state; i++) brain.Tick(senses, Step, world);
            Assert.That(brain.State, Is.EqualTo(state));
        }

        [Test]
        public void StaysUnawareUntilThePlayerIsNoticed()
        {
            Tick(2f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Unaware));
            Assert.That(world.Requests, Is.Zero);
            Assert.That(brain.WantsToShoot, Is.False);
        }

        [Test]
        public void FirstSightingInTheOpenTakesAnOpeningVolleyFromWhereHeStands()
        {
            world.Hidden = false;
            senses.SeesPlayer = true;
            senses.Position = new Vector3(1f, 0f, 0f);
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
            Assert.That(brain.Goal, Is.EqualTo(senses.Position));
            Assert.That(brain.Post.Cover, Is.EqualTo(world.Next.Cover), "He should already know where to hide next.");
            Assert.That(brain.WantsToShoot, Is.True);
            Assert.That(world.LastFlee, Is.False);
        }

        [Test]
        public void FirstSightingFromHiddenCoverWaitsBeforePeeking()
        {
            senses.SeesPlayer = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Hide));
            Assert.That(world.Requests, Is.Zero);
            Tick(1f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
        }

        [Test]
        public void HideTimerCountsOnlyAfterReachingCover()
        {
            senses.SeesPlayer = true;
            senses.ArrivedAtGoal = false;
            Tick(3f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Hide));
            senses.ArrivedAtGoal = true;
            Tick(1.1f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
        }

        [Test]
        public void TwoShotBurstSendsHimBackIntoHiding()
        {
            senses.SeesPlayer = true;
            Tick(1.1f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
            senses.ShotsFired = 1;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
            Assert.That(brain.WantsToShoot, Is.True);
            senses.ShotsFired = 2;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Hide));
            Assert.That(brain.WantsToShoot, Is.False);
        }

        [Test]
        public void PlayerWhoTakesCoverIsHuntedFromAlternatingSidesOfTheLastKnownPosition()
        {
            senses.SeesPlayer = true;
            Tick(1.1f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
            senses.SeesPlayer = false;
            Tick(brain.Settings.LoseSightDelay - 0.2f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek), "A moment out of sight is not yet a lost target.");
            senses.ArrivedAtGoal = false;
            Tick(0.3f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek), "Searching starts from the peek post.");
            senses.ArrivedAtGoal = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Hunt));
            Assert.That(world.LastFlankAttempt, Is.EqualTo(0));
            Assert.That(brain.Goal, Is.EqualTo(brain.LastKnown + new Vector3(5f, 0f, -2f)));
            Tick();
            Assert.That(world.LastFlankAttempt, Is.EqualTo(1), "Nothing at the first flank: try the other side.");
            Assert.That(brain.Goal, Is.EqualTo(brain.LastKnown + new Vector3(-5f, 0f, -2f)));
            Tick(2 * Step);
            Assert.That(brain.Investigating, Is.True, "After three flanks he checks the last known position itself.");
            Assert.That(brain.Goal, Is.EqualTo(brain.LastKnown));
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Hide), "Nothing there either: back into cover.");
        }

        [Test]
        public void HuntWithoutAFlankGoesStraightToTheLastKnownPosition()
        {
            world.FlankAvailable = false;
            senses.SeesPlayer = true;
            Tick(1.1f);
            senses.SeesPlayer = false;
            TickUntil(AmbusherState.Hunt);
            Assert.That(brain.Investigating, Is.True);
            Assert.That(brain.Goal, Is.EqualTo(brain.LastKnown));
        }

        [Test]
        public void SpottingThePlayerWhileHuntingTakesAShotFromThere()
        {
            senses.SeesPlayer = true;
            Tick(1.1f);
            senses.SeesPlayer = false;
            TickUntil(AmbusherState.Hunt);
            senses.ArrivedAtGoal = false;
            senses.Position = new Vector3(3f, 0f, 1f);
            senses.SeesPlayer = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
            Assert.That(brain.Goal, Is.EqualTo(senses.Position));
            Assert.That(brain.Post.Cover, Is.EqualTo(world.Next.Cover));
        }

        [Test]
        public void ClosePlayerMakesHimRetreatEvenUnseen()
        {
            senses.PlayerPosition = new Vector3(0f, 0f, 3f);
            senses.PlayerDistance = 3f;
            senses.ArrivedAtGoal = false;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Retreat));
            Assert.That(world.LastFlee, Is.True);
            Assert.That(brain.LastKnown, Is.EqualTo(senses.PlayerPosition));
        }

        [Test]
        public void RetreatIsNotReplannedUntilTheCooldownEnds()
        {
            senses.PlayerDistance = 3f;
            senses.ArrivedAtGoal = false;
            Tick();
            Assert.That(world.Requests, Is.EqualTo(1));
            Tick(brain.Settings.RelocateCooldown - 0.3f);
            Assert.That(world.Requests, Is.EqualTo(1));
            Tick(0.5f);
            Assert.That(world.Requests, Is.EqualTo(2));
        }

        [Test]
        public void BeingShotRevealsThePlayerAndTriggersRetreat()
        {
            senses.TookDamage = true;
            senses.PlayerPosition = new Vector3(4f, 0f, 12f);
            senses.PlayerDistance = 12.6f;
            senses.ArrivedAtGoal = false;
            Tick();
            Assert.That(brain.Alerted, Is.True);
            Assert.That(brain.LastKnown, Is.EqualTo(senses.PlayerPosition));
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Retreat));
        }

        [Test]
        public void RushingPlayerTriggersRetreatUnlessAShotIsAlreadyDrawn()
        {
            senses.SeesPlayer = true;
            Tick(1.1f);
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
            senses.PlayerPosition = new Vector3(0f, 0f, 7f);
            senses.PlayerDistance = 7f;
            senses.ClosingSpeed = 3f;
            senses.BowCommitted = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek), "A drawn shot is worth finishing.");
            senses.BowCommitted = false;
            senses.ArrivedAtGoal = false;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Retreat));
        }

        [Test]
        public void ArrivingAfterRetreatHidesBeforeTheNextPeek()
        {
            senses.PlayerDistance = 3f;
            senses.ArrivedAtGoal = false;
            Tick();
            senses.PlayerDistance = 12f;
            senses.PlayerPosition = new Vector3(0f, 0f, 12f);
            senses.ArrivedAtGoal = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Hide));
        }

        [Test]
        public void ArrivingAfterRetreatWithThePlayerInViewShootsAtOnce()
        {
            senses.PlayerDistance = 3f;
            senses.PlayerPosition = new Vector3(0f, 0f, 3f);
            senses.ArrivedAtGoal = false;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Retreat));
            senses.Position = world.Next.Cover;
            senses.PlayerPosition = new Vector3(0f, 0f, 2f);
            senses.PlayerDistance = AmbusherBrain.Flat(senses.Position, senses.PlayerPosition);
            senses.SeesPlayer = true;
            senses.ArrivedAtGoal = true;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Peek));
            Assert.That(brain.Goal, Is.EqualTo(senses.Position));
            Assert.That(brain.Post.Cover, Is.EqualTo(world.Next.Cover), "He ducks back where he stands after the burst.");
            Assert.That(brain.WantsToShoot, Is.True);
        }

        [Test]
        public void StalledRouteRequestsANewPostAwayFromTheFailedOne()
        {
            senses.SeesPlayer = true;
            senses.ArrivedAtGoal = false;
            Tick();
            Vector3 failedGoal = brain.Goal;
            senses.NavigationStalled = true;
            Tick();
            Assert.That(world.Requests, Is.EqualTo(1));
            Assert.That(world.LastFailed, Is.EqualTo(failedGoal));
        }

        [Test]
        public void DeadAmbusherStopsDeciding()
        {
            senses.SeesPlayer = true;
            Tick(1.1f);
            brain.Kill();
            senses.PlayerDistance = 1f;
            Tick();
            Assert.That(brain.State, Is.EqualTo(AmbusherState.Dead));
            Assert.That(brain.WantsToShoot, Is.False);
            Assert.That(world.Requests, Is.Zero);
        }
    }
}

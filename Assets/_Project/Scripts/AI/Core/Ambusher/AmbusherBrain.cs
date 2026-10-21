using UnityEngine;

namespace Lockdown.AI.Core.Ambusher
{
    public enum AmbusherState { Unaware, Hide, Peek, Retreat, Hunt, Dead }

    public struct AmbusherPost
    {
        public Vector3 Cover;
        public Vector3 Peek;
        public AmbusherPost(Vector3 cover, Vector3 peek) { Cover = cover; Peek = peek; }
    }

    // Level queries the decision layer needs. The Unity agent answers them with NavMesh and raycasts.
    public interface IAmbusherWorld
    {
        bool TryChoosePost(Vector3 self, Vector3 threat, bool flee, AmbusherPost current, Vector3? failedPost, out AmbusherPost post);
        bool IsHidden(Vector3 point, Vector3 threat);
        // A point that can see the last known position from a new angle. Even attempts try one side, odd the other.
        bool TryChooseFlank(Vector3 self, Vector3 lastKnown, int attempt, out Vector3 point);
    }

    public struct AmbusherSenses
    {
        public Vector3 Position;
        public bool SeesPlayer;
        public Vector3 PlayerPosition;
        // Straight-line distance; close footsteps are heard even without sight.
        public float PlayerDistance;
        // Speed at which a visible player approaches, in metres per second.
        public float ClosingSpeed;
        public bool TookDamage;
        public bool ArrivedAtGoal;
        public bool NavigationStalled;
        public int ShotsFired;
        // Drawing or holding: leaving now would waste the shot.
        public bool BowCommitted;
    }

    public sealed class AmbusherSettings
    {
        public float NearbyRange = 4f;
        public float PanicRange = 5f;
        public float ApproachWatchRange = 8f;
        public float ClosingSpeedThreshold = 2.5f;
        public float PeekWindow = 4f;
        public float FirstHide = 0.5f;
        public float HideAfterRetreat = 0.4f;
        public float HideAfterBurst = 1.2f;
        public float HideAfterTimeout = 0.8f;
        public float HideAfterReposition = 0.6f;
        public float RelocateCooldown = 2.5f;
        // Unseen this long at a peek, he goes looking instead of waiting.
        public float LoseSightDelay = 1f;
        public int FlankAttempts = 3;
        public float HuntGiveUp = 12f;
        public int BurstSize = 2;
    }

    // Port of the prototype Ambusher: hide, peek out for a two-shot burst, hide again, and
    // relocate when hit or approached. Added for the museum: a short retreat that ends in a
    // snap shot, and a hunt that circles the last known position when the player takes cover.
    public sealed class AmbusherBrain
    {
        private readonly AmbusherSettings settings;
        private float coverTimer;
        private float relocateTimer;
        private float unseen;
        private float huntTime;
        private int huntAttempt;
        private int attackStart;
        private Vector3? failedPost;

        public AmbusherState State { get; private set; } = AmbusherState.Unaware;
        public AmbusherPost Post { get; private set; }
        public Vector3 HuntPoint { get; private set; }
        public bool Investigating { get; private set; }
        public Vector3 LastKnown { get; private set; }
        public bool Alerted { get; private set; }
        public bool WantsToShoot { get; private set; }
        public int Repositions { get; private set; }
        public Vector3 Goal => State == AmbusherState.Peek ? Post.Peek : State == AmbusherState.Hunt ? HuntPoint : Post.Cover;
        public AmbusherSettings Settings => settings;

        public AmbusherBrain(Vector3 home, AmbusherSettings settings = null)
        {
            this.settings = settings ?? new AmbusherSettings();
            Post = new AmbusherPost(home, home);
            LastKnown = home;
        }

        public void Tick(in AmbusherSenses senses, float deltaTime, IAmbusherWorld world)
        {
            WantsToShoot = false;
            if (State == AmbusherState.Dead || deltaTime <= 0f) return;
            relocateTimer = Mathf.Max(0f, relocateTimer - deltaTime);
            bool firstContact = senses.SeesPlayer && !Alerted;
            bool nearby = senses.PlayerDistance < settings.NearbyRange;
            // Seeing, hearing close footsteps, or being shot all reveal where the player is.
            if (senses.SeesPlayer || nearby || senses.TookDamage)
            {
                LastKnown = senses.PlayerPosition;
                Alerted = true;
            }
            if (!Alerted) return;
            unseen = senses.SeesPlayer ? 0f : unseen + deltaTime;

            float distance = Flat(senses.Position, LastKnown);
            bool committed = senses.BowCommitted && (State == AmbusherState.Peek || State == AmbusherState.Hunt);
            bool rushed = senses.SeesPlayer && (distance < settings.PanicRange
                || (distance < settings.ApproachWatchRange && senses.ClosingSpeed > settings.ClosingSpeedThreshold && !committed));
            if ((senses.TookDamage || nearby || rushed) && (State != AmbusherState.Retreat || relocateTimer <= 0f))
                Reposition(senses, world, true);
            else if (firstContact && State != AmbusherState.Retreat && !world.IsHidden(Post.Cover, LastKnown))
            {
                // Take an opening volley from where he stands, already knowing where to hide next.
                Reposition(senses, world, false);
                SnapShot(senses);
            }
            else if (State == AmbusherState.Hunt && senses.SeesPlayer)
            {
                Reposition(senses, world, false);
                SnapShot(senses);
            }
            if (State == AmbusherState.Unaware)
            {
                State = AmbusherState.Hide;
                coverTimer = settings.FirstHide;
            }
            if (senses.NavigationStalled)
            {
                failedPost = Goal;
                if (State == AmbusherState.Hunt) NextHuntPoint(senses, world);
                else Reposition(senses, world, State == AmbusherState.Retreat || nearby);
                return;
            }

            bool arrived = senses.ArrivedAtGoal;
            if (State == AmbusherState.Retreat)
            {
                if (!arrived) return;
                // Turn and shoot at once if the player is in view; otherwise only a brief duck.
                if (senses.SeesPlayer) SnapShot(senses);
                else { State = AmbusherState.Hide; coverTimer = settings.HideAfterRetreat; }
                return;
            }
            if (State == AmbusherState.Hunt)
            {
                huntTime += deltaTime;
                if (!arrived) return;
                if (Investigating || huntTime > settings.HuntGiveUp) Reposition(senses, world, false);
                else NextHuntPoint(senses, world);
                return;
            }
            // Hiding and firing windows count only after he reaches his post.
            if (arrived) coverTimer -= deltaTime;
            if (State == AmbusherState.Hide)
            {
                if (arrived && coverTimer <= 0f)
                {
                    State = AmbusherState.Peek;
                    coverTimer = settings.PeekWindow;
                    attackStart = senses.ShotsFired;
                }
                return;
            }

            WantsToShoot = arrived && senses.SeesPlayer;
            if (senses.ShotsFired - attackStart >= settings.BurstSize)
            {
                State = AmbusherState.Hide;
                coverTimer = settings.HideAfterBurst;
                WantsToShoot = false;
            }
            else if (arrived && !senses.SeesPlayer && unseen >= settings.LoseSightDelay && !senses.BowCommitted)
                StartHunt(senses, world);
            else if (arrived && coverTimer <= 0f && !senses.BowCommitted)
            {
                State = AmbusherState.Hide;
                coverTimer = settings.HideAfterTimeout;
                WantsToShoot = false;
            }
        }

        // Shoot from where he stands; his cover post is kept for after the burst.
        private void SnapShot(in AmbusherSenses senses)
        {
            Post = new AmbusherPost(Post.Cover, senses.Position);
            State = AmbusherState.Peek;
            coverTimer = settings.PeekWindow;
            attackStart = senses.ShotsFired;
            WantsToShoot = senses.SeesPlayer;
        }

        private void StartHunt(in AmbusherSenses senses, IAmbusherWorld world)
        {
            State = AmbusherState.Hunt;
            huntAttempt = 0;
            huntTime = 0f;
            Investigating = false;
            WantsToShoot = false;
            NextHuntPoint(senses, world);
        }

        // Circle the last known position, alternating sides, then walk to it as a last resort.
        private void NextHuntPoint(in AmbusherSenses senses, IAmbusherWorld world)
        {
            if (!Investigating && huntAttempt < settings.FlankAttempts && world != null
                && world.TryChooseFlank(senses.Position, LastKnown, huntAttempt, out var flank))
            {
                HuntPoint = flank;
                huntAttempt++;
                return;
            }
            HuntPoint = LastKnown;
            Investigating = true;
        }

        private void Reposition(in AmbusherSenses senses, IAmbusherWorld world, bool flee)
        {
            if (world != null && world.TryChoosePost(senses.Position, LastKnown, flee, Post, failedPost, out var post)) Post = post;
            State = flee ? AmbusherState.Retreat : AmbusherState.Hide;
            coverTimer = settings.HideAfterReposition;
            relocateTimer = settings.RelocateCooldown;
            attackStart = senses.ShotsFired;
            Investigating = false;
            Repositions++;
        }

        public void Kill()
        {
            State = AmbusherState.Dead;
            WantsToShoot = false;
        }

        public static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}

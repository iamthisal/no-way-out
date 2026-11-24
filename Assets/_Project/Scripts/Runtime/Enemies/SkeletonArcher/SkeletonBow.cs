using UnityEngine;

namespace Lockdown.Enemies
{
    // Runs one shot cycle at a time: draw (the audible warning), hold, release, then nock the next arrow.
    // The archer agent calls Tick every frame with whether it currently wants a shot.
    [DisallowMultipleComponent]
    public sealed class SkeletonBow : MonoBehaviour
    {
        public enum Phase { Idle, Drawing, Holding, Releasing, Nocking }

        private static readonly int DrawState = Animator.StringToHash("Draw");
        private static readonly int HoldState = Animator.StringToHash("AimHold");
        private static readonly int ReleaseState = Animator.StringToHash("Release");
        private static readonly int NockState = Animator.StringToHash("Nock");

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform arrowTip;
        [SerializeField] private Renderer[] nockedArrow = new Renderer[0];
        [SerializeField] private ArrowProjectile arrowPrefab;
        [SerializeField] private AudioSource voice;
        [SerializeField] private AudioClip drawSound;
        [SerializeField] private AudioClip releaseSound;
        [Header("Timing (matches the animation clips)")]
        [SerializeField, Min(0.1f)] private float drawDuration = 0.7f;
        [SerializeField, Min(0f)] private float minimumHold = 0.1f;
        [SerializeField, Min(0.05f)] private float releaseDuration = 0.3f;
        [SerializeField, Min(0.1f)] private float nockDuration = 0.6f;
        // Aim stops tracking this long before the earliest release, so a last-moment dodge works.
        [SerializeField, Min(0f)] private float aimLock = 0.12f;
        [SerializeField, Min(0f)] private float lostSightGrace = 0.6f;
        [Header("Arrow")]
        [SerializeField, Min(1f)] private float arrowSpeed = 24f;
        [SerializeField, Min(0f)] private float arrowGravity = 3f;
        [SerializeField, Min(0f)] private float damage = 18f;
        [SerializeField, Min(0f)] private float spreadDegrees = 1.2f;
        [SerializeField, Min(0f)] private float spoiledSpreadDegrees = 7f;
        [SerializeField, Min(0f)] private float maxLead = 0.4f;

        private float phaseTime;
        private float lostSight;
        private float silentNock;
        private float spoiledUntil = float.NegativeInfinity;
        private Vector3 lockedAim;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public bool IsActive => CurrentPhase != Phase.Idle;
        public bool IsCommitted => CurrentPhase == Phase.Drawing || CurrentPhase == Phase.Holding;
        public bool ArrowInHand { get; private set; } = true;
        public bool AimSpoiled => Time.time < spoiledUntil;
        public int ShotsFired { get; private set; }
        public ArrowProjectile LastArrow { get; private set; }
        public Vector3 LockedAim => lockedAim;

        private void Awake() { ShowArrow(ArrowInHand); }

        public void Tick(bool wantShot, Vector3 aimPoint, Vector3 targetVelocity, float deltaTime)
        {
            if (!isActiveAndEnabled) return;
            phaseTime += deltaTime;
            switch (CurrentPhase)
            {
                case Phase.Idle:
                    if (!ArrowInHand)
                    {
                        // Re-arm out of sight without the full nocking animation.
                        silentNock += deltaTime;
                        if (silentNock >= nockDuration) { ArrowInHand = true; ShowArrow(true); }
                    }
                    if (wantShot) Enter(ArrowInHand ? Phase.Drawing : Phase.Nocking);
                    if (CurrentPhase == Phase.Drawing) Track(aimPoint, targetVelocity);
                    break;
                case Phase.Drawing:
                case Phase.Holding:
                    float sinceDraw = CurrentPhase == Phase.Drawing ? phaseTime : drawDuration + phaseTime;
                    if (wantShot && sinceDraw < drawDuration + minimumHold - aimLock) Track(aimPoint, targetVelocity);
                    lostSight = wantShot ? 0f : lostSight + deltaTime;
                    if (lostSight > lostSightGrace) { Cancel(); break; }
                    if (CurrentPhase == Phase.Drawing && phaseTime >= drawDuration) Enter(Phase.Holding);
                    else if (CurrentPhase == Phase.Holding && wantShot && phaseTime >= minimumHold) Release();
                    break;
                case Phase.Releasing:
                    if (phaseTime >= releaseDuration) Enter(Phase.Nocking);
                    break;
                case Phase.Nocking:
                    if (phaseTime >= nockDuration * 0.4f) ShowArrow(true);
                    if (phaseTime >= nockDuration)
                    {
                        ArrowInHand = true;
                        Enter(wantShot ? Phase.Drawing : Phase.Idle);
                    }
                    break;
            }
        }

        private void Track(Vector3 aimPoint, Vector3 targetVelocity)
        {
            float distance = arrowTip != null ? Vector3.Distance(arrowTip.position, aimPoint) : 0f;
            lockedAim = aimPoint + targetVelocity * Mathf.Min(maxLead, distance / arrowSpeed);
        }

        private void Release()
        {
            ShotsFired++;
            ArrowInHand = false;
            ShowArrow(false);
            if (voice != null && releaseSound != null) voice.PlayOneShot(releaseSound);
            if (arrowPrefab != null && arrowTip != null)
            {
                Vector3 origin = arrowTip.position;
                float flightTime = Vector3.Distance(origin, lockedAim) / arrowSpeed;
                // Aim above the target by the drop the arrow will suffer on the way.
                Vector3 direction = (lockedAim + Vector3.up * (0.5f * arrowGravity * flightTime * flightTime) - origin).normalized;
                float spread = AimSpoiled ? spoiledSpreadDegrees : spreadDegrees;
                Vector3 side = Vector3.Cross(direction, Vector3.up);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                direction = Quaternion.AngleAxis(Random.Range(0f, 360f), direction)
                    * Quaternion.AngleAxis(Random.Range(0f, spread), side.normalized) * direction;
                LastArrow = Instantiate(arrowPrefab, origin, Quaternion.LookRotation(direction));
                LastArrow.Launch(origin, direction * arrowSpeed, arrowGravity, damage, transform);
            }
            Enter(Phase.Releasing);
        }

        private void Enter(Phase phase)
        {
            CurrentPhase = phase;
            phaseTime = 0f;
            lostSight = 0f;
            silentNock = 0f;
            if (animator == null || !animator.isActiveAndEnabled) return;
            if (phase == Phase.Drawing)
            {
                animator.CrossFadeInFixedTime(DrawState, 0.18f);
                if (voice != null && drawSound != null) voice.PlayOneShot(drawSound);
            }
            else if (phase == Phase.Holding) animator.CrossFadeInFixedTime(HoldState, 0.05f);
            else if (phase == Phase.Releasing) animator.CrossFadeInFixedTime(ReleaseState, 0.02f);
            else if (phase == Phase.Nocking) animator.CrossFadeInFixedTime(NockState, 0.08f);
        }

        // Lower the bow at once, for example to run. An arrow already on the string stays in hand.
        public void Cancel()
        {
            if (CurrentPhase == Phase.Nocking && phaseTime >= nockDuration * 0.4f) ArrowInHand = true;
            ShowArrow(ArrowInHand);
            if (voice != null) voice.Stop();
            Enter(Phase.Idle);
        }

        public void SpoilAim(float seconds) { spoiledUntil = Mathf.Max(spoiledUntil, Time.time + seconds); }

        public void Disarm()
        {
            Cancel();
            enabled = false;
        }

        private void ShowArrow(bool visible)
        {
            foreach (var renderer in nockedArrow) if (renderer != null) renderer.enabled = visible;
        }
    }
}

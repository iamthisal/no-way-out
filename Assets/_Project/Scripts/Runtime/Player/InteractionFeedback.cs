using Lockdown.Interfaces;
using Lockdown.World;
using UnityEngine;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    public sealed class InteractionFeedback : MonoBehaviour
    {
        [SerializeField] private AudioClip doorHinge;
        [SerializeField] private AudioClip doorLatch;
        [SerializeField] private AudioClip scanAccepted;
        [SerializeField] private AudioClip scanDenied;
        [SerializeField] private AudioClip cardTap;
        private AudioSource audioSource;
        private float nextDeniedCue;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        internal void Report(IInteractable target, bool succeeded, LootInventory inventory,
            int previousItemCount)
        {
            var reader = target as DoorReader;
            var door = target as Door ?? reader?.Door;
            if (door != null)
            {
                if (succeeded) door.PlayMovementFeedback(this);
                if (door.RequiredIdentityTag != null && (!succeeded || door.IsOpen))
                    ReportScan(succeeded);
                return;
            }

            if (target is SecurityConsole console)
            {
                if (succeeded || !inventory.HasAuthorizedExitTag(console.IdentityTag))
                    ReportScan(succeeded);
                return;
            }

            if (target is MuseumExit)
            {
                if (succeeded)
                {
                    Play(scanAccepted, 0.7f);
                    Play(doorLatch, 0.5f);
                }
                else ReportScan(false);
                return;
            }

            if (!succeeded) return;
            if (target is LootContainer container && container.IsMoving) Play(doorHinge, 0.32f);
            if (inventory.ItemCount > previousItemCount && inventory.Items[previousItemCount].Value == 0)
                Play(cardTap, 0.65f);
        }

        internal void PlayHinge() => Play(doorHinge, 0.62f);
        internal void PlayLatch() => Play(doorLatch, 0.58f);

        private void ReportScan(bool accepted)
        {
            if (!accepted)
            {
                if (Time.unscaledTime < nextDeniedCue) return;
                nextDeniedCue = Time.unscaledTime + 0.38f;
            }

            Play(accepted ? scanAccepted : scanDenied, 0.72f);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
        }
    }
}

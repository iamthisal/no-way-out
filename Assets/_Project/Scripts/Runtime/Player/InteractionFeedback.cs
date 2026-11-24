using System.Collections;
using Lockdown.Interfaces;
using Lockdown.World;
using UnityEngine;
using UnityEngine.Rendering;

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
        private GameObject scanCard;
        private Coroutine scan;
        private float nextDeniedCue;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        private void OnDisable()
        {
            if (scan != null) StopCoroutine(scan);
            scan = null;
            if (scanCard != null) Destroy(scanCard);
            scanCard = null;
        }

        internal void Report(IInteractable target, bool succeeded, LootInventory inventory,
            int previousItemCount, Camera camera)
        {
            var reader = target as DoorReader;
            var door = target as Door ?? reader?.Door;
            if (door != null)
            {
                if (succeeded) door.PlayMovementFeedback(this);
                if (door.RequiredIdentityTag != null && (!succeeded || door.IsOpen))
                    ReportScan(succeeded, door.RequiredIdentityTag, reader != null ? reader : door,
                        inventory, camera);
                return;
            }

            if (target is SecurityConsole console)
            {
                if (succeeded || !inventory.HasAuthorizedExitTag(console.IdentityTag))
                    ReportScan(succeeded, console.IdentityTag, console, inventory, camera);
                return;
            }

            if (target is MuseumExit exit)
            {
                if (succeeded)
                {
                    Play(scanAccepted, 0.7f);
                    Play(doorLatch, 0.5f);
                }
                else ReportScan(false, exit.IdentityTag, exit, inventory, camera);
                return;
            }

            if (!succeeded) return;
            if (target is LootContainer container && container.IsMoving) Play(doorHinge, 0.32f);
            if (inventory.ItemCount > previousItemCount && inventory.Items[previousItemCount].Value == 0)
                Play(cardTap, 0.65f);
        }

        internal void PlayHinge() => Play(doorHinge, 0.62f);
        internal void PlayLatch() => Play(doorLatch, 0.58f);

        private void ReportScan(bool accepted, Transform tag, Component terminal, LootInventory inventory, Camera camera)
        {
            if (!accepted)
            {
                if (Time.unscaledTime < nextDeniedCue) return;
                nextDeniedCue = Time.unscaledTime + 0.38f;
            }

            AudioClip signal = accepted ? scanAccepted : scanDenied;
            var cardMesh = inventory.HasItem(tag) ? tag.GetComponentInChildren<MeshFilter>(true) : null;
            if (camera == null || cardMesh == null || cardMesh.sharedMesh == null ||
                !cardMesh.TryGetComponent<MeshRenderer>(out var appearance))
            {
                Play(signal, 0.72f);
                return;
            }

            if (scan != null)
            {
                StopCoroutine(scan);
                Destroy(scanCard);
            }

            scanCard = new GameObject("Identity Tag Scan");
            scanCard.transform.SetParent(transform, true);
            var visual = new GameObject("Card model");
            visual.transform.SetParent(scanCard.transform, false);
            var mesh = cardMesh.sharedMesh;
            var size = mesh.bounds.size;
            float scale = 0.16f / Mathf.Max(0.001f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = -mesh.bounds.center * scale;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = appearance.sharedMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var collider = terminal.GetComponent<Collider>();
            var scanner = collider != null ? collider.bounds.center : terminal.transform.position;
            if (collider != null) scanner = collider.ClosestPoint(camera.transform.position);
            scan = StartCoroutine(AnimateScan(camera.transform, scanner, signal));
        }

        private IEnumerator AnimateScan(Transform camera, Vector3 scanner, AudioClip signal)
        {
            const float approachTime = 0.16f;
            const float withdrawTime = 0.23f;
            var towardPlayer = (camera.position - scanner).normalized;
            scanner += towardPlayer * 0.045f;
            var readerRotation = Quaternion.LookRotation(-towardPlayer, Vector3.up) * Quaternion.Euler(0f, 0f, -12f);
            for (float elapsed = 0f; elapsed < approachTime; elapsed += Time.unscaledDeltaTime)
            {
                SetCardPose(camera, scanner, readerRotation, Mathf.SmoothStep(0f, 1f, elapsed / approachTime));
                yield return null;
            }
            SetCardPose(camera, scanner, readerRotation, 1f);
            Play(cardTap, 0.55f);
            Play(signal, 0.72f);
            for (float elapsed = 0f; elapsed < withdrawTime; elapsed += Time.unscaledDeltaTime)
            {
                SetCardPose(camera, scanner, readerRotation, Mathf.SmoothStep(1f, 0f, elapsed / withdrawTime));
                yield return null;
            }
            Destroy(scanCard);
            scanCard = null;
            scan = null;
        }

        private void SetCardPose(Transform camera, Vector3 scanner, Quaternion readerRotation, float progress)
        {
            var hand = camera.TransformPoint(new Vector3(0.19f, -0.17f, 0.48f));
            var handRotation = camera.rotation * Quaternion.Euler(0f, -20f, -22f);
            scanCard.transform.SetPositionAndRotation(Vector3.Lerp(hand, scanner, progress),
                Quaternion.Slerp(handRotation, readerRotation, progress));
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
        }
    }
}

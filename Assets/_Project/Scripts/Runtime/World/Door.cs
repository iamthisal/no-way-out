using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>Attach to a hinge pivot; put the moving panel and its colliders beneath it.</summary>
    [DisallowMultipleComponent]
    public sealed class Door : MonoBehaviour, IInteractable
    {
        [SerializeField, Range(-150f, 150f)] private float openAngle = 95f;
        [SerializeField, Min(1f)] private float degreesPerSecond = 180f;
        [SerializeField] private bool startsOpen;
        [SerializeField] private Transform requiredIdentityTag;
        [Header("Route block")]
        [SerializeField] private Transform blockingCrate;
        [SerializeField, Min(0.1f)] private float crateClearanceDistance = 1.5f;
        private Quaternion closedRotation;
        private float currentAngle;
        private InteractionFeedback closingFeedback;
        internal Transform RequiredIdentityTag => requiredIdentityTag;

        // IsOpen is the requested state, so another press can reverse a moving door.
        public bool IsOpen { get; private set; }
        public bool IsMoving => currentAngle != TargetAngle;
        public bool IsBlocked => !IsOpen && blockingCrate != null &&
            Vector3.ProjectOnPlane(blockingCrate.position - transform.position, Vector3.up).sqrMagnitude <
            crateClearanceDistance * crateClearanceDistance;
        public string InteractionLabel => IsBlocked ? "Push crate clear of doorway" : IsOpen ? "Close door" :
            requiredIdentityTag != null ? "Use identity tag" : "Open door";
        private float TargetAngle => IsOpen ? openAngle : 0f;

        private void Awake()
        {
            closedRotation = transform.localRotation;
            IsOpen = startsOpen;
            currentAngle = TargetAngle;
            transform.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
            if (!IsMoving && closingFeedback != null)
            {
                closingFeedback.PlayLatch();
                closingFeedback = null;
            }
        }

        internal void PlayMovementFeedback(InteractionFeedback feedback)
        {
            feedback.PlayHinge();
            closingFeedback = IsOpen ? null : feedback;
        }

        private void Update()
        {
            currentAngle = Mathf.MoveTowards(currentAngle, TargetAngle,
                Mathf.Max(1f, degreesPerSecond) * Time.deltaTime);
            transform.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
        }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null) return false;
            var target = interaction.CurrentTarget;
            if (target != (IInteractable)this && !(target is DoorReader reader && reader.Door == this)) return false;
            if (IsBlocked) return false;
            if (!IsOpen && !HasAccess(interactor.GetComponent<LootInventory>())) return false;
            IsOpen = !IsOpen;
            return true;
        }

        internal string GetPrompt(LootInventory inventory) =>
            IsBlocked ? "Push crate clear of doorway" :
            !IsOpen && !HasAccess(inventory) ? "Identity tag required" : InteractionLabel;

        private bool HasAccess(LootInventory inventory) =>
            requiredIdentityTag == null || (inventory != null && inventory.HasItem(requiredIdentityTag));
    }
}

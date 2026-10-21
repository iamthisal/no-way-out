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
        private Quaternion closedRotation;
        private float currentAngle;
        private DoorLock doorLock;

        // IsOpen is the requested state, so another press can reverse a moving door.
        public bool IsOpen { get; private set; }
        public bool IsMoving => currentAngle != TargetAngle;
        public string InteractionLabel => doorLock != null && doorLock.IsLocked ? doorLock.Label : IsOpen ? "Close door" : "Open door";
        private float TargetAngle => IsOpen ? openAngle : 0f;

        private void Awake()
        {
            doorLock = GetComponent<DoorLock>();
            closedRotation = transform.localRotation;
            IsOpen = startsOpen;
            currentAngle = TargetAngle;
            transform.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
        }

        private void Update()
        {
            currentAngle = Mathf.MoveTowards(currentAngle, TargetAngle,
                Mathf.Max(1f, degreesPerSecond) * Time.deltaTime);
            transform.localRotation = closedRotation * Quaternion.Euler(0f, currentAngle, 0f);
        }

        // For scripts that move a door without a player, such as the robots' bay door. Ignores any lock.
        public void SetOpen(bool open) { IsOpen = open; }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            if (doorLock != null && !doorLock.TryUnlock(interactor)) return false;
            IsOpen = !IsOpen;
            return true;
        }
    }
}

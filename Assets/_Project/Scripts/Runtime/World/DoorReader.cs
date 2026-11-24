using Lockdown.Interfaces;
using UnityEngine;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    public sealed class DoorReader : MonoBehaviour, IInteractable
    {
        [SerializeField] private Door door;

        internal Door Door => door;
        public string InteractionLabel => door != null ? door.InteractionLabel : "";

        public bool TryInteract(GameObject interactor) =>
            isActiveAndEnabled && door != null && door.TryInteract(interactor);
    }
}

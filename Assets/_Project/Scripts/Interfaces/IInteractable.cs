using UnityEngine;

namespace Lockdown.Interfaces
{
    public interface IInteractable
    {
        string InteractionLabel { get; }
        bool TryInteract(GameObject interactor);
    }
}

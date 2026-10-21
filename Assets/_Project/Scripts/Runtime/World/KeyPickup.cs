using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>A key lying in the museum, such as the Storage keycard. Taking it adds its id to the player's keys.</summary>
    [DisallowMultipleComponent]
    public sealed class KeyPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private string keyId = "StorageKeycard";
        [SerializeField] private string displayName = "Storage keycard";

        public string KeyId => keyId;
        public bool IsCollected { get; private set; }
        public string InteractionLabel => "Take the " + displayName;

        public bool TryInteract(GameObject interactor)
        {
            if (IsCollected || !isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            PlayerKeys.For(interactor).Add(keyId);
            IsCollected = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}

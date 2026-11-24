using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    public sealed class SecurityConsole : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform identityTag;

        public string InteractionLabel => "Authorize Identity Tag";

        internal string GetPrompt(LootInventory inventory)
        {
            if (!inventory.HasItem(identityTag)) return "Identity Tag required";
            return inventory.HasAuthorizedExitTag(identityTag) ? "Identity Tag authorized" : InteractionLabel;
        }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            var inventory = interactor.GetComponent<LootInventory>();
            return inventory != null && inventory.AuthorizeExitTag(identityTag);
        }
    }
}

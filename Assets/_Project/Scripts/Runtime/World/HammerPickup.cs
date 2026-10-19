using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    public sealed class HammerPickup : MonoBehaviour, IInteractable
    {
        public bool IsCollected { get; private set; }
        public string InteractionLabel => "Take hammer (3 to equip)";

        public bool TryInteract(GameObject interactor)
        {
            if (IsCollected || !isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            var equipment = interactor.GetComponent<PlayerEquipment>();
            if (interaction == null || equipment == null || interaction.CurrentTarget != (IInteractable)this) return false;
            if (!equipment.TryCollectHammer()) return false;
            IsCollected = true;
            gameObject.SetActive(false);
            return true;
        }
    }
}

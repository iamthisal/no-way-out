using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>The pistol kept in a storage place such as the Security room locker. Taking it equips it.</summary>
    [DisallowMultipleComponent]
    public sealed class PistolPickup : MonoBehaviour, IInteractable
    {
        public bool IsCollected { get; private set; }
        public string InteractionLabel => "Take the pistol (2 to equip)";

        public bool TryInteract(GameObject interactor)
        {
            if (IsCollected || !isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            var equipment = interactor.GetComponent<PlayerEquipment>();
            if (interaction == null || equipment == null || interaction.CurrentTarget != (IInteractable)this) return false;
            if (!equipment.TryCollectPistol()) return false;
            IsCollected = true;
            // The locker stays; it just has nothing left to offer.
            Destroy(this);
            return true;
        }
    }
}

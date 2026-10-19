using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    public sealed class LootItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private string itemName = "Museum Relic";
        [SerializeField, Min(1)] private int value = 100;
        [SerializeField] private BreakableGlass displayGlass;
        public string ItemName => string.IsNullOrWhiteSpace(itemName) ? gameObject.name : itemName;
        public int Value => Mathf.Max(1, value);
        public bool IsCollected { get; private set; }
        public BreakableGlass DisplayGlass => displayGlass;
        public bool IsProtected => displayGlass != null && !displayGlass.IsBroken;
        public string InteractionLabel => IsProtected ? "Break glass to take " + ItemName :
            "Take " + ItemName + " ($" + Value + ")";

        public bool TryInteract(GameObject interactor)
        {
            if (IsCollected || IsProtected || !isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            var inventory = interactor.GetComponent<LootInventory>();
            if (interaction == null || inventory == null || interaction.CurrentTarget != (IInteractable)this) return false;
            IsCollected = true;
            inventory.Add(ItemName, Value);
            gameObject.SetActive(false);
            return true;
        }
    }
}

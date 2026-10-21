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
        [SerializeField] private LootContainer lootContainer;
        public string ItemName => string.IsNullOrWhiteSpace(itemName) ? gameObject.name : itemName;
        public int Value => Mathf.Max(1, value);
        public bool IsCollected { get; private set; }
        public BreakableGlass DisplayGlass => displayGlass;
        public bool IsProtected => (displayGlass != null && !displayGlass.IsBroken) ||
            (lootContainer != null && !lootContainer.IsAccessible);
        public string InteractionLabel => IsProtected ?
            (lootContainer != null ? "Open crate to take " : "Break glass to take ") + ItemName :
            "Take " + ItemName + " ($" + Value + ")";

        internal void ConfigureContainerLoot(string name, int itemValue, LootContainer container)
        {
            itemName = name;
            value = itemValue;
            lootContainer = container;
        }

        public bool TryInteract(GameObject interactor)
        {
            if (IsCollected || IsProtected || !isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            var inventory = interactor.GetComponent<LootInventory>();
            if (interaction == null || inventory == null || interaction.CurrentTarget != (IInteractable)this) return false;
            Collect(inventory);
            return true;
        }

        internal bool TryCollectFromContainer(GameObject interactor, LootContainer container)
        {
            if (IsCollected || IsProtected || !isActiveAndEnabled || interactor == null ||
                container == null || lootContainer != container) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            var inventory = interactor.GetComponent<LootInventory>();
            if (interaction == null || inventory == null || interaction.CurrentTarget != (IInteractable)container) return false;
            Collect(inventory);
            return true;
        }

        private void Collect(LootInventory inventory)
        {
            IsCollected = true;
            inventory.Add(ItemName, Value);
            gameObject.SetActive(false);
        }
    }
}

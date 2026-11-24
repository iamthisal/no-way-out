using System;
using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    public sealed class MuseumExit : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform requiredIdentityTag;
        internal Transform IdentityTag => requiredIdentityTag;
        private LootItem[] requiredLoot = Array.Empty<LootItem>();

        public int RequiredLootCount => requiredLoot.Length;
        public string InteractionLabel => "Scan Identity Tag and exit";

        private void Start()
        {
            requiredLoot = Array.FindAll(FindObjectsByType<LootItem>(FindObjectsInactive.Exclude),
                item => item.isActiveAndEnabled && item.gameObject.scene == gameObject.scene && item.IsExhibitionLoot);
        }

        public int CollectedLootCount(LootInventory inventory)
        {
            if (inventory == null) return 0;
            int collected = 0;
            foreach (var item in requiredLoot)
                if (item != null && inventory.HasItem(item.transform)) collected++;
            return collected;
        }

        public string GetObjective(LootInventory inventory)
        {
            int collected = CollectedLootCount(inventory);
            bool hasTag = HasTag(inventory);
            if (RequiredLootCount == 0 || collected < RequiredLootCount)
            {
                var objective = $"Collect all exhibition loot  {collected}/{RequiredLootCount}";
                if (!hasTag) return objective;
                return objective + (HasClearance(inventory)
                    ? "\nIdentity Tag authorized for exit"
                    : "\nAuthorize the Identity Tag in the guard room");
            }
            if (!hasTag) return "All exhibition loot collected. Find the Identity Tag to exit.";
            return HasClearance(inventory)
                ? "You can exit now. Scan the Identity Tag at the exit."
                : "Exit locked. Authorize your Identity Tag in the guard room.";
        }

        internal string GetPrompt(LootInventory inventory)
        {
            if (!HasAllLoot(inventory)) return "Collect all exhibition loot first";
            if (!HasTag(inventory)) return "Identity Tag required";
            return HasClearance(inventory) ? InteractionLabel : "Authorize Identity Tag in the guard room";
        }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            var inventory = interactor.GetComponent<LootInventory>();
            if (!HasAllLoot(inventory) || !HasClearance(inventory)) return false;
            var vitals = interactor.GetComponent<PlayerVitals>();
            return vitals != null && vitals.Escape();
        }

        private bool HasAllLoot(LootInventory inventory) =>
            RequiredLootCount > 0 && CollectedLootCount(inventory) == RequiredLootCount;

        private bool HasTag(LootInventory inventory) =>
            inventory != null && inventory.HasItem(requiredIdentityTag);

        private bool HasClearance(LootInventory inventory) =>
            inventory != null && inventory.HasAuthorizedExitTag(requiredIdentityTag);
    }
}

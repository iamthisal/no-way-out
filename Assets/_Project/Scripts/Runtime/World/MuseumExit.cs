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
            if (RequiredLootCount == 0 || collected < RequiredLootCount)
                return $"Collect all exhibition loot  {collected}/{RequiredLootCount}";
            return HasTag(inventory)
                ? "You can exit now. Scan the Identity Tag at the exit."
                : "All exhibition loot collected. Find the Identity Tag to exit.";
        }

        internal string GetPrompt(LootInventory inventory)
        {
            if (!HasAllLoot(inventory)) return "Collect all exhibition loot first";
            return HasTag(inventory) ? InteractionLabel : "Identity Tag required";
        }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            var inventory = interactor.GetComponent<LootInventory>();
            if (!HasAllLoot(inventory) || !HasTag(inventory)) return false;
            var vitals = interactor.GetComponent<PlayerVitals>();
            return vitals != null && vitals.Escape();
        }

        private bool HasAllLoot(LootInventory inventory) =>
            RequiredLootCount > 0 && CollectedLootCount(inventory) == RequiredLootCount;

        private bool HasTag(LootInventory inventory) =>
            inventory != null && inventory.HasItem(requiredIdentityTag);
    }
}

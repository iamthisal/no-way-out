using UnityEngine;

namespace Lockdown.World
{
    // Marks a fallen mummy's amulet. The pickup itself is a LootItem; this records which son of
    // Horus it belonged to, so later story beats can check which amulets the player carries.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LootItem))]
    public sealed class MummyAmulet : MonoBehaviour
    {
        [SerializeField] private string godName = "Imsety";

        public string GodName => godName;
        public LootItem Loot => GetComponent<LootItem>();
    }
}

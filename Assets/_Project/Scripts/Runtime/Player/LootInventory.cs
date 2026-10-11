using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    public sealed class LootInventory : MonoBehaviour
    {
        public readonly struct Entry
        {
            public string Name { get; }
            public int Value { get; }
            public Entry(string name, int value) { Name = name; Value = value; }
        }

        private readonly List<Entry> items = new List<Entry>();
        private ReadOnlyCollection<Entry> readOnlyItems;
        public IReadOnlyList<Entry> Items => readOnlyItems ?? (readOnlyItems = items.AsReadOnly());
        public int ItemCount => items.Count;
        public long TotalValue { get; private set; }

        internal void Add(string itemName, int value)
        {
            items.Add(new Entry(itemName, value));
            TotalValue += value;
        }
    }
}

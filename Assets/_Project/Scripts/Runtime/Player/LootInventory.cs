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
            internal Transform Source { get; }
            public Entry(string name, int value) : this(name, value, null) { }
            internal Entry(string name, int value, Transform source) { Name = name; Value = value; Source = source; }
        }

        private readonly List<Entry> items = new List<Entry>();
        private ReadOnlyCollection<Entry> readOnlyItems;
        private Transform authorizedExitTag;
        public IReadOnlyList<Entry> Items => readOnlyItems ?? (readOnlyItems = items.AsReadOnly());
        public int ItemCount => items.Count;
        public long TotalValue { get; private set; }

        internal bool HasItem(Transform source) => source != null && items.Exists(item => item.Source == source);

        internal bool HasAuthorizedExitTag(Transform source) => source != null && authorizedExitTag == source;

        internal bool AuthorizeExitTag(Transform source)
        {
            if (!HasItem(source) || HasAuthorizedExitTag(source)) return false;
            authorizedExitTag = source;
            return true;
        }

        internal void Add(string itemName, int value, Transform source)
        {
            items.Add(new Entry(itemName, value, source));
            TotalValue += value;
        }
    }
}

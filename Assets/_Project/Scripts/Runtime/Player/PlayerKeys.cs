using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lockdown.Player
{
    /// <summary>The keys the player carries, by id. Locks ask for a key id rather than a key object.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerKeys : MonoBehaviour
    {
        private readonly HashSet<string> keys = new HashSet<string>();

        public event Action<string> KeyAdded;
        public IEnumerable<string> Keys => keys;

        // The player prefab does not carry this yet, so the first key added creates it.
        public static PlayerKeys For(GameObject player)
        {
            var found = player.GetComponent<PlayerKeys>();
            return found != null ? found : player.AddComponent<PlayerKeys>();
        }

        public bool Has(string id) => !string.IsNullOrEmpty(id) && keys.Contains(id);

        public bool Add(string id)
        {
            if (string.IsNullOrEmpty(id) || !keys.Add(id)) return false;
            KeyAdded?.Invoke(id);
            return true;
        }
    }
}

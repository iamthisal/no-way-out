using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>Keeps a door shut until the player brings its key. Put it on the hinge beside the Door.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Door))]
    public sealed class DoorLock : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private string keyId = "StorageKeycard";
        [SerializeField] private string keyName = "Storage keycard";
        [SerializeField] private Renderer[] indicators = new Renderer[0];
        [SerializeField] private Color lockedColor = new Color(0.9f, 0.08f, 0.06f);
        [SerializeField] private Color unlockedColor = new Color(0.1f, 0.85f, 0.25f);
        private PlayerKeys playerKeys;
        private MaterialPropertyBlock block;

        public bool IsLocked { get; private set; } = true;
        public string KeyId => keyId;
        public string Label => PlayerHasKey() ? "Unlock with the " + keyName : "Locked. Needs the " + keyName;

        private void Awake() { ShowState(); }

        // True when the door may move: it is already unlocked, or the interactor carries the key.
        public bool TryUnlock(GameObject interactor)
        {
            if (!IsLocked) return true;
            var keys = interactor != null ? interactor.GetComponent<PlayerKeys>() : null;
            if (keys == null || !keys.Has(keyId)) return false;
            IsLocked = false;
            ShowState();
            return true;
        }

        private bool PlayerHasKey()
        {
            if (playerKeys == null) playerKeys = FindAnyObjectByType<PlayerKeys>();
            return playerKeys != null && playerKeys.Has(keyId);
        }

        private void ShowState()
        {
            block ??= new MaterialPropertyBlock();
            Color color = IsLocked ? lockedColor : unlockedColor;
            foreach (var indicator in indicators)
            {
                if (indicator == null) continue;
                indicator.GetPropertyBlock(block);
                block.SetColor(BaseColor, color);
                block.SetColor(EmissionColor, color * 2f);
                indicator.SetPropertyBlock(block);
            }
        }
    }
}

using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>
    /// Keeps a door shut until the player brings its key, or, for a breakable lock, until the hammer
    /// smashes it. Put it on the hinge beside the Door, where blows to the leaf reach it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Door))]
    public sealed class DoorLock : MonoBehaviour, IDamageable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private string keyId = "StorageKeycard";
        [SerializeField] private string keyName = "Storage keycard";
        [SerializeField] private bool breakable;
        // One hammer blow (100) breaks it.
        [SerializeField, Min(1f)] private float strength = 100f;
        [SerializeField] private Renderer[] indicators = new Renderer[0];
        [SerializeField] private Color lockedColor = new Color(0.9f, 0.08f, 0.06f);
        [SerializeField] private Color unlockedColor = new Color(0.1f, 0.85f, 0.25f);
        [SerializeField] private Color brokenColor = new Color(0.04f, 0.04f, 0.04f);
        private PlayerKeys playerKeys;
        private MaterialPropertyBlock block;
        private float damageTaken;

        public bool IsLocked { get; private set; } = true;
        public bool IsBroken { get; private set; }
        public bool IsBreakable => breakable;
        public string KeyId => keyId;
        public string Label => PlayerHasKey() ? "Unlock with the " + keyName
            : breakable ? "Locked. Break the lock with the hammer" : "Locked. Needs the " + keyName;

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

        // Smashing is quiet: it opens the lock without raising the alarm.
        public void TakeDamage(float amount)
        {
            if (!breakable || !IsLocked || !(amount > 0f)) return;
            damageTaken += amount;
            if (damageTaken < strength) return;
            IsLocked = false;
            IsBroken = true;
            ShowState();
        }

        private bool PlayerHasKey()
        {
            if (playerKeys == null) playerKeys = FindAnyObjectByType<PlayerKeys>();
            return playerKeys != null && playerKeys.Has(keyId);
        }

        private void ShowState()
        {
            block ??= new MaterialPropertyBlock();
            Color color = IsBroken ? brokenColor : IsLocked ? lockedColor : unlockedColor;
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

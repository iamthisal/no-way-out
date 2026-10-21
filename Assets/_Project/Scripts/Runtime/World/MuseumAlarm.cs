using System;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>The museum's security alarm. The guard raises it when he spots the player.</summary>
    [DisallowMultipleComponent]
    public sealed class MuseumAlarm : MonoBehaviour
    {
        [SerializeField] private AudioSource siren;
        private GUIStyle style;

        public static MuseumAlarm Current { get; private set; }
        public bool IsOn { get; private set; }
        public string Cause { get; private set; }
        public event Action Raised;
        public event Action Silenced;

        private void OnEnable() { Current = this; }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (siren != null) siren.Stop();
        }

        public void Raise(string cause)
        {
            if (IsOn) return;
            IsOn = true;
            Cause = cause;
            if (siren != null && siren.clip != null) siren.Play();
            Raised?.Invoke();
        }

        public void Silence()
        {
            if (!IsOn) return;
            IsOn = false;
            Cause = null;
            if (siren != null) siren.Stop();
            Silenced?.Invoke();
        }

        private void OnGUI()
        {
            if (!IsOn) return;
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            // Same row as the loot counter on the right.
            style.normal.textColor = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? new Color(1f, 0.2f, 0.15f) : new Color(0.55f, 0.08f, 0.06f);
            GUI.Label(new Rect(24f, 20f, 300f, 32f), "ALARM", style);
        }
    }
}

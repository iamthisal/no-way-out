using System;
using UnityEngine;
using UnityEngine.Events;

namespace Lockdown.World
{
    /// <summary>
    /// The museum's security alarm. Breaking display glass raises it, and so does the guard spotting the
    /// player. While it rings, every new noise or sighting is reported with where it happened, which is
    /// what the security robots hunt by. The Security room's computer switches it off for good.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MuseumAlarm : MonoBehaviour
    {
        public const string GlassCause = "Display glass broken";

        [SerializeField] private AudioSource siren;
        private BreakableGlass[] glasses = new BreakableGlass[0];
        private UnityAction[] listeners = new UnityAction[0];
        private GUIStyle style;

        public static MuseumAlarm Current { get; private set; }
        public bool IsOn { get; private set; }
        public bool IsDisabled { get; private set; }
        public string Cause { get; private set; }
        // Where the latest noise or sighting came from.
        public Vector3 LastAlert { get; private set; }
        public event Action Raised;
        public event Action Silenced;
        public event Action<Vector3> Alerted;

        private void OnEnable() { Current = this; }

        // Every display case is wired to the alarm.
        private void Start()
        {
            glasses = FindObjectsByType<BreakableGlass>(FindObjectsInactive.Include);
            listeners = new UnityAction[glasses.Length];
            for (int i = 0; i < glasses.Length; i++)
            {
                var glass = glasses[i];
                listeners[i] = () => Raise(GlassCause, glass.transform.position);
                glass.OnBroken.AddListener(listeners[i]);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < glasses.Length; i++) if (glasses[i] != null) glasses[i].OnBroken.RemoveListener(listeners[i]);
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (siren != null) siren.Stop();
        }

        public void Raise(string cause) { Raise(cause, transform.position); }

        // Sounds the alarm, or, if it is already ringing, reports a new place to search.
        public void Raise(string cause, Vector3 where)
        {
            if (IsDisabled) return;
            LastAlert = where;
            if (!IsOn)
            {
                IsOn = true;
                Cause = cause;
                if (siren != null && siren.clip != null) siren.Play();
                Raised?.Invoke();
            }
            Alerted?.Invoke(where);
        }

        // Switched off at the Security computer: nothing can raise it again.
        public void Disable()
        {
            IsDisabled = true;
            Silence();
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

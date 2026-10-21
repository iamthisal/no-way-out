using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>
    /// The Security room's computer. It switches the alarm system off for good, ringing or not. Its screen
    /// glows red during the alarm, blue while the system is armed, and goes dark once it is offline.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlarmConsole : MonoBehaviour, IInteractable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer screen;
        [SerializeField] private Color armedColor = new Color(0.04f, 0.1f, 0.3f);
        [SerializeField] private Color alarmColor = new Color(0.8f, 0.05f, 0.04f);
        [SerializeField] private Color offlineColor = new Color(0.02f, 0.02f, 0.025f);
        private MaterialPropertyBlock block;
        private int shownState = -1;

        private static MuseumAlarm Alarm => MuseumAlarm.Current;
        public string InteractionLabel => Alarm == null || Alarm.IsDisabled ? "Alarm system offline"
            : Alarm.IsOn ? "Turn off the alarm" : "Switch off the alarm system";

        private void Update() { if (State() != shownState) ShowScreen(); }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null || Alarm == null || Alarm.IsDisabled) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            Alarm.Disable();
            ShowScreen();
            return true;
        }

        // 0 armed, 1 ringing, 2 offline.
        private static int State() => Alarm == null || Alarm.IsDisabled ? 2 : Alarm.IsOn ? 1 : 0;

        private void ShowScreen()
        {
            shownState = State();
            if (screen == null) return;
            block ??= new MaterialPropertyBlock();
            Color color = shownState == 2 ? offlineColor : shownState == 1 ? alarmColor : armedColor;
            screen.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(EmissionColor, color * 1.5f);
            screen.SetPropertyBlock(block);
        }
    }
}

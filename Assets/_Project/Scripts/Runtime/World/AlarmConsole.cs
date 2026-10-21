using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>The Security room's computer. While the alarm is on, it can turn it off. Its screen glows red during the alarm.</summary>
    [DisallowMultipleComponent]
    public sealed class AlarmConsole : MonoBehaviour, IInteractable
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Renderer screen;
        [SerializeField] private Color quietColor = new Color(0.04f, 0.1f, 0.3f);
        [SerializeField] private Color alarmColor = new Color(0.8f, 0.05f, 0.04f);
        private MaterialPropertyBlock block;
        private bool showingAlarm = true;

        private static bool AlarmOn => MuseumAlarm.Current != null && MuseumAlarm.Current.IsOn;
        public string InteractionLabel => AlarmOn ? "Turn off the alarm" : "Security console: all quiet";

        private void Start() { ShowScreen(); }
        private void Update() { if (AlarmOn != showingAlarm) ShowScreen(); }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || interactor == null || !AlarmOn) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            MuseumAlarm.Current.Silence();
            ShowScreen();
            return true;
        }

        private void ShowScreen()
        {
            showingAlarm = AlarmOn;
            if (screen == null) return;
            block ??= new MaterialPropertyBlock();
            Color color = showingAlarm ? alarmColor : quietColor;
            screen.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            block.SetColor(EmissionColor, color * 1.5f);
            screen.SetPropertyBlock(block);
        }
    }
}

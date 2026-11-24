using System.IO;
using Lockdown.AI.Core.Director;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.AI.Agents
{
    /// <summary>Shows why the director acts. F9 toggles it, F10 writes the decision log as CSV.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpawnDirectorAgent))]
    public sealed class DirectorOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible;
        private SpawnDirectorAgent director;
        private GUIStyle style;

        private void Awake() { director = GetComponent<SpawnDirectorAgent>(); }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f9Key.wasPressedThisFrame) visible = !visible;
            if (keyboard.f10Key.wasPressedThisFrame) ExportCsv();
        }

        [ContextMenu("Export Decision Log")]
        public string ExportCsv()
        {
            if (director == null || director.Log == null) return null;
            string path = Path.Combine(Application.persistentDataPath, "DirectorLog.csv");
            File.WriteAllText(path, director.Log.ToCsv());
            Debug.Log("Director log written to " + path, this);
            return path;
        }

        private void OnGUI()
        {
            if (!visible || director.Brain == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 14, normal = { textColor = Color.white } };
            var area = new Rect(Screen.width - 396f, 16f, 380f, 250f);
            GUI.Box(area, "Spawn director (F9 hide, F10 export)");
            GUI.Label(new Rect(area.x + 12f, area.y + 28f, area.width - 24f, area.height - 32f), Describe(), style);
        }

        private string Describe()
        {
            var brain = director.Brain;
            var seen = director.Observation;
            string text = $"{brain.Phase}   stress {brain.Stress:F2}   intensity {brain.Intensity:F2} / target {brain.Target:F2}\n"
                + $"damage {seen.RecentDamage:F2}  health {seen.HealthFraction:P0}  engaged {seen.EngagedEnemies}  seen {seen.Seen:F2}\n"
                + $"noise {seen.Noise:F2}  loot {seen.LootFraction:P0}  alarm {(seen.AlarmUnanswered ? "unanswered" : "-")}\n"
                + $"guards {seen.Census.Guards}  archers {seen.Census.Archers}  hounds {seen.Census.Hounds}  dormant {seen.Census.Dormant}\n"
                + $"cooldown {brain.Cooldown:F0}s   spawned {director.SpawnCount}   removed {director.Spawned.Removed}\n";
            if (director.Log.Count == 0) return text;
            var decision = director.Log[director.Log.Count - 1].Decision;
            text += $"\nLast: {(decision.Spawn ? "spawn" : "wait")} - {decision.Reason}\n";
            foreach (SpawnOption option in decision.Top) text += $"  {option.Utility:F2}  {option.Kind} at {option.RoomId}\n";
            return text + $"  {decision.WaitUtility:F2}  wait";
        }
    }
}

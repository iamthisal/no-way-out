using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    /// <summary>
    /// Top-centre objective plaque: an OBJECTIVE label over the current goal. The first line is
    /// the goal; any further lines are smaller notes.
    /// </summary>
    internal sealed class ObjectiveHud
    {
        private GUIStyle labelStyle, goalStyle, noteStyle;
        private float stylesScale = -1f;

        public void Draw(string objective)
        {
            if (string.IsNullOrEmpty(objective)) return;
            float scale = Scale(0.6f);
            if (StylesStale(labelStyle, stylesScale, scale))
            {
                stylesScale = scale;
                labelStyle = Style(15, scale);
                goalStyle = Style(22, scale);
                goalStyle.wordWrap = true;
                noteStyle = Style(17, scale);
                noteStyle.wordWrap = true;
            }

            string[] lines = objective.Split('\n');
            string goal = lines[0].Trim();
            string notes = lines.Length > 1 ? string.Join("\n", lines, 1, lines.Length - 1).Trim() : "";
            float pad = 26f * scale;
            float widest = goalStyle.CalcSize(new GUIContent(goal)).x;
            if (notes.Length > 0) widest = Mathf.Max(widest, noteStyle.CalcSize(new GUIContent(notes)).x);
            float width = Mathf.Clamp(widest + pad * 2f, 420f * scale, Mathf.Min(780f * scale, Screen.width - 64f));
            float textWidth = width - pad * 2f;
            float goalHeight = goalStyle.CalcHeight(new GUIContent(goal), textWidth);
            float noteHeight = notes.Length > 0 ? noteStyle.CalcHeight(new GUIContent(notes), textWidth) + 4f * scale : 0f;
            float labelTop = 12f * scale, labelHeight = 20f * scale, gap = 6f * scale;
            float height = labelTop + labelHeight + gap + goalHeight + noteHeight + 14f * scale;

            Color previous = GUI.color;
            var panel = new Rect((Screen.width - width) * 0.5f, 20f * scale, width, height);
            Plaque(panel, scale, 0.84f, Brass);

            float y = panel.y + labelTop;
            Text(new Rect(panel.x, y, panel.width, labelHeight), Spaced("Objective"), labelStyle, Brass);
            y += labelHeight + gap;
            Text(new Rect(panel.x + pad, y, textWidth, goalHeight), goal, goalStyle, Ivory);
            if (notes.Length > 0)
                Text(new Rect(panel.x + pad, y + goalHeight + 4f * scale, textWidth, noteHeight), notes, noteStyle,
                    new Color(Ivory.r, Ivory.g, Ivory.b, 0.72f));
            GUI.color = previous;
        }
    }
}

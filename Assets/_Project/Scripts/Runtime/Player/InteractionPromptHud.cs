using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    /// <summary>
    /// The interaction prompt under the crosshair as a thin walnut bar: a brass key cap for the
    /// binding, then the label.
    /// </summary>
    internal sealed class InteractionPromptHud
    {
        private GUIStyle keyStyle, labelStyle;
        private float stylesScale = -1f;

        public void Draw(string binding, string label)
        {
            if (string.IsNullOrEmpty(label)) return;
            float scale = Scale(0.6f);
            if (StylesStale(keyStyle, stylesScale, scale))
            {
                stylesScale = scale;
                keyStyle = Style(17, scale);
                labelStyle = Style(18, scale, TextAnchor.MiddleLeft);
                labelStyle.wordWrap = true;
            }

            float pad = 8f * scale, key = 26f * scale, gap = 12f * scale, labelPad = 16f * scale;
            float maxWidth = Mathf.Min(620f * scale, Screen.width - 32f);
            float labelWidth = Mathf.Min(labelStyle.CalcSize(new GUIContent(label)).x + 2f,
                maxWidth - pad - key - gap - labelPad);
            float labelHeight = labelStyle.CalcHeight(new GUIContent(label), labelWidth);
            float height = Mathf.Max(40f * scale, labelHeight + 12f * scale);
            float width = pad + key + gap + labelWidth + labelPad;
            var bar = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.5f + 34f * scale, width, height);

            Color previous = GUI.color;
            Fill(bar, new Color(Walnut.r, Walnut.g, Walnut.b, 0.82f));
            Frame(bar, 1f, Brass);
            var cap = new Rect(bar.x + pad, bar.center.y - key * 0.5f, key, key);
            Fill(cap, Brass);
            Text(cap, binding, keyStyle, Walnut);
            Text(new Rect(cap.xMax + gap, bar.y, labelWidth, bar.height), label, labelStyle, Ivory);
            GUI.color = previous;
        }
    }
}

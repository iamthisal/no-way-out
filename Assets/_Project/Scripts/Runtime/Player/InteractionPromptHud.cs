using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    /// <summary>
    /// The interaction prompt under the crosshair as a thin walnut bar: a brass key cap for the
    /// binding, then the label. It fades in briefly whenever the prompt changes.
    /// </summary>
    internal sealed class InteractionPromptHud
    {
        private const float FadeSeconds = 0.15f;
        private GUIStyle keyStyle, labelStyle;
        private float stylesScale = -1f;
        private string lastPrompt;
        private float shownAt;

        public void Draw(string binding, string label)
        {
            if (string.IsNullOrEmpty(label)) { lastPrompt = null; return; }
            string prompt = binding + label;
            if (Event.current.type == EventType.Repaint && prompt != lastPrompt)
            {
                lastPrompt = prompt;
                shownAt = Time.unscaledTime;
            }
            float fade = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeSeconds);
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
            Fill(bar, new Color(Walnut.r, Walnut.g, Walnut.b, 0.82f), fade);
            Frame(bar, 1f, Brass, fade);
            var cap = new Rect(bar.x + pad, bar.center.y - key * 0.5f, key, key);
            Fill(cap, Brass, fade);
            Text(cap, binding, keyStyle, Walnut, fade);
            Text(new Rect(cap.xMax + gap, bar.y, labelWidth, bar.height), label, labelStyle, Ivory, fade);
            GUI.color = previous;
        }
    }
}

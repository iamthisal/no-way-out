using Lockdown.Agents.Pursuer;
using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    /// <summary>
    /// The guard's warning as a thin walnut bar in the lower middle of the screen: a tab naming who is
    /// acting ("Guard", or "Arrest" while handcuffing), then the message. While handcuffing, a line
    /// along the bottom edge fills as the cuffs close.
    /// </summary>
    internal sealed class GuardAlertHud
    {
        private const float FadeSeconds = 0.18f;
        private GUIStyle kickerStyle, messageStyle;
        private float stylesScale = -1f;
        private GuardAlert lastAlert;
        private int lastFrame = -10;
        private float shownAt;

        public void Draw(GuardAlert alert, float progress)
        {
            string message = GuardAlerts.Message(alert);
            if (string.IsNullOrEmpty(message)) return;
            // A new alert, or one returning after a gap, fades in and rises slightly.
            if (Event.current.type == EventType.Repaint)
            {
                if (alert != lastAlert || Time.frameCount > lastFrame + 1) shownAt = Time.unscaledTime;
                lastAlert = alert;
                lastFrame = Time.frameCount;
            }
            float fade = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeSeconds);
            float scale = Scale(0.6f);
            if (StylesStale(kickerStyle, stylesScale, scale))
            {
                stylesScale = scale;
                kickerStyle = Style(14, scale);
                messageStyle = Style(18, scale, TextAnchor.MiddleLeft);
                messageStyle.wordWrap = true;
            }

            string kicker = Spaced(GuardAlerts.Kicker(alert));
            float pad = 7f * scale, gap = 14f * scale, messagePad = 18f * scale;
            float tabWidth = kickerStyle.CalcSize(new GUIContent(kicker)).x + 20f * scale;
            float maxWidth = Mathf.Min(660f * scale, Screen.width - 32f);
            float messageWidth = Mathf.Min(messageStyle.CalcSize(new GUIContent(message)).x + 2f,
                maxWidth - pad - tabWidth - gap - messagePad);
            float messageHeight = messageStyle.CalcHeight(new GUIContent(message), messageWidth);
            float height = Mathf.Max(40f * scale, messageHeight + 12f * scale);
            float width = pad + tabWidth + gap + messageWidth + messagePad;
            var bar = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.72f + (1f - fade) * 6f * scale, width, height);

            Color accent = Accent(alert);
            bool urgent = alert >= GuardAlert.ReturningFire;
            float done = Mathf.Clamp01(progress);
            // Urgent alerts pulse; the handcuff pulse quickens as the cuffs close.
            float pulse = urgent ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (6f + 8f * done)) : 0f;

            Color previous = GUI.color;
            Fill(bar, new Color(Walnut.r, Walnut.g, Walnut.b, 0.82f), fade);
            Frame(bar, 1f, Color.Lerp(accent, BrightBrass, pulse * 0.55f), fade);
            var tab = new Rect(bar.x + pad, bar.y + pad, tabWidth, bar.height - pad * 2f);
            Fill(tab, accent, fade);
            Text(tab, kicker, kickerStyle, accent == Brass ? Walnut : Ivory, fade);
            Text(new Rect(tab.xMax + gap, bar.y, messageWidth, bar.height), message, messageStyle, Ivory, fade);
            if (progress >= 0f)
            {
                float line = Mathf.Max(2f, 3f * scale);
                var track = new Rect(bar.x + 1f, bar.yMax - 1f - line, bar.width - 2f, line);
                Fill(track, DimBrass, 0.6f * fade);
                Fill(new Rect(track.x, track.y, track.width * done, line), Color.Lerp(BrightBrass, Ember, done), fade);
            }
            GUI.color = previous;
        }

        // Brass is a heads-up, oxblood means the way out is closing, ember means act now.
        private static Color Accent(GuardAlert alert) =>
            alert >= GuardAlert.ReturningFire ? Ember : alert == GuardAlert.BlockingEscape ? Oxblood : Brass;
    }
}

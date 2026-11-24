using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    /// <summary>
    /// Bottom-left health plaque. The bar shifts from verdigris to amber to red,
    /// pulsing when health is low.
    /// </summary>
    internal sealed class HealthBarHud
    {
        private const float LowHealth = 0.3f;
        private GUIStyle labelStyle, valueStyle;
        private float stylesScale = -1f;

        public void Draw(float current, float maximum)
        {
            float target = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
            float scale = Scale(0.6f);
            if (StylesStale(labelStyle, stylesScale, scale))
            {
                stylesScale = scale;
                labelStyle = Style(18, scale, TextAnchor.MiddleLeft);
                valueStyle = Style(30, scale, TextAnchor.MiddleRight);
            }

            Color previous = GUI.color;
            float margin = 24f * scale, pad = 20f * scale;
            var panel = new Rect(margin, Screen.height - margin - 92f * scale, 420f * scale, 92f * scale);
            bool low = target <= LowHealth && current > 0f;
            float pulse = low ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            Plaque(panel, scale, 0.84f, low ? Color.Lerp(Brass, Ember, pulse) : Brass);

            var header = new Rect(panel.x + pad, panel.y + 12f * scale, panel.width - pad * 2f, 34f * scale);
            Text(header, Spaced("Health"), labelStyle, low ? Oxblood : Brass);
            Text(header, Mathf.CeilToInt(Mathf.Max(0f, current)).ToString(), valueStyle, Ivory);

            var bar = new Rect(panel.x + pad, panel.y + 52f * scale, panel.width - pad * 2f, 20f * scale);
            Fill(bar, new Color(0.025f, 0.02f, 0.016f, 1f));
            Color fill = FillColour(target);
            if (low) fill = Color.Lerp(fill, BrightBrass, pulse * 0.25f);
            var filled = new Rect(bar.x, bar.y, bar.width * target, bar.height);
            Fill(filled, fill);
            Fill(new Rect(filled.x, filled.y, filled.width, Mathf.Max(1f, bar.height * 0.22f)), Color.white, 0.18f);
            for (int tick = 1; tick < 4; tick++)
                Fill(new Rect(bar.x + bar.width * tick * 0.25f - 0.5f, bar.y, Mathf.Max(1f, scale), bar.height),
                    new Color(Walnut.r, Walnut.g, Walnut.b, 0.85f));
            Frame(bar, 1f, DimBrass);
            GUI.color = previous;
        }

        private static Color FillColour(float fraction)
        {
            if (fraction >= 0.6f) return Verdigris;
            if (fraction >= LowHealth) return Color.Lerp(Brass, Verdigris, (fraction - LowHealth) / (0.6f - LowHealth));
            float t = fraction / LowHealth;
            return Color.Lerp(Ember, Brass, t * t);
        }
    }
}

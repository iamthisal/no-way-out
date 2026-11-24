using System.Globalization;
using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    /// <summary>
    /// Top-right loot plaque: the haul's value counts up to the new total and the frame
    /// flashes gold whenever something valuable is collected.
    /// </summary>
    internal sealed class LootCounterHud
    {
        private const float FlashSeconds = 0.9f;
        private GUIStyle labelStyle, countStyle, valueStyle;
        private float stylesScale = -1f;
        private double shownValue = -1d;
        private long lastValue = -1;
        private float flashUntil;

        /// <summary>The value the plaque currently shows while it counts up, exposed for checks.</summary>
        internal long ShownValue => (long)System.Math.Round(shownValue);

        /// <returns>The plaque's bottom edge, so content below it can start beneath.</returns>
        public float Draw(long totalValue, int itemCount)
        {
            if (Event.current.type == EventType.Repaint) Animate(totalValue);
            float scale = Scale(0.6f);
            if (StylesStale(labelStyle, stylesScale, scale))
            {
                stylesScale = scale;
                labelStyle = Style(18, scale, TextAnchor.MiddleLeft);
                countStyle = Style(18, scale, TextAnchor.MiddleRight);
                valueStyle = Style(44, scale, TextAnchor.MiddleLeft);
            }

            Color previous = GUI.color;
            float margin = 24f * scale, pad = 20f * scale;
            float width = Mathf.Min(360f * scale, Screen.width - margin * 2f);
            var panel = new Rect(Screen.width - margin - width, 20f * scale, width, 104f * scale);
            float flash = Mathf.Clamp01((flashUntil - Time.unscaledTime) / FlashSeconds);
            Plaque(panel, scale, 0.84f, Color.Lerp(Brass, BrightBrass, flash));
            if (flash > 0f) Fill(panel, Brass, flash * 0.14f);

            var header = new Rect(panel.x + pad, panel.y + 12f * scale, panel.width - pad * 2f, 30f * scale);
            Text(header, Spaced("Loot"), labelStyle, Brass);
            Text(header, Spaced(itemCount == 1 ? "1 item" : itemCount + " items"), countStyle,
                new Color(Ivory.r, Ivory.g, Ivory.b, 0.75f));
            var value = new Rect(panel.x + pad, panel.y + 42f * scale, panel.width - pad * 2f, 52f * scale);
            Text(value, "$" + ShownValue.ToString("N0", CultureInfo.InvariantCulture), valueStyle,
                Color.Lerp(Ivory, BrightBrass, flash));
            GUI.color = previous;
            return panel.yMax;
        }

        private void Animate(long totalValue)
        {
            if (lastValue < 0) { lastValue = totalValue; shownValue = totalValue; return; }
            if (totalValue > lastValue) flashUntil = Time.unscaledTime + FlashSeconds;
            lastValue = totalValue;
            double gap = totalValue - shownValue;
            if (System.Math.Abs(gap) < 0.5d) { shownValue = totalValue; return; }
            // Close most of the gap within about half a second, whatever its size.
            shownValue += gap * Mathf.Clamp01(Time.unscaledDeltaTime * 7f);
            if (System.Math.Abs(totalValue - shownValue) < 0.5d) shownValue = totalValue;
        }
    }
}

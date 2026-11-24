using System.Globalization;
using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    /// <summary>Top-right loot plaque: the haul's value and how many items it holds.</summary>
    internal sealed class LootCounterHud
    {
        private GUIStyle labelStyle, countStyle, valueStyle;
        private float stylesScale = -1f;

        /// <returns>The plaque's bottom edge, so content below it can start beneath.</returns>
        public float Draw(long totalValue, int itemCount)
        {
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
            Plaque(panel, scale, 0.84f, Brass);

            var header = new Rect(panel.x + pad, panel.y + 12f * scale, panel.width - pad * 2f, 30f * scale);
            Text(header, Spaced("Loot"), labelStyle, Brass);
            Text(header, Spaced(itemCount == 1 ? "1 item" : itemCount + " items"), countStyle,
                new Color(Ivory.r, Ivory.g, Ivory.b, 0.75f));
            var value = new Rect(panel.x + pad, panel.y + 42f * scale, panel.width - pad * 2f, 52f * scale);
            Text(value, "$" + totalValue.ToString("N0", CultureInfo.InvariantCulture), valueStyle, Ivory);
            GUI.color = previous;
            return panel.yMax;
        }
    }
}

using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    internal enum WeaponHudSlot { Hands, Pistol, Hammer }

    /// <summary>
    /// Bottom-right weapon plaque: the held weapon's name and icon, with ∞ beneath for the
    /// fists and hammer, or the pistol's rounds as a big count over a small magazine size.
    /// </summary>
    internal sealed class WeaponHud
    {
        private const float SwapFlashSeconds = 0.5f;
        // Cinzel's descender scales with size, so a smaller label sharing a bottom edge sits low.
        private const float DescentRatio = 0.24f;
        private GUIStyle nameStyle, countStyle, magazineStyle;
        private float stylesScale = -1f;
        private WeaponHudSlot? lastSlot;
        private float flashUntil;

        public void Draw(WeaponHudSlot slot, int ammo, int magazine, bool reloading)
        {
            if (Event.current.type == EventType.Repaint && lastSlot != slot)
            {
                if (lastSlot.HasValue) flashUntil = Time.unscaledTime + SwapFlashSeconds;
                lastSlot = slot;
            }
            float scale = Scale(0.6f);
            if (StylesStale(nameStyle, stylesScale, scale))
            {
                stylesScale = scale;
                nameStyle = Style(17, scale);
                countStyle = Style(46, scale, TextAnchor.LowerLeft);
                magazineStyle = Style(22, scale, TextAnchor.LowerLeft);
            }

            Color previous = GUI.color;
            float margin = 24f * scale, width = 210f * scale, height = 192f * scale;
            var panel = new Rect(Screen.width - margin - width, Screen.height - margin - height, width, height);
            float flash = Mathf.Clamp01((flashUntil - Time.unscaledTime) / SwapFlashSeconds);
            Plaque(panel, scale, 0.84f, Color.Lerp(Brass, BrightBrass, flash));

            Text(new Rect(panel.x, panel.y + 14f * scale, panel.width, 24f * scale), Spaced(Name(slot)), nameStyle, Brass);
            float iconSize = 72f * scale;
            var icon = new Rect(panel.center.x - iconSize * 0.5f, panel.y + 40f * scale, iconSize, iconSize);
            GUI.color = Shown(Color.Lerp(Brass, BrightBrass, flash));
            GUI.DrawTexture(icon, HudIcons.Get(Icon(slot)), ScaleMode.ScaleToFit);
            float rule = 56f * scale;
            Fill(new Rect(panel.center.x - rule * 0.5f, panel.y + 119f * scale, rule, 1f), DimBrass);

            var row = new Rect(panel.x, panel.y + 128f * scale, panel.width, 52f * scale);
            if (slot != WeaponHudSlot.Pistol)
            {
                float size = 64f * scale;
                GUI.color = Shown(Ivory);
                GUI.DrawTexture(new Rect(panel.center.x - size * 0.5f, row.center.y - size * 0.5f, size, size),
                    HudIcons.Get(HudIcon.Infinity), ScaleMode.ScaleToFit);
            }
            else if (reloading)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 7f);
                Text(row, Spaced("Reloading"), nameStyle, Brass, pulse);
            }
            else
            {
                string count = ammo.ToString(), size = "/ " + magazine;
                Vector2 big = countStyle.CalcSize(new GUIContent(count)), small = magazineStyle.CalcSize(new GUIContent(size));
                float gap = 6f * scale, left = panel.center.x - (big.x + gap + small.x) * 0.5f;
                Color colour = ammo <= 0 ? Oxblood : ammo * 4 <= magazine ? Brass : Ivory;
                Text(new Rect(left, row.y, big.x, row.height), count, countStyle, colour);
                float lift = (countStyle.fontSize - magazineStyle.fontSize) * DescentRatio;
                Text(new Rect(left + big.x + gap, row.y - lift, small.x, row.height), size, magazineStyle, Brass);
            }
            GUI.color = previous;
        }

        private static string Name(WeaponHudSlot slot) =>
            slot == WeaponHudSlot.Pistol ? "Pistol" : slot == WeaponHudSlot.Hammer ? "Hammer" : "Fists";

        private static HudIcon Icon(WeaponHudSlot slot) =>
            slot == WeaponHudSlot.Pistol ? HudIcon.Pistol : slot == WeaponHudSlot.Hammer ? HudIcon.Sledgehammer : HudIcon.Fist;
    }
}

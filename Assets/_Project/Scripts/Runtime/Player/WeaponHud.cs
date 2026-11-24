using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    internal enum WeaponHudSlot { Hands, Pistol, Hammer }

    /// <summary>
    /// Bottom-right weapon plaque: the held weapon's name and icon, with ∞ beneath for the
    /// fists and hammer.
    /// </summary>
    internal sealed class WeaponHud
    {
        private GUIStyle nameStyle;
        private float stylesScale = -1f;

        public void Draw(WeaponHudSlot slot, int ammo, int magazine, bool reloading)
        {
            float scale = Scale(0.6f);
            if (StylesStale(nameStyle, stylesScale, scale))
            {
                stylesScale = scale;
                nameStyle = Style(17, scale);
            }

            Color previous = GUI.color;
            float margin = 24f * scale, width = 210f * scale, height = 192f * scale;
            var panel = new Rect(Screen.width - margin - width, Screen.height - margin - height, width, height);
            Plaque(panel, scale, 0.84f, Brass);

            Text(new Rect(panel.x, panel.y + 14f * scale, panel.width, 24f * scale), Spaced(Name(slot)), nameStyle, Brass);
            float iconSize = 72f * scale;
            var icon = new Rect(panel.center.x - iconSize * 0.5f, panel.y + 40f * scale, iconSize, iconSize);
            GUI.color = Shown(Brass);
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
            GUI.color = previous;
        }

        private static string Name(WeaponHudSlot slot) =>
            slot == WeaponHudSlot.Pistol ? "Pistol" : slot == WeaponHudSlot.Hammer ? "Hammer" : "Fists";

        private static HudIcon Icon(WeaponHudSlot slot) =>
            slot == WeaponHudSlot.Pistol ? HudIcon.Pistol : slot == WeaponHudSlot.Hammer ? HudIcon.Sledgehammer : HudIcon.Fist;
    }
}

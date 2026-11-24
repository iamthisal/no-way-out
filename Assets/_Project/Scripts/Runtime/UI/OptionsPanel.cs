using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.UI
{
    /// <summary>The options plaque shared by the main and pause menus: master volume and Back.</summary>
    internal sealed class OptionsPanel
    {
        private GUIStyle titleStyle, labelStyle, valueStyle;
        private float stylesScale = -1f;

        /// <returns>True when the player presses Back; the volume is saved then.</returns>
        public bool Draw(float scale, float alpha = 1f)
        {
            if (StylesStale(titleStyle, stylesScale, scale))
            {
                stylesScale = scale;
                titleStyle = Style(44, scale);
                labelStyle = Style(22, scale, TextAnchor.MiddleLeft);
                valueStyle = Style(22, scale, TextAnchor.MiddleRight);
            }

            float width = Mathf.Min(760f * scale, Screen.width - 32f), height = 400f * scale;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            MenuUi.Panel(panel, scale, alpha);
            Text(new Rect(panel.x, panel.y + 40f * scale, panel.width, 60f * scale), Spaced("Options"), titleStyle, Ivory, alpha);
            Ornament(new Vector2(panel.center.x, panel.y + 116f * scale), scale, alpha);

            float inset = 60f * scale;
            var row = new Rect(panel.x + inset, panel.y + 152f * scale, panel.width - inset * 2f, 34f * scale);
            Text(row, Spaced("Master volume"), labelStyle, Brass, alpha);
            Text(row, Mathf.RoundToInt(GameSettings.Volume * 100f) + "%", valueStyle, Ivory, alpha);
            var track = new Rect(row.x, panel.y + 206f * scale, row.width, 10f * scale);
            float volume = MenuUi.Slider(track, GameSettings.Volume, scale, alpha);
            if (!Mathf.Approximately(volume, GameSettings.Volume)) GameSettings.Volume = volume;

            var back = new Rect(panel.center.x - 160f * scale, panel.y + 276f * scale, 320f * scale, 80f * scale);
            if (!MenuUi.Button(back, "Back", scale, alpha)) return false;
            GameSettings.Save();
            return true;
        }
    }
}

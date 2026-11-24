using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    internal enum EndScreenKind { Caught, Died, Escaped }

    /// <summary>Draws the caught, died and mission-passed screens as a brass museum plaque.</summary>
    internal static class EndScreen
    {
        private static GUIStyle titleStyle, subtitleStyle, buttonStyle;
        private static float stylesScale = -1f;

        /// <returns>True on the frame the player presses the button.</returns>
        public static bool Draw(EndScreenKind kind)
        {
            float scale = Scale();
            EnsureStyles(scale);
            bool escaped = kind == EndScreenKind.Escaped;
            Color previous = GUI.color;

            // Backdrop: a tinted wash over the museum.
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), escaped
                ? new Color(0.03f, 0.1f, 0.075f, 0.94f)
                : new Color(0.24f, 0.03f, 0.04f, 0.85f));

            float width = Mathf.Min(1100f * scale, Screen.width - 32f), height = 560f * scale;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Fill(panel, new Color(Walnut.r, Walnut.g, Walnut.b, 0.97f));
            Frame(panel, Mathf.Max(2f, 3f * scale), Brass);
            Frame(Inset(panel, 14f * scale), 1f, DimBrass);

            Text(Row(panel, 146f, 130f, scale), Title(kind), titleStyle, Ivory);
            Text(Row(panel, 284f, 56f, scale), Subtitle(kind), subtitleStyle, new Color(Ivory.r, Ivory.g, Ivory.b, 0.78f));

            var button = new Rect(panel.center.x - 210f * scale, panel.y + 384f * scale, 420f * scale, 96f * scale);
            Fill(button, new Color(0.16f, 0.125f, 0.09f, 1f));
            Frame(button, Mathf.Max(2f, 2f * scale), Brass);
            Text(button, Spaced(escaped ? "Play again" : "Retry"), buttonStyle, Ivory);
            bool pressed = GUI.Button(button, GUIContent.none, GUIStyle.none);

            GUI.color = previous;
            return pressed;
        }

        private static string Title(EndScreenKind kind) =>
            kind == EndScreenKind.Caught ? "You got caught" : kind == EndScreenKind.Died ? "You died" : "You escaped";

        private static string Subtitle(EndScreenKind kind) =>
            kind == EndScreenKind.Caught ? "The guard put you in handcuffs."
            : kind == EndScreenKind.Died ? "The museum keeps its treasures tonight."
            : "You slipped out of the museum with the collection.";

        private static void EnsureStyles(float scale)
        {
            if (!StylesStale(titleStyle, stylesScale, scale)) return;
            stylesScale = scale;
            titleStyle = Style(96, scale);
            subtitleStyle = Style(32, scale);
            buttonStyle = Style(36, scale);
        }

        private static Rect Row(Rect panel, float top, float height, float scale) =>
            new Rect(panel.x, panel.y + top * scale, panel.width, height * scale);
    }
}

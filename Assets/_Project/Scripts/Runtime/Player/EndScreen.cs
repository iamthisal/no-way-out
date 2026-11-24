using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.Player
{
    internal enum EndScreenKind { Caught, Died, Escaped }

    /// <summary>Draws the caught, died and mission-passed screens as a brass museum plaque.</summary>
    internal static class EndScreen
    {
        private const float FadeSeconds = 0.5f;
        private static Texture2D vignette;
        private static GUIStyle kickerStyle, titleStyle, subtitleStyle, buttonStyle;
        private static float stylesScale = -1f;

        /// <returns>True on the frame the player presses the button.</returns>
        public static bool Draw(EndScreenKind kind, float shownAt)
        {
            float fade = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeSeconds);
            float scale = Scale();
            EnsureStyles(scale);
            bool escaped = kind == EndScreenKind.Escaped;
            Color previous = GUI.color;

            // Backdrop: a tinted wash over the museum, darker at the edges.
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), escaped
                ? new Color(0.03f, 0.1f, 0.075f, 0.94f)
                : new Color(0.24f, 0.03f, 0.04f, 0.85f), fade);
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Vignette());

            // The plaque, rising slightly as it fades in.
            float width = Mathf.Min(1100f * scale, Screen.width - 32f), height = 560f * scale;
            var panel = new Rect((Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f + (1f - fade) * 24f * scale, width, height);
            Fill(panel, new Color(Walnut.r, Walnut.g, Walnut.b, 0.97f), fade);
            Frame(panel, Mathf.Max(2f, 3f * scale), Brass, fade);
            Frame(Inset(panel, 14f * scale), 1f, DimBrass, fade);

            Color accent = escaped ? Brass : Oxblood;
            Text(Row(panel, 64f, 40f, scale), Spaced(Kicker(kind)), kickerStyle, accent, fade);
            Ornament(new Vector2(panel.center.x, panel.y + 132f * scale), scale, fade);
            Text(Row(panel, 146f, 130f, scale), Title(kind), titleStyle, Ivory, fade);
            Text(Row(panel, 284f, 56f, scale), Subtitle(kind), subtitleStyle, new Color(Ivory.r, Ivory.g, Ivory.b, 0.78f), fade);

            var button = new Rect(panel.center.x - 210f * scale, panel.y + 384f * scale, 420f * scale, 96f * scale);
            bool hover = button.Contains(Event.current.mousePosition);
            Fill(button, hover ? Brass : new Color(0.16f, 0.125f, 0.09f, 1f), fade);
            Frame(button, Mathf.Max(2f, 2f * scale), Brass, fade);
            Text(button, Spaced(escaped ? "Play again" : "Retry"), buttonStyle, hover ? Walnut : Ivory, fade);
            bool pressed = GUI.Button(button, GUIContent.none, GUIStyle.none) && fade >= 1f;

            GUI.color = previous;
            return pressed;
        }

        private static string Kicker(EndScreenKind kind) =>
            kind == EndScreenKind.Escaped ? "Mission passed" : "Security report";

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
            kickerStyle = Style(26, scale);
            titleStyle = Style(96, scale);
            subtitleStyle = Style(32, scale);
            buttonStyle = Style(36, scale);
        }

        private static Rect Row(Rect panel, float top, float height, float scale) =>
            new Rect(panel.x, panel.y + top * scale, panel.width, height * scale);

        // A short brass rule with a diamond at its centre.
        private static void Ornament(Vector2 centre, float scale, float fade)
        {
            float half = 150f * scale, gap = 16f * scale, line = Mathf.Max(1f, 2f * scale);
            Fill(new Rect(centre.x - half, centre.y - line * 0.5f, half - gap, line), DimBrass, fade);
            Fill(new Rect(centre.x + gap, centre.y - line * 0.5f, half - gap, line), DimBrass, fade);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, centre);
            float size = 10f * scale;
            Fill(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), Brass, fade);
            GUI.matrix = matrix;
        }

        private static Texture2D Vignette()
        {
            if (vignette != null) return vignette;
            const int size = 128;
            vignette = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.45f, Mathf.Sqrt(dx * dx + dy * dy)));
                pixels[y * size + x] = new Color32(0, 0, 0, (byte)(edge * 200f));
            }
            vignette.SetPixels32(pixels);
            vignette.Apply(false, true);
            return vignette;
        }
    }
}

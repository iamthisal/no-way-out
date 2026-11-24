using UnityEngine;

namespace Lockdown.Player
{
    /// <summary>
    /// The museum look shared by the end screens, the loot plaque and the health bar:
    /// walnut panels, brass frames, ivory Cinzel lettering.
    /// </summary>
    internal static class MuseumHud
    {
        public static readonly Color Brass = new Color(0.79f, 0.64f, 0.36f);
        public static readonly Color BrightBrass = new Color(1f, 0.86f, 0.52f);
        public static readonly Color DimBrass = new Color(0.49f, 0.39f, 0.22f);
        public static readonly Color Ivory = new Color(0.95f, 0.91f, 0.82f);
        public static readonly Color Walnut = new Color(0.075f, 0.058f, 0.045f);
        public static readonly Color Oxblood = new Color(0.86f, 0.36f, 0.32f);
        public static readonly Color Verdigris = new Color(0.42f, 0.68f, 0.56f);
        public static readonly Color Ember = new Color(0.8f, 0.2f, 0.17f);

        private static Font font;
        private static Texture2D vignette;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.Load<Font>("Fonts/Cinzel");
                return font;
            }
        }

        /// <summary>1 at 1920x1080; shrinks with the window but never below <paramref name="minimum"/>.</summary>
        public static float Scale(float minimum = 0f) =>
            Mathf.Max(minimum, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));

        public static GUIStyle Style(int size, float scale, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(8, Mathf.RoundToInt(size * scale)),
                alignment = alignment,
                wordWrap = false,
                clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0),
            };
            if (Font != null) style.font = Font;
            return style;
        }

        /// <summary>True when cached styles were built for another scale or before the font loaded.</summary>
        public static bool StylesStale(GUIStyle sample, float builtFor, float scale) =>
            sample == null || !Mathf.Approximately(builtFor, scale) || sample.font != Font;

        // The palette is authored in sRGB. In a Linear project IMGUI would brighten it, so convert first.
        public static Color Shown(Color color, float alpha = 1f)
        {
            Color shown = QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color;
            shown.a = color.a * alpha;
            return shown;
        }

        public static void Fill(Rect rect, Color color, float alpha = 1f)
        {
            GUI.color = Shown(color, alpha);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        public static void Frame(Rect rect, float thickness, Color color, float alpha = 1f)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color, alpha);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color, alpha);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color, alpha);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color, alpha);
        }

        public static void Text(Rect rect, string text, GUIStyle style, Color color, float alpha = 1f)
        {
            style.normal.textColor = Shown(color, alpha);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        /// <summary>A walnut panel with a brass frame and an inner hairline, like an exhibit label.</summary>
        public static void Plaque(Rect rect, float scale, float opacity, Color frame, float alpha = 1f)
        {
            Fill(rect, new Color(Walnut.r, Walnut.g, Walnut.b, opacity), alpha);
            Frame(rect, Mathf.Max(1f, 2f * scale), frame, alpha);
            Frame(Inset(rect, 7f * scale), 1f, DimBrass, alpha * 0.8f);
        }

        /// <summary>A short brass rule with a diamond at its centre.</summary>
        public static void Ornament(Vector2 centre, float scale, float alpha = 1f)
        {
            float half = 150f * scale, gap = 16f * scale, line = Mathf.Max(1f, 2f * scale);
            Fill(new Rect(centre.x - half, centre.y - line * 0.5f, half - gap, line), DimBrass, alpha);
            Fill(new Rect(centre.x + gap, centre.y - line * 0.5f, half - gap, line), DimBrass, alpha);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, centre);
            float size = 10f * scale;
            Fill(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), Brass, alpha);
            GUI.matrix = matrix;
        }

        /// <summary>A black vignette that darkens the screen edges, drawn full-screen.</summary>
        public static Texture2D Vignette()
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

        public static Rect Inset(Rect rect, float by) =>
            new Rect(rect.x + by, rect.y + by, rect.width - by * 2f, rect.height - by * 2f);

        // IMGUI has no letter spacing, so space out the capitals like an engraved plaque.
        public static string Spaced(string text) =>
            string.Join(" ", text.ToUpperInvariant().ToCharArray()).Replace("   ", "     ");
    }
}

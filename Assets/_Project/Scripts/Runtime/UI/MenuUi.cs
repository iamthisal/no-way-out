using UnityEngine;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.UI
{
    /// <summary>Museum-styled IMGUI widgets shared by the main menu, pause menu and options.</summary>
    internal static class MenuUi
    {
        private static readonly Color ButtonFill = new Color(0.16f, 0.125f, 0.09f, 1f);
        private static GUIStyle buttonStyle;
        private static float buttonScale = -1f;

        /// <summary>A tinted full-screen wash with darkened edges.</summary>
        public static void Backdrop(Color tint, float alpha = 1f)
        {
            var screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(screen, tint, alpha);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(screen, Vignette());
        }

        /// <summary>A walnut panel with a heavy brass frame and an inner hairline.</summary>
        public static void Panel(Rect rect, float scale, float alpha = 1f)
        {
            Fill(rect, new Color(Walnut.r, Walnut.g, Walnut.b, 0.97f), alpha);
            Frame(rect, Mathf.Max(2f, 3f * scale), Brass, alpha);
            Frame(Inset(rect, 14f * scale), 1f, DimBrass, alpha);
        }

        /// <returns>True on the frame the button is clicked. Ignores clicks until fully faded in.</returns>
        public static bool Button(Rect rect, string label, float scale, float alpha = 1f)
        {
            if (StylesStale(buttonStyle, buttonScale, scale))
            {
                buttonScale = scale;
                buttonStyle = Style(30, scale);
            }
            bool ready = alpha >= 1f;
            bool hover = ready && rect.Contains(Event.current.mousePosition);
            Fill(rect, hover ? Brass : ButtonFill, alpha);
            Frame(rect, Mathf.Max(2f, 2f * scale), Brass, alpha);
            Text(rect, Spaced(label), buttonStyle, hover ? Walnut : Ivory, alpha);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none) && ready;
        }

        /// <summary>A brass slider from 0 to 1. Click anywhere on it or drag the handle.</summary>
        public static float Slider(Rect track, float value, float scale, float alpha = 1f)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive);
            float reach = 16f * scale;
            var hitArea = new Rect(track.x - reach, track.y - reach, track.width + reach * 2f, track.height + reach * 2f);
            Event e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (alpha >= 1f && e.button == 0 && hitArea.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        value = ValueAt(track, e.mousePosition.x);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        value = ValueAt(track, e.mousePosition.x);
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }

            bool active = GUIUtility.hotControl == id || (alpha >= 1f && hitArea.Contains(e.mousePosition));
            Fill(track, new Color(0.025f, 0.02f, 0.016f, 1f), alpha);
            Fill(new Rect(track.x, track.y, track.width * value, track.height), Brass, alpha);
            Frame(track, 1f, DimBrass, alpha);
            float knobWidth = 18f * scale, knobHeight = track.height + 22f * scale;
            var knob = new Rect(track.x + track.width * value - knobWidth * 0.5f, track.center.y - knobHeight * 0.5f,
                knobWidth, knobHeight);
            Fill(knob, active ? BrightBrass : Brass, alpha);
            Frame(knob, 1f, Walnut, alpha);
            Fill(new Rect(knob.center.x - 0.5f, knob.y + 5f * scale, 1f, knob.height - 10f * scale), Walnut, alpha * 0.8f);
            return value;
        }

        private static float ValueAt(Rect track, float x) => Mathf.Clamp01((x - track.x) / track.width);
    }
}

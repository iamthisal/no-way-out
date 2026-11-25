using Lockdown.Intro;
using UnityEngine;
using UnityEngine.InputSystem;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.UI
{
    /// <summary>The title screen: NO WAY OUT, then Start Game, Options and Exit.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        public const string GameTitle = "No Way Out";
        private const float FadeSeconds = 0.6f;
        private static readonly Color Background = new Color(0.06f, 0.045f, 0.035f, 1f);
        private readonly OptionsPanel options = new OptionsPanel();
        private GUIStyle titleStyle;
        private float stylesScale = -1f;
        private float shownAt;

        public bool IsShowingOptions { get; private set; }

        private void OnEnable()
        {
            // Arriving from a paused or finished game must not leave it frozen, muted or cursor-locked.
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            bool back = (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
                        (Gamepad.current?.buttonEast.wasPressedThisFrame ?? false);
            if (back && IsShowingOptions) CloseOptions();
        }

        /// <summary>Starts a new run with the break-in cutscene.</summary>
        public void StartGame()
        {
            IntroCutscene.Requested = true;
            SceneFlow.Load(SceneFlow.MuseumScene);
        }

        public void OpenOptions() => IsShowingOptions = true;

        public void CloseOptions()
        {
            GameSettings.Save();
            IsShowingOptions = false;
        }

        private void OnGUI()
        {
            float fade = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeSeconds);
            float scale = Scale();
            if (StylesStale(titleStyle, stylesScale, scale))
            {
                stylesScale = scale;
                titleStyle = Style(128, scale);
            }

            Color previous = GUI.color;
            MenuUi.Backdrop(Background);
            // A thin double brass frame around the screen, like a gallery mount.
            var mount = Inset(new Rect(0f, 0f, Screen.width, Screen.height), 28f * scale);
            Frame(mount, Mathf.Max(1f, 2f * scale), DimBrass);
            Frame(Inset(mount, 8f * scale), 1f, DimBrass, 0.6f);

            if (IsShowingOptions)
            {
                if (options.Draw(scale)) CloseOptions();
                GUI.color = previous;
                return;
            }

            float titleY = Screen.height * 0.2f;
            Text(new Rect(0f, titleY, Screen.width, 150f * scale), GameTitle.ToUpperInvariant(), titleStyle, Ivory, fade);
            Ornament(new Vector2(Screen.width * 0.5f, titleY + 168f * scale), scale, fade);

            float width = 440f * scale, height = 84f * scale, gap = 22f * scale;
            var button = new Rect((Screen.width - width) * 0.5f, titleY + 236f * scale, width, height);
            if (MenuUi.Button(button, "Start game", scale, fade)) StartGame();
            button.y += height + gap;
            if (MenuUi.Button(button, "Options", scale, fade)) OpenOptions();
            button.y += height + gap;
            if (SceneFlow.CanQuit && MenuUi.Button(button, "Exit", scale, fade)) SceneFlow.Quit();
            GUI.color = previous;
        }
    }
}

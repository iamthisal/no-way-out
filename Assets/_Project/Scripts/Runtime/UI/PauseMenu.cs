using Lockdown.Combat;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using static Lockdown.Player.MuseumHud;

namespace Lockdown.UI
{
    /// <summary>
    /// In-game pause menu. Escape (or Start) freezes time and audio and offers Continue, Restart,
    /// Options and Main Menu. It never opens over the caught, died or escaped screens.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(PlayerVitals))]
    public sealed class PauseMenu : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;
        private static readonly Color Dim = new Color(0.02f, 0.015f, 0.012f, 0.72f);
        private readonly OptionsPanel options = new OptionsPanel();
        private FirstPersonController controller;
        private PlayerVitals vitals;
        private Health health;
        private GUIStyle titleStyle;
        private float stylesScale = -1f;
        private float timeScaleBeforePause = 1f;
        private float openedAt;

        public bool IsPaused { get; private set; }
        public bool IsShowingOptions { get; private set; }

        private bool HasEnded => vitals.HasEscaped || vitals.IsCaptured || (health != null && health.IsDead);

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            vitals = GetComponent<PlayerVitals>();
            health = GetComponent<Health>();
        }

        private void Update()
        {
            if (HasEnded)
            {
                Unpause();
                return;
            }
            bool pausePressed = (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false) ||
                                (Gamepad.current?.startButton.wasPressedThisFrame ?? false);
            if (!IsPaused)
            {
                if (pausePressed) Pause();
                return;
            }
            // Something else re-captured the cursor (gameplay code or a test): carry on playing.
            if (controller.IsCursorCaptured)
            {
                Unpause();
                return;
            }
            if (!pausePressed && !(Gamepad.current?.buttonEast.wasPressedThisFrame ?? false)) return;
            if (IsShowingOptions) CloseOptions();
            else Continue();
        }

        public void Pause()
        {
            if (IsPaused || HasEnded) return;
            IsPaused = true;
            IsShowingOptions = false;
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            controller.InputBlocked = true;
            controller.ReleaseCursor();
            openedAt = Time.unscaledTime;
        }

        public void Continue()
        {
            if (!IsPaused) return;
            Unpause();
            controller.CaptureCursor();
        }

        public void Restart()
        {
            Unpause();
            SceneFlow.Load(gameObject.scene.path);
        }

        public void ReturnToMainMenu()
        {
            Unpause();
            SceneFlow.Load(SceneFlow.MainMenuScene);
        }

        public void OpenOptions() => IsShowingOptions = true;

        public void CloseOptions()
        {
            GameSettings.Save();
            IsShowingOptions = false;
        }

        private void Unpause()
        {
            if (!IsPaused) return;
            IsPaused = false;
            IsShowingOptions = false;
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;
            if (controller != null) controller.InputBlocked = false;
        }

        // Leaving the scene while paused must not leave the next one frozen or muted.
        private void OnDisable() => Unpause();

        private void OnGUI()
        {
            if (!IsPaused) return;
            GUI.depth = -150;
            float fade = Mathf.Clamp01((Time.unscaledTime - openedAt) / FadeSeconds);
            float scale = Scale();
            if (StylesStale(titleStyle, stylesScale, scale))
            {
                stylesScale = scale;
                titleStyle = Style(52, scale);
            }

            Color previous = GUI.color;
            MenuUi.Backdrop(Dim, fade);
            if (IsShowingOptions)
            {
                if (options.Draw(scale)) CloseOptions();
                GUI.color = previous;
                return;
            }

            float width = Mathf.Min(560f * scale, Screen.width - 32f), height = 584f * scale;
            var panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            MenuUi.Panel(panel, scale, fade);
            Text(new Rect(panel.x, panel.y + 44f * scale, panel.width, 76f * scale), Spaced("Paused"), titleStyle, Ivory, fade);
            Ornament(new Vector2(panel.center.x, panel.y + 140f * scale), scale, fade);

            float buttonWidth = 420f * scale, buttonHeight = 76f * scale, gap = 16f * scale;
            var button = new Rect(panel.center.x - buttonWidth * 0.5f, panel.y + 176f * scale, buttonWidth, buttonHeight);
            if (MenuUi.Button(button, "Continue", scale, fade)) Continue();
            button.y += buttonHeight + gap;
            if (MenuUi.Button(button, "Restart", scale, fade)) Restart();
            button.y += buttonHeight + gap;
            if (MenuUi.Button(button, "Options", scale, fade)) OpenOptions();
            button.y += buttonHeight + gap;
            if (MenuUi.Button(button, "Main menu", scale, fade)) ReturnToMainMenu();
            GUI.color = previous;
        }
    }
}

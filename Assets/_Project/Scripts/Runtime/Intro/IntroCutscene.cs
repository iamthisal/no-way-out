using System.Collections.Generic;
using Lockdown.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;

namespace Lockdown.Intro
{
    /// <summary>
    /// Plays the break-in before the museum starts. Only the main menu's Start game asks for it, so Restart
    /// and pressing Play in the museum go straight to the garden. Until it ends, the player, the enemies and
    /// the HUD stay inactive: nothing can see, hurt or spawn around a player who is not there yet.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class IntroCutscene : MonoBehaviour
    {
        private const float SkipFadeSeconds = 0.35f, RevealSeconds = 0.9f, PromptDelay = 1.2f;

        [SerializeField] private GameObject stage;
        [SerializeField] private PlayableDirector director;
        [Tooltip("Scene roots that stay active during the intro. Every other root waits for it to end.")]
        [SerializeField] private GameObject[] scenery = new GameObject[0];
        [SerializeField, Min(0f)] private float fadeInSeconds = 1f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.6f;

        private readonly List<GameObject> heldBack = new List<GameObject>();
        private GUIStyle promptStyle;
        private float promptScale = -1f;
        private float skippedAt = -1f, endedAt = -1f;

        /// <summary>Set by the main menu. The next museum that loads plays the intro once.</summary>
        public static bool Requested { get; set; }

        public bool IsPlaying { get; private set; }
        public float Duration => director != null ? (float)director.duration : 0f;
        public float Elapsed => director != null ? (float)director.time : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRequest() { Requested = false; }

        // Runs before every other Awake in the scene, so gameplay objects have not started yet.
        private void Awake()
        {
            bool play = Requested && stage != null && director != null;
            Requested = false;
            if (stage != null) stage.SetActive(play);
            if (!play) { enabled = false; return; }
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                if (root == gameObject || !root.activeSelf || System.Array.IndexOf(scenery, root) >= 0) continue;
                root.SetActive(false);
                heldBack.Add(root);
            }
            IsPlaying = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            director.time = 0d;
            director.Play();
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                if (Time.unscaledTime - endedAt > RevealSeconds) enabled = false;
                return;
            }
            if (skippedAt < 0f && SkipPressed()) Skip();
            bool skipDone = skippedAt >= 0f && Time.unscaledTime - skippedAt >= SkipFadeSeconds;
            if (skipDone || director.state != PlayState.Playing || director.time >= director.duration - 0.02d) Finish();
        }

        /// <summary>Fades out quickly and starts the game.</summary>
        public void Skip()
        {
            if (IsPlaying && skippedAt < 0f) skippedAt = Time.unscaledTime;
        }

        private static bool SkipPressed()
        {
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            return (keys != null && (keys.spaceKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame || keys.escapeKey.wasPressedThisFrame))
                || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame));
        }

        private void Finish()
        {
            IsPlaying = false;
            endedAt = Time.unscaledTime;
            director.Stop();
            stage.SetActive(false);
            foreach (var root in heldBack) if (root != null) root.SetActive(true);
            heldBack.Clear();
        }

        private float Blackness()
        {
            if (!IsPlaying) return 1f - Mathf.Clamp01((Time.unscaledTime - endedAt) / RevealSeconds);
            float black = fadeInSeconds > 0f ? 1f - Elapsed / fadeInSeconds : 0f;
            if (fadeOutSeconds > 0f) black = Mathf.Max(black, 1f - (Duration - Elapsed) / fadeOutSeconds);
            if (skippedAt >= 0f) black = Mathf.Max(black, (Time.unscaledTime - skippedAt) / SkipFadeSeconds);
            return Mathf.Clamp01(black);
        }

        private void OnGUI()
        {
            GUI.depth = -300;
            Color previous = GUI.color;
            if (IsPlaying)
            {
                // Wide-screen bars, like a film.
                float bar = Screen.height * 0.095f;
                MuseumHud.Fill(new Rect(0f, 0f, Screen.width, bar), Color.black);
                MuseumHud.Fill(new Rect(0f, Screen.height - bar, Screen.width, bar), Color.black);
                DrawPrompt(bar);
            }
            MuseumHud.Fill(new Rect(0f, 0f, Screen.width, Screen.height), Color.black, Blackness());
            GUI.color = previous;
        }

        private void DrawPrompt(float bar)
        {
            float scale = MuseumHud.Scale();
            if (MuseumHud.StylesStale(promptStyle, promptScale, scale))
            {
                promptScale = scale;
                promptStyle = MuseumHud.Style(22, scale, TextAnchor.MiddleRight);
            }
            float alpha = 0.7f * Mathf.Clamp01(Elapsed - PromptDelay);
            MuseumHud.Text(new Rect(0f, Screen.height - bar, Screen.width - 48f * scale, bar), "Space  -  Skip", promptStyle, MuseumHud.Ivory, alpha);
        }
    }
}

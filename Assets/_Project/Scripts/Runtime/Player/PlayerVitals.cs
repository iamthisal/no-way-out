using Lockdown.Combat;
using Lockdown.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(FirstPersonController))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerVitals : MonoBehaviour
    {
        [SerializeField] private AudioClip hitSound;
        [SerializeField, Range(0f, 1f)] private float hitVolume = 0.7f;
        [SerializeField] private AudioClip deathSound;
        [SerializeField, Range(0f, 1f)] private float deathVolume = 0.8f;
        private Health health;
        private FirstPersonController controller;
        private AudioSource hitAudio;
        private float hitEndsAt;
        private GUIStyle labelStyle;
        private float endScreenShownAt;
        private float timeScaleBeforeEscape;
        public bool IsCaptured { get; private set; }
        public bool HasEscaped { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<FirstPersonController>();
            hitAudio = GetComponent<AudioSource>();
            if (hitAudio == null) hitAudio = gameObject.AddComponent<AudioSource>();
            hitAudio.playOnAwake = false;
            hitAudio.loop = false;
            hitAudio.spatialBlend = 0f;
        }

        private void OnEnable()
        {
            health.OnDamaged.AddListener(ShowHit);
            health.OnDeath.AddListener(Die);
        }

        private void OnDisable()
        {
            health.OnDamaged.RemoveListener(ShowHit);
            health.OnDeath.RemoveListener(Die);
            RestoreTimeAfterEscape();
        }

        private void ShowHit()
        {
            hitEndsAt = Time.time + 0.2f;
            if (!health.IsDead && hitSound != null) hitAudio.PlayOneShot(hitSound, hitVolume);
        }

        private void Die()
        {
            if (HasEscaped) return;
            hitAudio.Stop();
            if (deathSound != null)
            {
                hitAudio.clip = deathSound;
                hitAudio.volume = deathVolume;
                hitAudio.Play();
            }
            DisableControls();
        }

        public bool Capture()
        {
            if (health.IsDead || IsCaptured || HasEscaped) return false;
            IsCaptured = true;
            DisableControls();
            return true;
        }

        internal bool Escape()
        {
            if (health.IsDead || IsCaptured || HasEscaped) return false;
            HasEscaped = true;
            DisableControls();
            timeScaleBeforeEscape = Time.timeScale;
            Time.timeScale = 0f;
            return true;
        }

        private void RestoreTimeAfterEscape()
        {
            if (HasEscaped && Time.timeScale == 0f) Time.timeScale = timeScaleBeforeEscape;
        }

        private void DisableControls()
        {
            endScreenShownAt = Time.unscaledTime;
            controller.ReleaseCursor();
            controller.enabled = false;
            foreach (var weapon in GetComponentsInChildren<FirstPersonWeapon>(true)) weapon.enabled = false;
            var equipment = GetComponent<PlayerEquipment>();
            if (equipment != null) equipment.enabled = false;
        }

        public void RestartEncounter()
        {
            if (!health.IsDead && !IsCaptured && !HasEscaped) return;
            RestoreTimeAfterEscape();
            string path = gameObject.scene.path;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path, LoadSceneMode.Single);
#endif
        }

        private void OnGUI()
        {
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
            bool ended = HasEscaped || IsCaptured || health.IsDead;
            // Draw the end screen above the rest of the HUD so its button gets the click.
            GUI.depth = ended ? -100 : 0;
            if (HasEscaped)
            {
                if (EndScreen.Draw(EndScreenKind.Escaped, endScreenShownAt)) RestartEncounter();
                return;
            }
            Color previous = GUI.color;
            if (!ended && Time.time < hitEndsAt)
            {
                GUI.color = new Color(0.65f, 0.02f, 0.04f, 0.12f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = previous;
            GUI.Label(new Rect(24f, Screen.height - 64f, 180f, 34f), "Health " + Mathf.CeilToInt(health.CurrentHealth), labelStyle);
            if (!ended) return;
            if (EndScreen.Draw(IsCaptured ? EndScreenKind.Caught : EndScreenKind.Died, endScreenShownAt)) RestartEncounter();
        }
    }
}

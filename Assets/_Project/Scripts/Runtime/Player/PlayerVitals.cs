using Lockdown.Combat;
using Lockdown.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(FirstPersonController))]
    public sealed class PlayerVitals : MonoBehaviour
    {
        private Health health;
        private FirstPersonController controller;
        private float hitEndsAt;
        private GUIStyle labelStyle;
        private GUIStyle deathStyle;

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<FirstPersonController>();
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
        }

        private void ShowHit() { hitEndsAt = Time.time + 0.2f; }

        private void Die()
        {
            controller.ReleaseCursor();
            controller.enabled = false;
            foreach (var weapon in GetComponentsInChildren<FirstPersonWeapon>()) weapon.enabled = false;
        }

        public void RestartEncounter()
        {
            if (!health.IsDead) return;
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
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, normal = { textColor = Color.white } };
                deathStyle = new GUIStyle(labelStyle) { fontSize = 30, alignment = TextAnchor.MiddleCenter };
            }
            Color previous = GUI.color;
            if (health.IsDead || Time.time < hitEndsAt)
            {
                GUI.color = new Color(0.65f, 0.02f, 0.04f, health.IsDead ? 0.3f : 0.12f);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
            GUI.color = previous;
            GUI.Label(new Rect(24f, Screen.height - 64f, 180f, 34f), "Health " + Mathf.CeilToInt(health.CurrentHealth), labelStyle);
            if (!health.IsDead) return;
            GUI.Label(new Rect(0f, Screen.height * 0.5f - 60f, Screen.width, 48f), "You died", deathStyle);
            if (GUI.Button(new Rect(Screen.width * 0.5f - 60f, Screen.height * 0.5f, 120f, 40f), "Restart")) RestartEncounter();
        }
    }
}

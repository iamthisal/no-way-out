#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.Combat;
using Lockdown.Player;
using Lockdown.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Tests.Director
{
    /// <summary>Loads the real museum and finds what the director tests need.</summary>
    internal static class MuseumFixture
    {
        public const string Scene = "Assets/_Project/Scenes/Museum/Museum_Main.unity";

        public static IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode(Scene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        public static FirstPersonController Player => Object.FindAnyObjectByType<FirstPersonController>();
        public static SpawnDirectorAgent Director => Object.FindAnyObjectByType<SpawnDirectorAgent>();
        public static AlarmStimulusRelay Relay => Object.FindAnyObjectByType<AlarmStimulusRelay>();
        public static GlassAlarm Alarm => Object.FindAnyObjectByType<GlassAlarm>();
        public static PursuerAgent Guard => Object.FindObjectsByType<PursuerAgent>().First(guard => guard.name == "Pursuer");

        /// <summary>The display case closest to a place.</summary>
        public static BreakableGlass CaseNear(Vector3 position) => Object.FindObjectsByType<BreakableGlass>()
            .Where(glass => glass.name == "M_Museum_DisplayGlass")
            .OrderBy(glass => (glass.transform.position - position).sqrMagnitude).First();

        public static void Break(BreakableGlass glass) { glass.GetComponent<Health>().TakeDamage(1000f); }

        /// <summary>Keeps the player out of sight of everything, in a closed corner.</summary>
        public static void MovePlayer(Vector3 position, Quaternion rotation)
        {
            var body = Player.GetComponent<CharacterController>();
            body.enabled = false;
            Player.transform.SetPositionAndRotation(position, rotation);
            body.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
#endif

using System;
using System.IO;
using System.Linq;
using Lockdown.AI.Agents;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    /// <summary>Runs an explicitly requested editor-only check; never runs in a game build.</summary>
    [InitializeOnLoad]
    public static class PursuerLiveSync
    {
        private static string RequestPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PursuerLiveSync.request"));
        private static string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/PursuerLiveState.json"));

        static PursuerLiveSync() { EditorApplication.update += CheckRequest; }

        [Serializable]
        private sealed class State
        {
            public string timestamp;
            public string scenePath;
            public bool playing;
            public bool dirty;
            public bool present;
            public int renderers;
            public int lootContainers;
            public bool prefabImported;
            public int missingMeshes;
            public Vector3 position;
            public string cameras;
            public string action;
            public string error;
            public string aiState;
            public bool onNavMesh;
            public bool playerVisible;
            public int patrolPoints;
            public int wanderingEnemies;
        }

        private static void CheckRequest()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                var state = new State { timestamp = DateTime.UtcNow.ToString("O"), playing = EditorApplication.isPlaying };
                var scene = SceneManager.GetActiveScene();
                var command = File.ReadAllText(RequestPath).Trim();
                if (command == "verify-wanderer") PursuerRegressionRunner.Run();
                if (command == "verify-decisions") PursuerRegressionRunner.RunDecisions();
                if (command == "snapshot-scene")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
                    Directory.CreateDirectory("Assets/_Recovery");
                    EditorSceneManager.SaveScene(scene, "Assets/_Recovery/PursuerBeforeSetup.unity", true);
                }
                if (command == "reload-preserved-scene")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
                    scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Museum/Museum_Main.unity");
                    PursuerAIBuilder.Connect();
                }
                if (command == "setup-ai")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
                    if (scene.isDirty) throw new InvalidOperationException("Save the editor's unsaved scene edits before reloading the merged museum.");
                    scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Museum/Museum_Main.unity");
                    PursuerAIBuilder.Connect();
                }
                if (command == "stop-play") EditorApplication.isPlaying = false;
                if (command == "remove-wanderer")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.isPlaying = false; return; }
                    if (scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                        throw new InvalidOperationException("Removal requires Museum_Main in Edit mode.");
                    if (scene.isDirty)
                    {
                        Directory.CreateDirectory("Assets/_Recovery");
                        EditorSceneManager.SaveScene(scene, "Assets/_Recovery/BeforeWandererRemoval_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".unity", true);
                    }
                    var enemies = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Lockdown.Enemies.WanderingEnemy>(true)).ToArray();
                    foreach (var enemy in enemies) Undo.DestroyObjectImmediate(enemy.gameObject);
                    if (enemies.Length > 0)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                    state.action = "Removed " + enemies.Length + " legacy wandering enemies from the museum.";
                }
                if (command == "connect-ai") PursuerAIBuilder.Connect();
                if (command == "verify-ai") PursuerAIVerification.Start();
                if (command == "sync")
                {
                    if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                        throw new InvalidOperationException("Sync requires Museum_Main in Edit mode.");
                    if (!scene.isDirty)
                    {
                        scene = EditorSceneManager.OpenScene(scene.path);
                        state.action = "Reloaded saved Museum_Main into the clean editor scene.";
                    }
                    else
                    {
                        var current = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Pursuer_VisualOnly" || root.name == "Pursuer");
                        if (current == null)
                        {
                            current = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PursuerVisualBuilder.PrefabPath), scene);
                            current.name = "Pursuer_VisualOnly";
                            current.transform.SetPositionAndRotation(new Vector3(-1.3f, 0f, -7.4f), Quaternion.Euler(0f, 160f, 0f));
                            Undo.RegisterCreatedObjectUndo(current, "Place pursuer visual");
                            EditorSceneManager.MarkSceneDirty(scene);
                        }
                        state.action = "Placed visual in loaded scene while preserving unsaved edits.";
                    }
                }
                state.scenePath = scene.path;
                state.wanderingEnemies = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<Lockdown.Enemies.WanderingEnemy>(true).Length);
                state.dirty = scene.isDirty;
                var visual = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Pursuer_VisualOnly" || root.name == "Pursuer");
                state.present = visual != null;
                state.renderers = visual != null ? visual.GetComponentsInChildren<Renderer>().Length : 0;
                state.missingMeshes = visual != null ? visual.GetComponentsInChildren<SkinnedMeshRenderer>().Count(renderer => renderer.sharedMesh == null) : 0;
                state.position = visual != null ? visual.transform.position : Vector3.zero;
                state.cameras = string.Join("; ", scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Select(camera => camera.name + " enabled=" + camera.enabled + " at=" + camera.transform.position));
                state.lootContainers = scene.GetRootGameObjects().Sum(root => root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Count(component => component != null && component.GetType().Name == "LootContainer"));
                state.prefabImported = AssetDatabase.LoadAssetAtPath<GameObject>(PursuerVisualBuilder.PrefabPath) != null;
                if (visual != null && visual.TryGetComponent<PursuerAgent>(out var brain))
                {
                    state.aiState = brain.State.ToString();
                    state.playerVisible = brain.IsPlayerVisible;
                    var nav = visual.GetComponent<NavMeshAgent>();
                    state.onNavMesh = nav.enabled && nav.isOnNavMesh;
                    state.patrolPoints = new SerializedObject(brain).FindProperty("patrolPoints").arraySize;
                }
                if (visual != null && command == "sync")
                {
                    Selection.activeGameObject = visual;
                    if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Frame(new Bounds(visual.transform.position + Vector3.up, new Vector3(3f, 3f, 3f)), false);
                }
                File.WriteAllText(ReportPath, JsonUtility.ToJson(state, true));
                File.Delete(RequestPath);
            }
            catch (Exception error)
            {
                File.WriteAllText(ReportPath, JsonUtility.ToJson(new State { timestamp = DateTime.UtcNow.ToString("O"), error = error.ToString() }, true));
                File.Delete(RequestPath);
            }
        }
    }
}

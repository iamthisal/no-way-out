using System;
using System.Linq;
using Lockdown.Enemies;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    public static class HoundResponseBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/InvestigatingHound.prefab";

        [MenuItem("Tools/Lockdown/Build Glass Hound Response")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HoundModelBuilder.PrefabPath);
            if (source == null) throw new InvalidOperationException("Build the museum hound model first.");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                root.name = "InvestigatingHound";
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.32f;
                agent.height = 1.05f;
                agent.baseOffset = -0.05f;
                agent.speed = 3.2f;
                agent.acceleration = 9f;
                agent.angularSpeed = 360f;
                agent.stoppingDistance = 0.65f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                root.AddComponent<HoundInvestigator>();
                root.AddComponent<HoundLocomotion>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            // Replace the earlier presentation-only scene instance with an empty director.
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "MuseumHound") Undo.DestroyObjectImmediate(root);
            var existing = Object.FindObjectsByType<GlassHoundResponse>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.scene == scene);
            var director = existing != null ? existing.gameObject : new GameObject("GlassHoundResponse");
            var response = director.GetComponent<GlassHoundResponse>() ?? director.AddComponent<GlassHoundResponse>();
            var entries = new Transform[3];
            var positions = new[] { new Vector3(0f, 0f, 7f), new Vector3(-9f, 0f, 25f), new Vector3(9f, 0f, 25f) };
            var names = new[] { "Entry_CentralHall", "Entry_WestGallery", "Entry_EastGallery" };
            Physics.SyncTransforms();
            for (int i = 0; i < entries.Length; i++)
            {
                if (!NavMesh.SamplePosition(positions[i], out var hit, 1.2f, NavMesh.AllAreas))
                    throw new InvalidOperationException("No NavMesh at " + names[i] + ": " + positions[i]);
                var entry = director.transform.Find(names[i]);
                if (entry == null) { entry = new GameObject(names[i]).transform; entry.SetParent(director.transform, false); }
                entry.SetPositionAndRotation(hit.position, Quaternion.identity);
                entries[i] = entry;
            }
            var serialized = new SerializedObject(response);
            serialized.FindProperty("houndPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var glasses = Object.FindObjectsByType<BreakableGlass>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(g => g.gameObject.scene == scene).OrderBy(g => g.transform.position.z).ThenBy(g => g.transform.position.x).ToArray();
            var sources = serialized.FindProperty("glassSources");
            sources.arraySize = glasses.Length;
            for (int i = 0; i < glasses.Length; i++) sources.GetArrayElementAtIndex(i).objectReferenceValue = glasses[i];
            var points = serialized.FindProperty("entryPoints");
            points.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Glass hound response configured: " + glasses.Length + " cases, three entries, no initial dogs.");
        }
    }
}

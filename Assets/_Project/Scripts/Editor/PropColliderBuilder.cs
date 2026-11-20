using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    /// <summary>
    /// The FixedCover props started as greybox cubes whose Box Colliders were the "target outer
    /// bounds for later imported art". The cube renderers were later hidden and the art placed
    /// inside, but several boxes stayed taller than the art, so shots (and enemy sight) stopped in
    /// mid-air above it. This replaces each such box with exact mesh colliders on the visible art.
    /// </summary>
    public static class PropColliderBuilder
    {
        private const string MuseumScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";
        // A box counts as oversized when it rises this far above the art it stands for.
        public const float Tolerance = 0.1f;

        [MenuItem("Tools/Lockdown/Fit Oversized Prop Colliders To Art")]
        public static void FitOversizedPropColliders()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MuseumScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var museum = GameObject.Find("Museum");
            if (museum == null) throw new InvalidOperationException("No 'Museum' root found.");
            var cover = museum.transform.Find("FixedCover");
            if (cover == null) throw new InvalidOperationException("No FixedCover group found.");

            Undo.SetCurrentGroupName("Fit oversized prop colliders");
            int group = Undo.GetCurrentGroup();
            var fitted = new List<string>();
            foreach (var box in cover.GetComponentsInChildren<BoxCollider>(true))
            {
                var art = VisibleArt(box);
                if (art.Count == 0 || !IsOversized(box, art)) continue;
                int layer = box.gameObject.layer;
                foreach (var filter in art)
                {
                    EnsureReadable(filter.sharedMesh);
                    var collider = filter.GetComponent<MeshCollider>();
                    if (collider == null) collider = Undo.AddComponent<MeshCollider>(filter.gameObject);
                    Undo.RecordObject(collider, "Fit prop collider");
                    collider.sharedMesh = filter.sharedMesh;
                    collider.convex = false;
                    Undo.RecordObject(filter.gameObject, "Fit prop collider");
                    filter.gameObject.layer = layer;
                }
                fitted.Add(box.name);
                Undo.DestroyObjectImmediate(box);
            }
            BoundaryDoorBuilder.FitPlanterCollider(museum.transform);
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Fitted mesh colliders to " + fitted.Count + " props (" + string.Join(", ", fitted) +
                      ") and the courtyard vase. Rebake the museum NavMesh and save the scene.");
        }

        /// <summary>The enabled art meshes a placeholder box stands for (its children, not itself).</summary>
        public static List<MeshFilter> VisibleArt(BoxCollider box)
        {
            var art = new List<MeshFilter>();
            var own = box.GetComponent<MeshRenderer>();
            if (own != null && own.enabled) return art; // The box is itself the visible art.
            foreach (var filter in box.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.gameObject == box.gameObject || filter.sharedMesh == null) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.enabled && filter.gameObject.activeInHierarchy) art.Add(filter);
            }
            return art;
        }

        public static bool IsOversized(BoxCollider box, List<MeshFilter> art)
        {
            Bounds bounds = art[0].GetComponent<Renderer>().bounds;
            foreach (var filter in art) bounds.Encapsulate(filter.GetComponent<Renderer>().bounds);
            return box.bounds.max.y - bounds.max.y > Tolerance;
        }

        /// <summary>
        /// Mesh colliders need Read/Write meshes to cook at load time, because Prebake Collision
        /// Meshes is off in this project. Built-in meshes are always readable.
        /// </summary>
        public static void EnsureReadable(Mesh mesh)
        {
            if (mesh == null || mesh.isReadable) return;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (AssetImporter.GetAtPath(path) is ModelImporter importer)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                return;
            }
            var serialized = new SerializedObject(mesh);
            serialized.FindProperty("m_IsReadable").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mesh);
        }
    }
}

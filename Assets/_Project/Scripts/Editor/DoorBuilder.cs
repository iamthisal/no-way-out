using System;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class DoorBuilder
    {
        private static readonly string[] DoorNames =
        {
            "Door_D06_Storage", "Door_D07_Storage", "Door_D08_Storage",
            "Door_D09_Security", "Door_D10_Security", "Door_D11_Toilet", "Door_D21_GuestToilet"
        };

        [MenuItem("Tools/Lockdown/Configure Museum Doors")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");

            // Resolve everything before editing, so a missing model cannot leave a partial setup.
            var roots = new Transform[DoorNames.Length];
            var panels = new Renderer[DoorNames.Length];
            for (int i = 0; i < DoorNames.Length; i++)
            {
                roots[i] = Find(scene, DoorNames[i]);
                if (roots[i] == null) throw new InvalidOperationException("Missing door: " + DoorNames[i]);
                if (roots[i].GetComponentInChildren<Door>(true) != null) continue;
                bool toilet = DoorNames[i].Contains("Toilet");
                foreach (var renderer in roots[i].GetComponentsInChildren<Renderer>(true))
                    if (!toilet || renderer.name == "Cube") { panels[i] = renderer; break; }
                if (panels[i] == null) throw new InvalidOperationException("Missing door panel: " + DoorNames[i]);
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure museum doors");
            for (int i = 0; i < roots.Length; i++)
                if (panels[i] != null) Configure(roots[i], panels[i], DoorNames[i].Contains("Toilet"));
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Museum doors configured. Save the scene to keep the changes.");
        }

        private static void Configure(Transform root, Renderer panel, bool toilet)
        {
            // The washroom asset has separate panel/frame meshes inside one model prefab.
            // Unpack the instance so only its panel can be moved beneath an unscaled hinge.
            if (toilet && PrefabUtility.IsPartOfPrefabInstance(panel))
            {
                var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(panel.gameObject);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            }

            Quaternion orientation = root.rotation * Quaternion.Euler(0f, toilet ? 90f : 0f, 0f);
            var bounds = PanelBounds(panel, root.position, orientation);
            var hingeObject = new GameObject("Hinge");
            Undo.RegisterCreatedObjectUndo(hingeObject, "Create door hinge");
            var hinge = hingeObject.transform;
            hinge.SetParent(root, false);
            hinge.SetPositionAndRotation(root.position + orientation *
                new Vector3(bounds.min.x, bounds.center.y, bounds.center.z), orientation);
            Undo.SetTransformParent(panel.transform, hinge, "Attach moving door panel");

            foreach (var child in hinge.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(child.gameObject, "Make door movable");
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
            }

            var collider = Undo.AddComponent<BoxCollider>(hingeObject);
            collider.center = new Vector3(bounds.size.x * 0.5f, 0f, 0f);
            collider.size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(0.08f, bounds.size.z));
            var obstacle = Undo.AddComponent<NavMeshObstacle>(hingeObject);
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = collider.center;
            obstacle.size = collider.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            Undo.AddComponent<Door>(hingeObject);

            if (toilet)
            {
                foreach (var frame in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (frame.transform.IsChildOf(hinge) || frame.GetComponent<Collider>() != null) continue;
                    var frameCollider = Undo.AddComponent<MeshCollider>(frame.gameObject);
                    frameCollider.sharedMesh = frame.sharedMesh;
                }
            }
        }

        private static Bounds PanelBounds(Renderer renderer, Vector3 origin, Quaternion rotation)
        {
            var local = renderer.localBounds;
            var inverse = Quaternion.Inverse(rotation);
            var bounds = new Bounds();
            bool first = true;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        var corner = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                        var point = inverse * (renderer.transform.TransformPoint(corner) - origin);
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
            return bounds;
        }

        private static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child;
            return null;
        }
    }
}

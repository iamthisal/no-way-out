using System;
using Lockdown.Player;
using Lockdown.Weapons;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class HammerBuilder
    {
        [MenuItem("Tools/Lockdown/Configure Storage Hammer")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var worldHammer = Find(scene, "hammer_low");
            if (worldHammer == null) throw new InvalidOperationException("The storage hammer model is missing.");
            ConfigurePlayer(worldHammer.gameObject);
            if (worldHammer.GetComponent<HammerPickup>() == null)
            {
                Undo.AddComponent<HammerPickup>(worldHammer.gameObject);
                var collider = worldHammer.GetComponent<BoxCollider>();
                if (collider == null) collider = Undo.AddComponent<BoxCollider>(worldHammer.gameObject);
                var bounds = BoundsIn(worldHammer, worldHammer.GetComponentsInChildren<Renderer>());
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Storage hammer and player equipment configured. Save the scene to keep the pickup.");
        }

        private static void ConfigurePlayer(GameObject worldHammer)
        {
            var player = PrefabUtility.LoadPrefabContents(FirstPersonPlayerBuilder.PrefabPath);
            try
            {
                var camera = player.transform.Find("PlayerCamera");
                if (camera == null) throw new InvalidOperationException("Player camera is missing.");
                var held = camera.Find("HeldHammer");
                if (held == null)
                {
                    held = new GameObject("HeldHammer").transform;
                    held.SetParent(camera, false);
                    held.localPosition = new Vector3(0.28f, -0.24f, 0.55f);
                    var model = UnityEngine.Object.Instantiate(worldHammer, held).transform;
                    model.name = "HammerModel";
                    model.localPosition = Vector3.zero;
                    model.localRotation = Quaternion.Euler(0f, 0f, 180f);
                    model.localScale = Vector3.one;
                    model.gameObject.SetActive(true);
                    foreach (var pickup in model.GetComponentsInChildren<HammerPickup>(true)) UnityEngine.Object.DestroyImmediate(pickup);
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
                    var bounds = BoundsIn(held, model.GetComponentsInChildren<Renderer>());
                    model.localScale *= 0.55f / Mathf.Max(0.001f, bounds.size.y);
                    bounds = BoundsIn(held, model.GetComponentsInChildren<Renderer>());
                    model.localPosition += new Vector3(-bounds.center.x, 0.12f - bounds.center.y, -bounds.center.z);
                    var gripPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerEquipmentBuilder.GripPath);
                    if (gripPrefab != null)
                    {
                        var grip = (GameObject)PrefabUtility.InstantiatePrefab(gripPrefab, player.scene);
                        grip.name = "HammerGripHand";
                        grip.transform.SetParent(held, false);
                    }
                    foreach (var child in held.GetComponentsInChildren<Transform>(true))
                        GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                    held.gameObject.AddComponent<FirstPersonHammer>();
                }
                held.gameObject.SetActive(false);
                var equipment = player.GetComponent<PlayerEquipment>();
                var serialized = new SerializedObject(equipment);
                serialized.FindProperty("hammer").objectReferenceValue = held.GetComponent<FirstPersonHammer>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, FirstPersonPlayerBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        internal static Transform Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child;
            return null;
        }

        internal static Bounds BoundsIn(Transform target, Renderer[] renderers)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in renderers)
            {
                var local = renderer.localBounds;
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                        {
                            var point = target.InverseTransformPoint(renderer.transform.TransformPoint(
                                local.center + Vector3.Scale(local.extents, new Vector3(x, y, z))));
                            if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                            else bounds.Encapsulate(point);
                        }
            }
            return bounds;
        }
    }
}

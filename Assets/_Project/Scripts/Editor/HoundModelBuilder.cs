using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class HoundModelBuilder
    {
        public const string ModelPath = "Assets/_Project/Models/Hound/MuseumHound.fbx";
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/MuseumHound.prefab";
        public const string ControllerPath = "Assets/_Project/Animations/Hound/HoundIdle.controller";
        private const string MaterialFolder = "Assets/_Project/Materials/Enemies/Hound";
        private static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            { "Hound_Tan", new Color(0.40f, 0.235f, 0.115f).gamma },
            { "Hound_TanLight", new Color(0.58f, 0.38f, 0.19f).gamma },
            { "Hound_Saddle", new Color(0.095f, 0.062f, 0.043f).gamma },
            { "Hound_Black", new Color(0.025f, 0.022f, 0.020f).gamma },
            { "Hound_Amber", new Color(0.66f, 0.32f, 0.065f).gamma },
            { "Hound_Glint", new Color(0.78f, 0.80f, 0.72f).gamma },
            { "Hound_Collar", new Color(0.075f, 0.105f, 0.080f).gamma },
            { "Hound_Brass", new Color(0.40f, 0.31f, 0.16f).gamma }
        };

        [MenuItem("Tools/Lockdown/Build Museum Hound Model")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the hound prefab.");
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate MuseumHound.fbx with Blender/Hound/build_hound.py first.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.globalScale = 1f;
            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0) throw new InvalidOperationException("The hound FBX must contain its idle action.");
            clips[0].name = "HoundIdle";
            clips[0].loopTime = true;
            clips[0].loopPose = true;
            clips[0].lockRootRotation = true;
            clips[0].keepOriginalOrientation = true;
            clips[0].lockRootPositionXZ = true;
            clips[0].lockRootHeightY = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Animations/Hound");
            AnimationClip idle = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is AnimationClip candidate && !candidate.name.StartsWith("__preview")) { idle = candidate; break; }
            if (idle == null) throw new InvalidOperationException("The imported idle clip is missing.");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                var state = controller.layers[0].stateMachine.AddState("HoundIdle");
                state.motion = idle;
                controller.layers[0].stateMachine.defaultState = state;
            }
            else controller.layers[0].stateMachine.defaultState.motion = idle;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("MuseumHound");
                SceneManager.MoveGameObjectToScene(root, preview);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = MaterialFor(materials[i].name);
                    renderer.sharedMaterials = materials;
                    renderer.quality = SkinQuality.Bone2;
                }
                var head = FindBone(model.transform, "Head");
                var tail = FindBone(model.transform, "TailBase");
                if (head == null || tail == null) throw new InvalidOperationException("The hound head/tail rig bones are missing.");
                if (head.position.z < tail.position.z) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var bounds = CombinedBounds(model);
                if (bounds.size.y < 0.7f || bounds.size.y > 1.5f)
                    throw new InvalidOperationException("Unexpected hound import scale: " + bounds.size);
                model.transform.localPosition = Vector3.up * -bounds.min.y;
                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                var collider = root.AddComponent<CapsuleCollider>();
                collider.direction = 2;
                collider.center = new Vector3(0f, 0.48f, -0.06f);
                collider.radius = 0.25f;
                collider.height = 1.15f;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
            Debug.Log("MuseumHound prefab created with generic rig, idle animation, URP materials, and body collider.");
        }

        [MenuItem("Tools/Lockdown/Place Museum Hound")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "MuseumHound") { Selection.activeGameObject = root; return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build the museum hound first.");
            Quaternion rotation = Quaternion.Euler(0f, 210f, 0f);
            foreach (var candidate in new[] { new Vector3(2.4f, 0f, 0.5f), new Vector3(2.7f, 0f, 2.5f), new Vector3(1.5f, 0f, 1f), new Vector3(-2f, 0f, -0.5f) })
            {
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                Physics.SyncTransforms();
                if (!Physics.Raycast(hit.position + Vector3.up * 0.5f, Vector3.down, out var floor, 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var position = floor.point;
                if (Physics.CheckBox(position + Vector3.up * 0.48f, new Vector3(0.32f, 0.40f, 0.75f), rotation, ~0, QueryTriggerInteraction.Ignore)) continue;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetPositionAndRotation(position, rotation);
                Undo.RegisterCreatedObjectUndo(instance, "Place museum hound");
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = instance;
                Debug.Log("Museum hound placed in the central hall at " + position + ". Save the scene.");
                return;
            }
            throw new InvalidOperationException("No clear central-hall position was found for the hound.");
        }

        private static Material MaterialFor(string name)
        {
            if (!Palette.TryGetValue(name, out var color)) throw new InvalidOperationException("Unknown hound material: " + name);
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", name == "Hound_Black" || name == "Hound_Amber" ? 0.45f : 0.18f);
            material.SetFloat("_Metallic", name == "Hound_Brass" ? 0.5f : 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        private static Bounds CombinedBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("The hound has no renderers.");
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}

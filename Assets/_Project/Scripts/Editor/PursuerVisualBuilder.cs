using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    /// <summary>Builds a presentation-only character from the imported Quaternius rig.</summary>
    public static class PursuerVisualBuilder
    {
        public const string SourcePath = "Assets/_ThirdParty/Characters/QuaterniusToonShooter/Character_Soldier.gltf";
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/PursuerVisual.prefab";
        private const string MaterialFolder = "Assets/_Project/Materials/Enemies/Pursuer";
        private const string AnimationFolder = "Assets/_Project/Animations/Pursuer";
        private static string VerificationFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification/Pursuer"));
        private static readonly HashSet<string> Weapons = new HashSet<string> {
            "AK", "GrenadeLauncher", "Knife_1", "Knife_2", "Pistol", "Revolver", "Revolver_Small",
            "RocketLauncher", "ShortCannon", "Shotgun", "Shovel", "SMG", "Sniper", "Sniper_2"
        };

        public static void BuildImportPackage()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use Build or Place in the interactive editor.");
            Build();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            InstantiateVisual(scene);
            Directory.CreateDirectory("Assets/_Project/Scenes/Preview");
            EditorSceneManager.SaveScene(scene, "Assets/_Project/Scenes/Preview/PursuerPlacement.unity");
        }

        [MenuItem("Tools/Lockdown/Place Pursuer Visual in Museum")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Pursuer_VisualOnly" || root.GetComponent<Lockdown.AI.Agents.PursuerAgent>() != null);
            if (existing != null) { Selection.activeGameObject = existing; return; }
            var visual = InstantiateVisual(scene);
            Undo.RegisterCreatedObjectUndo(visual, "Place visual-only pursuer");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = visual;
        }

        private static GameObject InstantiateVisual(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build the Pursuer visual prefab first.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            visual.name = "Pursuer_VisualOnly";
            visual.transform.SetPositionAndRotation(new Vector3(-1.3f, 0f, -7.4f), Quaternion.Euler(0f, 160f, 0f));
            return visual;
        }

        [MenuItem("Tools/Lockdown/Build Pursuer Visual Only")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the character in Edit mode.");
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(AnimationFolder);
            Directory.CreateDirectory("Assets/_Project/Prefabs/Characters");
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            if (source == null) throw new InvalidOperationException("The soldier model has not imported: " + SourcePath);
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("PursuerVisual");
                SceneManager.MoveGameObjectToScene(root, scene);
                var model = Object.Instantiate(source, root.transform);
                model.name = "ImportedSoldier";
                foreach (var child in model.GetComponentsInChildren<Transform>(true))
                    if (Weapons.Contains(child.name)) child.gameObject.SetActive(child.name == "SMG");

                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader is required.");
                var materials = new Dictionary<string, Material>();
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mapped = renderer.sharedMaterials.Select(original =>
                    {
                        string name = original != null ? original.name.Replace(" (Instance)", "") : "DarkGrey";
                        if (!materials.TryGetValue(name, out var material))
                        {
                            string path = MaterialFolder + "/Pursuer_" + name + ".mat";
                            material = AssetDatabase.LoadAssetAtPath<Material>(path);
                            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                            material.shader = shader;
                            material.SetColor("_BaseColor", Palette(name));
                            material.SetFloat("_Smoothness", name.Contains("Grey") ? 0.28f : 0.12f);
                            material.SetFloat("_Metallic", name == "LightGrey" ? 0.45f : 0f);
                            EditorUtility.SetDirty(material);
                            materials.Add(name, material);
                        }
                        return material;
                    }).ToArray();
                    renderer.sharedMaterials = mapped;
                }

                var clips = AssetDatabase.LoadAllAssetsAtPath(SourcePath).OfType<AnimationClip>().ToArray();
                Debug.Log("Imported clips: " + string.Join(", ", clips.Select(clip => clip.name)));
                var idle = clips.FirstOrDefault(clip => clip.name == "Idle") ??
                    clips.FirstOrDefault(clip => clip.name.EndsWith("Idle", StringComparison.OrdinalIgnoreCase));
                if (idle == null) throw new InvalidOperationException("The imported rig needs its included idle animation.");
                var legacy = model.GetComponentsInChildren<Animation>(true);
                var animationRoot = legacy.Length > 0 ? legacy[0].gameObject : model;
                foreach (var animation in legacy) Object.DestroyImmediate(animation);
                string clipPath = AnimationFolder + "/PursuerIdle.anim";
                var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (idleClip == null) { idleClip = Object.Instantiate(idle); AssetDatabase.CreateAsset(idleClip, clipPath); }
                else EditorUtility.CopySerialized(idle, idleClip);
                idleClip.name = "PursuerIdle";
                idleClip.legacy = false;
                var settings = AnimationUtility.GetAnimationClipSettings(idleClip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(idleClip, settings);
                string controllerPath = AnimationFolder + "/PursuerVisual.controller";
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                var machine = controller.layers[0].stateMachine;
                var state = machine.states.FirstOrDefault().state;
                if (state == null) state = machine.AddState("Idle");
                state.motion = idleClip;
                machine.defaultState = state;
                var animator = animationRoot.GetComponent<Animator>();
                if (animator == null) animator = animationRoot.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                idleClip.SampleAnimation(animationRoot, 0f);

                var bounds = Bounds(root);
                if (bounds.size.y < 0.01f) throw new InvalidOperationException("The imported model has no visible geometry.");
                model.transform.localScale *= 1.9f / bounds.size.y;
                bounds = Bounds(root);
                model.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var collider = root.AddComponent<CapsuleCollider>();
                collider.height = 1.9f;
                collider.radius = 0.3f;
                collider.center = Vector3.up * 0.95f;
                if (root.GetComponentsInChildren<MonoBehaviour>(true).Any())
                    throw new InvalidOperationException("The presentation prefab must not contain gameplay scripts.");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                WriteVerification(root, clips.Length);
                RenderPreview(root);
                Debug.Log("Pursuer visual built: imported soldier, idle animation, passive collider, no AI or combat scripts.");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static Color Palette(string name)
        {
            string hex = name switch {
                "Character_Main" => "343B48", "Pants" => "293341", "DarkGrey" => "242831",
                "Black" => "11151C", "Grey" => "727B88", "Grey2" => "4B5462",
                "LightGrey" => "BEA16A", "Red" => "742D3E", "Skin" => "BD986E",
                "Wood" => "614A35", "DarkWood" => "3D3028", _ => "454C57"
            };
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }

        private static Bounds Bounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>().Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0) return new Bounds();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void WriteVerification(GameObject root, int clipCount)
        {
            Directory.CreateDirectory(VerificationFolder);
            File.WriteAllText(Path.Combine(VerificationFolder, "character.txt"),
                "Source: Quaternius Toon Shooter Game Kit / Character_Soldier.\n" +
                "Imported clips: " + clipCount + "\nHeight: " + Bounds(root).size.y +
                "\nGameplay scripts: " + root.GetComponentsInChildren<MonoBehaviour>(true).Length +
                "\nActive weapons: " + string.Join(", ", root.GetComponentsInChildren<Transform>()
                    .Where(child => Weapons.Contains(child.name)).Select(child => child.name)));
        }

        private static void RenderPreview(GameObject root)
        {
            var previewMaterials = new List<Material>();
            var originalMaterials = new Dictionary<Renderer, Material[]>();
            // The isolated import project may use the built-in renderer; keep this
            // preview compatible while the saved prefab retains its URP materials.
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    originalMaterials[renderer] = renderer.sharedMaterials;
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        var material = new Material(Shader.Find("Standard"));
                        material.color = original.GetColor("_BaseColor");
                        previewMaterials.Add(material);
                        return material;
                    }).ToArray();
                }
            }
            var cameraObject = new GameObject("Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, root.scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.scene = root.scene;
            camera.transform.position = new Vector3(3.2f, 1.8f, 4.2f);
            camera.transform.LookAt(new Vector3(0f, 0.95f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.19f, 0.22f);
            camera.fieldOfView = 30f;
            var lightObject = new GameObject("Preview Key");
            SceneManager.MoveGameObjectToScene(lightObject, root.scene);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            var target = RenderTexture.GetTemporary(900, 1000, 24);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(900, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 900, 1000), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(VerificationFolder, "character.png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                foreach (var entry in originalMaterials) entry.Key.sharedMaterials = entry.Value;
                foreach (var material in previewMaterials) Object.DestroyImmediate(material);
            }
        }
    }
}

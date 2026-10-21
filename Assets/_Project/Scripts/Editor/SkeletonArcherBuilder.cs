using System;
using System.Collections.Generic;
using System.IO;
using Lockdown.AI.Agents;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    public static class SkeletonArcherBuilder
    {
        public const string ModelPath = "Assets/_Project/Models/SkeletonArcher/SkeletonArcher.fbx";
        public const string ArrowModelPath = "Assets/_Project/Models/SkeletonArcher/SkeletonArrow.fbx";
        public const string ClipTablePath = "Assets/_Project/Models/SkeletonArcher/SkeletonArcher.clips.json";
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/SkeletonArcher.prefab";
        public const string ArrowPrefabPath = "Assets/_Project/Prefabs/Characters/SkeletonArrow.prefab";
        public const string ControllerPath = "Assets/_Project/Animations/SkeletonArcher/SkeletonArcher.controller";
        private const string MaterialFolder = "Assets/_Project/Materials/Enemies/SkeletonArcher";
        private const string AudioFolder = "Assets/_Project/Audio/SkeletonArcher";
        private const string ScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";
        // Natural History gallery bounds from the World Builder manual.
        private static readonly Rect NaturalHistory = Rect.MinMaxRect(3.5f, 14f, 10.5f, 22f);
        private static readonly string[] HeadPieces = { "Piece_Skull", "Piece_Jaw" };
        private static readonly string[] BowArmPieces = { "Piece_UpperArm_L", "Piece_Forearm_L", "Piece_Hand_L" };
        private static readonly string[] GearPieces = { "Piece_Bow", "Piece_Quiver", "Piece_Belt", "Piece_Dagger", "Piece_NockedArrow" };

        // Linear colours from the Blender palette, with smoothness, metallic and emission intensity.
        private static readonly Dictionary<string, (Color color, float smoothness, float metallic, float emission)> Palette =
            new Dictionary<string, (Color, float, float, float)>
            {
                { "Skeleton_Bone", (new Color(0.55f, 0.47f, 0.33f), 0.22f, 0f, 0f) },
                { "Skeleton_BoneDark", (new Color(0.30f, 0.235f, 0.15f), 0.18f, 0f, 0f) },
                { "Skeleton_Socket", (new Color(0.018f, 0.014f, 0.011f), 0.08f, 0f, 0f) },
                { "Skeleton_EyeGlow", (new Color(0.25f, 0.85f, 0.95f), 0.5f, 0f, 3f) },
                { "Skeleton_Brass", (new Color(0.40f, 0.31f, 0.16f), 0.45f, 0.5f, 0f) },
                { "Skeleton_Tag", (new Color(0.62f, 0.58f, 0.46f), 0.12f, 0f, 0f) },
                { "Skeleton_Wood", (new Color(0.10f, 0.055f, 0.03f), 0.32f, 0f, 0f) },
                { "Skeleton_Shaft", (new Color(0.36f, 0.24f, 0.12f), 0.25f, 0f, 0f) },
                { "Skeleton_Leather", (new Color(0.13f, 0.07f, 0.035f), 0.22f, 0f, 0f) },
                { "Skeleton_String", (new Color(0.55f, 0.50f, 0.40f), 0.15f, 0f, 0f) },
                { "Skeleton_Iron", (new Color(0.09f, 0.09f, 0.10f), 0.55f, 0.7f, 0f) },
                { "Skeleton_Fletch", (new Color(0.38f, 0.06f, 0.04f), 0.12f, 0f, 0f) },
            };

        [Serializable] private sealed class ClipTable { public int fps; public ClipRow[] clips; }
        [Serializable] private sealed class ClipRow { public string name; public int first; public int last; public bool loop; }

        [MenuItem("Tools/Lockdown/Build Skeleton Archer")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the skeleton archer.");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Animations/SkeletonArcher");
            var clips = ConfigureArcherImport();
            ConfigureArrowImport();
            var controller = BuildController(clips);
            var arrow = BuildArrowPrefab();
            BuildArcherPrefab(controller, arrow);
            AssetDatabase.SaveAssets();
            Debug.Log("SkeletonArcher prefab built: 25 rigid bone pieces with hitboxes, " + clips.Count + " clips, arrow projectile and AI.");
        }

        private static Dictionary<string, AnimationClip> ConfigureArcherImport()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate SkeletonArcher.fbx with Blender/SkeletonArcher/build_skeleton_archer.py first.");
            var table = JsonUtility.FromJson<ClipTable>(File.ReadAllText(ClipTablePath));
            if (table?.clips == null || table.clips.Length == 0) throw new InvalidOperationException("The clip table is empty.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.globalScale = 1f;
            var defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) throw new InvalidOperationException("The archer FBX must contain its animation take.");
            // Blender keys frame 0 as the first frame of the take; keep the table's numbering relative to it.
            float offset = defaults[0].firstFrame - table.clips[0].first;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var row in table.clips)
                clips.Add(new ModelImporterClipAnimation
                {
                    name = row.name,
                    takeName = defaults[0].takeName,
                    firstFrame = row.first + offset,
                    lastFrame = row.last + offset,
                    loopTime = row.loop,
                    loopPose = false,
                    wrapMode = row.loop ? WrapMode.Loop : WrapMode.ClampForever,
                    lockRootRotation = true,
                    lockRootHeightY = true,
                    lockRootPositionXZ = true,
                    keepOriginalOrientation = true,
                    keepOriginalPositionY = true,
                    keepOriginalPositionXZ = true
                });
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();

            var result = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) result[clip.name] = clip;
            foreach (var row in table.clips)
                if (!result.ContainsKey(row.name)) throw new InvalidOperationException("Imported clip missing: " + row.name);
            return result;
        }

        private static void ConfigureArrowImport()
        {
            var importer = AssetImporter.GetAtPath(ArrowModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate SkeletonArrow.fbx with the Blender script first.");
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            // The controller is generated: rebuild it so its states always match the clip table.
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 2f);
            tree.AddChild(clips["Run"], 4.2f);
            var machine = controller.layers[0].stateMachine;
            // States change from code with cross-fades; the agent and bow own the transitions.
            foreach (var name in new[] { "Crouch", "Draw", "AimHold", "Release", "Nock", "Hit" })
                machine.AddState(name).motion = clips[name];
            machine.defaultState = locomotion;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static ArrowProjectile BuildArrowPrefab()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowModelPath);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("SkeletonArrow");
                SceneManager.MoveGameObjectToScene(root, preview);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                ApplyMaterials(model);
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                var bounds = CombinedBounds(model);
                // The tip sits at the root and the shaft trails behind along -Z, whatever axis the FBX conversion produced.
                if (bounds.center.sqrMagnitude < 1e-4f) throw new InvalidOperationException("The arrow pivot must be at its tip.");
                model.transform.localRotation = Quaternion.FromToRotation(bounds.center.normalized, Vector3.back) * model.transform.localRotation;
                bounds = CombinedBounds(model);
                if (Mathf.Abs(bounds.max.z) > 0.03f || bounds.size.z < 0.7f)
                    throw new InvalidOperationException("Unexpected arrow pivot or length: " + bounds);

                var trailObject = new GameObject("Trail");
                trailObject.transform.SetParent(root.transform, false);
                trailObject.transform.localPosition = new Vector3(0f, 0f, bounds.min.z + 0.05f);
                var trail = trailObject.AddComponent<TrailRenderer>();
                trail.time = 0.12f;
                trail.startWidth = 0.025f;
                trail.endWidth = 0f;
                trail.minVertexDistance = 0.05f;
                trail.emitting = false;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.sharedMaterial = TrailMaterial();
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(new Color(0.95f, 0.92f, 0.85f), 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0.55f, 0f), new GradientAlphaKey(0f, 1f) });
                trail.colorGradient = gradient;

                var audio = root.AddComponent<AudioSource>();
                audio.clip = Clip("ArrowWhoosh");
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.minDistance = 1f;
                audio.maxDistance = 12f;
                audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.volume = 0.6f;
                audio.dopplerLevel = 0.5f;
                var projectile = root.AddComponent<ArrowProjectile>();
                Set(projectile, "trail", trail);
                Set(projectile, "flightAudio", audio);
                Set(projectile, "impactSound", Clip("ArrowImpact"));
                PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath).GetComponent<ArrowProjectile>();
        }

        private static void BuildArcherPrefab(AnimatorController controller, ArrowProjectile arrow)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("SkeletonArcher");
                SceneManager.MoveGameObjectToScene(root, preview);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                ApplyMaterials(model);

                var foot = Find(model.transform, "Piece_Foot_L");
                var ankle = Find(model.transform, "Foot_L");
                if (foot == null || ankle == null) throw new InvalidOperationException("The archer's foot piece or ankle bone is missing.");
                // Toes lie ahead of the ankle; turn the model round if they point to -Z.
                if (foot.GetComponent<Renderer>().bounds.center.z < ankle.position.z) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var bounds = CombinedBounds(model);
                if (bounds.size.y < 1.5f || bounds.size.y > 2.2f) throw new InvalidOperationException("Unexpected archer import scale: " + bounds.size);
                model.transform.localPosition = Vector3.up * -bounds.min.y;

                var animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var navAgent = root.AddComponent<NavMeshAgent>();
                navAgent.radius = 0.3f;
                navAgent.height = 1.85f;
                navAgent.speed = 2f;
                navAgent.angularSpeed = 600f;
                navAgent.acceleration = 14f;
                navAgent.stoppingDistance = 0.05f;
                navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                // A kinematic body lets the moving bone hitboxes act as one compound collider.
                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                var health = root.AddComponent<Health>();
                SetFloat(health, "maxHealth", 70f);
                var voice = root.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1f;
                voice.minDistance = 2f;
                voice.maxDistance = 20f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;

                var body = root.AddComponent<SkeletonBody>();
                var hitboxes = new List<SkeletonHitbox>();
                foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderer.name.StartsWith("Piece_")) continue;
                    renderer.gameObject.AddComponent<BoxCollider>();
                    var hitbox = renderer.gameObject.AddComponent<SkeletonHitbox>();
                    var serialized = new SerializedObject(hitbox);
                    serialized.FindProperty("region").enumValueIndex = (int)RegionFor(renderer.name);
                    serialized.FindProperty("body").objectReferenceValue = body;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    hitboxes.Add(hitbox);
                }
                if (hitboxes.Count != 25) throw new InvalidOperationException("Expected 25 bone pieces, found " + hitboxes.Count);
                var bowString = Find(model.transform, "BowString");
                var nocked = Find(model.transform, "Piece_NockedArrow");
                var arrowBone = Find(model.transform, "Arrow");
                if (bowString == null || nocked == null || arrowBone == null) throw new InvalidOperationException("Bow string, nocked arrow or arrow bone missing.");
                var arrowBounds = nocked.GetComponent<Renderer>().bounds;
                // In the bind pose the nocked arrow points along the model's forward axis.
                var tip = new GameObject("ArrowTip").transform;
                tip.SetParent(arrowBone, false);
                tip.SetPositionAndRotation(new Vector3(arrowBounds.center.x, arrowBounds.center.y, arrowBounds.max.z), root.transform.rotation);

                SetArray(body, "pieces", hitboxes.ToArray());
                Set(body, "animator", animator);
                Set(body, "bowString", bowString.GetComponent<Renderer>());
                Set(body, "collapseSound", Clip("BoneCollapse"));
                Set(body, "assembleSound", Clip("BoneAssemble"));

                var bow = root.AddComponent<SkeletonBow>();
                Set(bow, "animator", animator);
                Set(bow, "arrowTip", tip);
                SetArray(bow, "nockedArrow", new Object[] { nocked.GetComponent<Renderer>() });
                Set(bow, "arrowPrefab", arrow);
                Set(bow, "voice", voice);
                Set(bow, "drawSound", Clip("BowCreak"));
                Set(bow, "releaseSound", Clip("BowRelease"));

                var rattleObject = new GameObject("Rattle");
                rattleObject.transform.SetParent(root.transform, false);
                rattleObject.transform.localPosition = Vector3.up * 1f;
                var rattleAudio = rattleObject.AddComponent<AudioSource>();
                rattleAudio.clip = Clip("BoneRattleLoop");
                rattleAudio.loop = true;
                rattleAudio.playOnAwake = false;
                rattleAudio.spatialBlend = 1f;
                rattleAudio.minDistance = 1.5f;
                rattleAudio.maxDistance = 16f;
                rattleAudio.rolloffMode = AudioRolloffMode.Logarithmic;
                var rattle = rattleObject.AddComponent<SkeletonRattle>();
                Set(rattle, "body", body);
                Set(rattle, "tracked", root.transform);

                var agent = root.AddComponent<SkeletonArcherAgent>();
                Set(agent, "animator", animator);
                Set(agent, "bow", bow);
                Set(agent, "body", body);
                var dormancy = root.AddComponent<SkeletonDormancy>();
                Set(dormancy, "body", body);
                SetArray(dormancy, "enableOnWake", new Object[] { agent });

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        [MenuItem("Tools/Lockdown/Place Skeleton Archer")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == "SkeletonArcher") { Selection.activeGameObject = existing; return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build the skeleton archer first.");
            var centre = new Vector3(NaturalHistory.center.x, 0f, NaturalHistory.center.y);
            // Beside the stegosaurus, clear of the D17/D18 doorways.
            foreach (var candidate in new[] { new Vector3(8.7f, 0f, 16.0f), new Vector3(8.9f, 0f, 19.6f), new Vector3(5.4f, 0f, 16.1f), new Vector3(6.9f, 0f, 15.3f) })
            {
                if (!NavMesh.SamplePosition(candidate, out var hit, 0.8f, NavMesh.AllAreas)) continue;
                Physics.SyncTransforms();
                if (!Physics.Raycast(hit.position + Vector3.up * 0.5f, Vector3.down, out var floor, 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var position = floor.point;
                if (Physics.CheckBox(position + Vector3.up * 0.5f, new Vector3(0.5f, 0.4f, 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                var facing = centre - position;
                facing.y = 0f;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing.sqrMagnitude > 0.01f ? facing : Vector3.back));
                var glass = new List<Object>();
                foreach (var pane in Object.FindObjectsByType<BreakableGlass>())
                    if (NaturalHistory.Contains(new Vector2(pane.transform.position.x, pane.transform.position.z))) glass.Add(pane);
                SetArray(instance.GetComponent<SkeletonDormancy>(), "wakeOnBroken", glass.ToArray());
                Undo.RegisterCreatedObjectUndo(instance, "Place skeleton archer");
                EditorSceneManager.MarkSceneDirty(scene);
                Selection.activeGameObject = instance;
                Debug.Log("Skeleton archer placed as a bone pile in Natural History at " + position + ", woken by " + glass.Count + " glass panes. Save the scene.");
                return;
            }
            throw new InvalidOperationException("No clear Natural History position was found for the skeleton archer.");
        }

        private static SkeletonRegion RegionFor(string piece)
        {
            if (Array.IndexOf(HeadPieces, piece) >= 0) return SkeletonRegion.Head;
            if (Array.IndexOf(BowArmPieces, piece) >= 0) return SkeletonRegion.BowArm;
            if (Array.IndexOf(GearPieces, piece) >= 0) return SkeletonRegion.Gear;
            return SkeletonRegion.Body;
        }

        private static void ApplyMaterials(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = MaterialFor(materials[i].name);
                renderer.sharedMaterials = materials;
                if (renderer is SkinnedMeshRenderer skin) skin.quality = SkinQuality.Bone2;
            }
        }

        private static Material MaterialFor(string name)
        {
            if (!Palette.TryGetValue(name, out var spec)) throw new InvalidOperationException("Unknown skeleton material: " + name);
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is missing.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", spec.color.gamma);
            material.SetFloat("_Smoothness", spec.smoothness);
            material.SetFloat("_Metallic", spec.metallic);
            if (spec.emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", spec.color * spec.emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            // Fletching is a single quad, so render both faces.
            if (name == "Skeleton_Fletch") material.SetFloat("_Cull", (float)CullMode.Off);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material TrailMaterial()
        {
            string path = MaterialFolder + "/Skeleton_ArrowTrail.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("Sprites/Default shader is missing.");
            material = new Material(shader) { name = "Skeleton_ArrowTrail" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Generate " + name + ".wav with Blender/SkeletonArcher/generate_audio.py first.");
            return clip;
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException(target.GetType().Name + " has no field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException(target.GetType().Name + " has no field " + field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedProperties();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true)) if (child.name == name) return child;
            return null;
        }

        private static Bounds CombinedBounds(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(model.name + " has no renderers.");
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}

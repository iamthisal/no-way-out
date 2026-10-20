using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.Enemies;
using Lockdown.World;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    public static class MummyBuilder
    {
        public const string MummyModelPath = "Assets/_Project/Models/Mummy/Mummy.fbx";
        public const string JarModelPath = "Assets/_Project/Models/Mummy/CanopicJars.fbx";
        public const string SarcophagusModelPath = "Assets/_Project/Models/Mummy/Sarcophagi.fbx";
        public const string ClipTablePath = "Assets/_Project/Models/Mummy/Mummy.clips.json";
        public const string MummyPrefabPath = "Assets/_Project/Prefabs/Characters/Mummy.prefab";
        public const string PropFolder = "Assets/_Project/Prefabs/Props";
        public const string ControllerPath = "Assets/_Project/Animations/Mummy/Mummy.controller";
        private const string MaterialFolder = "Assets/_Project/Materials/Enemies/Mummy";
        private const string AudioFolder = "Assets/_Project/Audio/Mummy";
        private const string ScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";
        public const int AmuletValue = 750;
        // The four sons of Horus in binding order: mummy i, sarcophagus i, jar i and amulet i belong together.
        public static readonly string[] Gods = { "Imsety", "Hapi", "Duamutef", "Qebehsenuef" };
        // Distance from a sarcophagus's footprint centre to the spot its mummy walks out to (Blender: 0.95 - 0.03).
        public const float StepOut = 0.92f;
        // Sarcophagi stand with their backs to the Egypt gallery's side walls, one per corner. The south-east
        // corner is the D13 doorway, so that coffin stands on the east wall just north of the door.
        private static readonly (string god, Vector3 position, Vector3 facing)[] TombSpots =
        {
            ("Imsety", new Vector3(-10.05f, 0f, 14.62f), Vector3.right),
            ("Hapi", new Vector3(-10.05f, 0f, 21.38f), Vector3.right),
            ("Duamutef", new Vector3(-3.95f, 0f, 21.38f), Vector3.left),
            ("Qebehsenuef", new Vector3(-3.95f, 0f, 15.45f), Vector3.left),
        };
        // Decor that sat in those corners moves to clear wall space in the same gallery.
        private static readonly (string name, Vector2 from, Vector2 to)[] DecorMoves =
        {
            ("P_Egypt_Plant", new Vector2(-9.5f, 15.2f), new Vector2(-9.9f, 16.75f)),
            ("P_Egyptian_Column", new Vector2(-9.5f, 20.9f), new Vector2(-9.75f, 19.95f)),
            ("P_Egypt_CornerFeature", new Vector2(-4.5f, 21.3f), new Vector2(-4.2f, 18.0f)),
        };
        // One jar per gallery, spread around the museum.
        private static readonly (string god, string room, Rect bounds)[] JarRooms =
        {
            ("Imsety", "Stone Age", Rect.MinMaxRect(-10.5f, 22f, -3.5f, 30f)),
            ("Hapi", "Greece / Rome", Rect.MinMaxRect(-3.5f, 22f, 3.5f, 30f)),
            ("Duamutef", "Natural History", Rect.MinMaxRect(3.5f, 14f, 10.5f, 22f)),
            ("Qebehsenuef", "Art Gallery", Rect.MinMaxRect(-16f, -3f, -5f, 5f)),
        };
        // Route markers from the World Builder manual.
        private static readonly Dictionary<string, Vector3> Routes = new Dictionary<string, Vector3>
        {
            { "Egypt", new Vector3(-7f, 0f, 16f) }, { "StoneAge", new Vector3(-7f, 0f, 26f) }, { "GreeceRome", new Vector3(0f, 0f, 26f) },
            { "AsianHeritage", new Vector3(7f, 0f, 26f) }, { "NaturalHistory", new Vector3(5f, 0f, 18f) }, { "Exhibition", new Vector3(0f, 0f, 10f) },
            { "Temporary", new Vector3(-11f, 0f, 8f) }, { "Art", new Vector3(-10f, 0f, 1f) }, { "Central", new Vector3(0f, 0f, 0f) },
        };
        private static readonly string[][] PatrolLoops =
        {
            new[] { "Egypt", "StoneAge", "GreeceRome", "AsianHeritage", "NaturalHistory", "Exhibition" },
            new[] { "Egypt", "Exhibition", "NaturalHistory", "AsianHeritage", "GreeceRome", "StoneAge" },
            new[] { "Egypt", "Exhibition", "Temporary", "Art", "Central", "Exhibition" },
            new[] { "Egypt", "StoneAge", "Egypt", "Exhibition", "Central", "Art", "Temporary", "Exhibition" },
        };

        private static readonly Dictionary<string, (Color color, float smoothness, float metallic, float emission)> Palette =
            new Dictionary<string, (Color, float, float, float)>
            {
                { "Mummy_Linen", (new Color(0.46f, 0.40f, 0.28f), 0.12f, 0f, 0f) },
                { "Mummy_LinenDark", (new Color(0.26f, 0.21f, 0.14f), 0.1f, 0f, 0f) },
                { "Mummy_Resin", (new Color(0.03f, 0.022f, 0.015f), 0.35f, 0f, 0f) },
                { "Mummy_EyeGlow", (new Color(0.55f, 0.95f, 0.30f), 0.5f, 0f, 3f) },
                { "Mummy_Amulet", (new Color(0.06f, 0.45f, 0.42f), 0.6f, 0.1f, 0.8f) },
                { "Mummy_Gold", (new Color(0.75f, 0.55f, 0.15f), 0.6f, 0.8f, 0f) },
                { "Mummy_Teeth", (new Color(0.55f, 0.48f, 0.32f), 0.3f, 0f, 0f) },
                { "Mummy_Dust", (new Color(0.42f, 0.36f, 0.25f), 0.05f, 0f, 0f) },
                { "Jar_Alabaster", (new Color(0.72f, 0.66f, 0.54f), 0.55f, 0f, 0f) },
                { "Jar_Inner", (new Color(0.35f, 0.31f, 0.25f), 0.2f, 0f, 0f) },
                { "Jar_Dark", (new Color(0.04f, 0.04f, 0.06f), 0.45f, 0f, 0f) },
                { "Jar_Gold", (new Color(0.78f, 0.56f, 0.16f), 0.6f, 0.7f, 0f) },
                { "Jar_Skin", (new Color(0.42f, 0.24f, 0.12f), 0.35f, 0f, 0f) },
                { "Jar_Fur", (new Color(0.30f, 0.30f, 0.24f), 0.12f, 0f, 0f) },
                { "Jar_Black", (new Color(0.03f, 0.03f, 0.035f), 0.5f, 0f, 0f) },
                { "Jar_Feather", (new Color(0.30f, 0.18f, 0.08f), 0.18f, 0f, 0f) },
                { "Jar_Wood", (new Color(0.10f, 0.06f, 0.035f), 0.35f, 0f, 0f) },
                { "Jar_Brass", (new Color(0.40f, 0.31f, 0.16f), 0.45f, 0.5f, 0f) },
                { "Jar_Imsety", (new Color(0.06f, 0.45f, 0.42f), 0.6f, 0f, 0f) },
                { "Jar_Hapi", (new Color(0.04f, 0.10f, 0.45f), 0.6f, 0f, 0f) },
                { "Jar_Duamutef", (new Color(0.50f, 0.08f, 0.04f), 0.6f, 0f, 0f) },
                { "Jar_Qebehsenuef", (new Color(0.75f, 0.50f, 0.10f), 0.6f, 0f, 0f) },
                { "Sarc_Wood", (new Color(0.07f, 0.045f, 0.03f), 0.35f, 0f, 0f) },
                { "Sarc_Inner", (new Color(0.16f, 0.05f, 0.03f), 0.1f, 0f, 0f) },
                { "Sarc_Gold", (new Color(0.80f, 0.58f, 0.18f), 0.65f, 0.85f, 0f) },
            };

        [Serializable] private sealed class ClipTable { public int fps; public ClipRow[] clips; }
        [Serializable] private sealed class ClipRow { public string name; public int first; public int last; public bool loop; }

        [MenuItem("Tools/Lockdown/Build Mummies and Canopic Jars")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the mummies.");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Animations/Mummy");
            EnsureFolder(PropFolder);
            var clips = ConfigureMummyImport();
            ConfigureStaticImport(JarModelPath);
            ConfigureStaticImport(SarcophagusModelPath);
            var controller = BuildController(clips);
            BuildMummyPrefab(controller);
            foreach (var god in Gods)
            {
                BuildJarPrefab(god);
                BuildSarcophagusPrefab(god);
                BuildAmuletPrefab(god);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Mummy prefab (" + clips.Count + " clips), four canopic jars, sarcophagi and amulet pickups built.");
        }

        private static Dictionary<string, AnimationClip> ConfigureMummyImport()
        {
            var importer = AssetImporter.GetAtPath(MummyModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate Mummy.fbx with Blender/Mummy/build_mummy.py first.");
            var table = JsonUtility.FromJson<ClipTable>(File.ReadAllText(ClipTablePath));
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            var defaults = importer.defaultClipAnimations;
            if (defaults.Length == 0) throw new InvalidOperationException("The mummy FBX must contain its animation take.");
            float offset = defaults[0].firstFrame - table.clips[0].first;
            importer.clipAnimations = table.clips.Select(row => new ModelImporterClipAnimation
            {
                name = row.name, takeName = defaults[0].takeName, firstFrame = row.first + offset, lastFrame = row.last + offset,
                loopTime = row.loop, loopPose = false, wrapMode = row.loop ? WrapMode.Loop : WrapMode.ClampForever,
                lockRootRotation = true, lockRootHeightY = true, lockRootPositionXZ = true,
                keepOriginalOrientation = true, keepOriginalPositionY = true, keepOriginalPositionXZ = true
            }).ToArray();
            importer.SaveAndReimport();
            var result = new Dictionary<string, AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(MummyModelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) result[clip.name] = clip;
            foreach (var row in table.clips)
                if (!result.ContainsKey(row.name)) throw new InvalidOperationException("Imported clip missing: " + row.name);
            return result;
        }

        private static void ConfigureStaticImport(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Generate " + Path.GetFileName(path) + " with the Blender script first.");
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.globalScale = 1f;
            importer.SaveAndReimport();
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Shamble"], 1f);
            tree.AddChild(clips["Chase"], 2.9f);
            var machine = controller.layers[0].stateMachine;
            // The body and agent change states from code with cross-fades.
            foreach (var name in new[] { "Entombed", "WalkOut", "Bite", "Hit", "Fall", "Down", "Rise" }) machine.AddState(name).motion = clips[name];
            machine.defaultState = locomotion;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void BuildMummyPrefab(AnimatorController controller)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MummyModelPath);
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Mummy");
                SceneManager.MoveGameObjectToScene(root, preview);
                // The clips animate the imported root itself, so turning and grounding live on a parent pivot.
                var facing = new GameObject("Facing").transform;
                facing.SetParent(root.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                model.name = "Model";
                model.transform.SetParent(facing, false);
                ApplyMaterials(model);
                var foot = Find(model.transform, "Piece_Foot_L");
                var ankle = Find(model.transform, "Foot_L");
                if (foot == null || ankle == null) throw new InvalidOperationException("The mummy's foot piece or ankle bone is missing.");
                if (foot.GetComponent<Renderer>().bounds.center.z < ankle.position.z) facing.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var bounds = CombinedBounds(model);
                if (bounds.size.y < 1.5f || bounds.size.y > 2.2f) throw new InvalidOperationException("Unexpected mummy import scale: " + bounds.size);
                facing.localPosition = Vector3.up * -bounds.min.y;
                var animator = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var navAgent = root.AddComponent<NavMeshAgent>();
                navAgent.radius = 0.32f;
                navAgent.height = 1.8f;
                navAgent.speed = 1f;
                navAgent.angularSpeed = 360f;
                navAgent.acceleration = 8f;
                navAgent.stoppingDistance = 0.05f;
                navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.radius = 0.3f;
                capsule.height = 1.8f;
                var voice = root.AddComponent<AudioSource>();
                Spatial(voice, 2f, 22f);
                var shuffleObject = new GameObject("Shuffle");
                shuffleObject.transform.SetParent(root.transform, false);
                var shuffle = shuffleObject.AddComponent<AudioSource>();
                Spatial(shuffle, 1.5f, 14f);
                shuffle.clip = Clip("MummyShuffleLoop");
                shuffle.loop = true;
                var dust = CreateBurst(root.transform, "Dust", new Vector3(0f, 0.6f, -0.2f), new Vector3(0.6f, 1.0f, 1.4f), 80,
                    MaterialFor("Mummy_Dust"), 0.15f, 0.04f, 0.12f);

                var pieces = model.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Piece_")).ToArray();
                if (pieces.Length != 21) throw new InvalidOperationException("Expected 21 mummy pieces, found " + pieces.Length);
                var body = root.AddComponent<MummyBody>();
                Set(body, "animator", animator);
                Set(body, "bodyCollider", capsule);
                Set(body, "amulet", pieces.Single(p => p.name == "Piece_Amulet"));
                SetArray(body, "pieces", pieces);
                Set(body, "dust", dust);
                Set(body, "voice", voice);
                Set(body, "groanSound", Clip("MummyGroan"));
                Set(body, "riseSound", Clip("MummyRise"));
                Set(body, "crumbleSound", Clip("MummyCrumble"));
                var agent = root.AddComponent<MummyAgent>();
                Set(agent, "animator", animator);
                Set(agent, "voice", voice);
                Set(agent, "shuffle", shuffle);
                Set(agent, "groanSound", Clip("MummyGroan"));
                Set(agent, "biteSound", Clip("MummyBite"));
                PrefabUtility.SaveAsPrefabAsset(root, MummyPrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // Re-roots one FBX group under a clean transform: footprint centred on the origin, standing on the floor.
        private static GameObject Reroot(string modelPath, string group, string name, Scene preview, Func<Transform, bool> footprint, out Transform[] parts)
        {
            var imported = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), preview);
            PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var node = Find(imported.transform, group) ?? throw new InvalidOperationException(group + " is missing from " + Path.GetFileName(modelPath));
            parts = node.Cast<Transform>().ToArray();
            var basis = parts.Where(footprint).Select(p => p.GetComponent<Renderer>().bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            Vector3 shift = new Vector3(basis.center.x, basis.min.y, basis.center.z);
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, preview);
            foreach (var part in parts)
            {
                part.SetParent(root.transform, true);
                part.position -= shift;
            }
            Object.DestroyImmediate(imported);
            ApplyMaterials(root);
            return root;
        }

        private static void BuildJarPrefab(string god)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = Reroot(JarModelPath, "Jar_" + god, "CanopicJar_" + god, preview, p => p.name.StartsWith("Plinth"), out var parts);
                var plinth = parts.First(p => p.name.StartsWith("Plinth"));
                plinth.name = "Plinth";
                var baseBounds = parts.First(p => p.name.StartsWith("Base")).GetComponent<Renderer>().bounds;
                // The breakable jar gets a clean, unscaled transform at its base; the stand stays separate.
                var jar = new GameObject("Jar");
                jar.transform.SetParent(root.transform, false);
                jar.transform.localPosition = new Vector3(baseBounds.center.x, baseBounds.min.y, baseBounds.center.z);
                var fragments = new List<Rigidbody>();
                foreach (var part in parts.Where(p => p != plinth))
                {
                    part.SetParent(jar.transform, true);
                    part.gameObject.AddComponent<BoxCollider>().enabled = false;
                    var body = part.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.mass = part.name.StartsWith("Lid") ? 0.3f : 0.12f;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    fragments.Add(body);
                }
                plinth.gameObject.AddComponent<BoxCollider>();
                Carve(plinth.gameObject);
                var hitbox = jar.AddComponent<CapsuleCollider>();
                hitbox.center = new Vector3(0f, 0.25f, 0f);
                hitbox.radius = 0.14f;
                hitbox.height = 0.52f;
                var spirit = CreateBurst(jar.transform, "Spirit", new Vector3(0f, 0.3f, 0f), new Vector3(0.2f, 0.3f, 0.2f), 50,
                    MaterialFor("Jar_" + god), -0.35f, 0.04f, 0.1f);
                var canopic = jar.AddComponent<CanopicJar>();
                var serialized = new SerializedObject(canopic);
                serialized.FindProperty("godName").stringValue = god;
                serialized.FindProperty("glaze").colorValue = Palette["Jar_" + god].color.gamma;
                serialized.FindProperty("maxHealth").floatValue = 40f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SetArray(canopic, "fragments", fragments.ToArray());
                Set(canopic, "intactCollider", hitbox);
                Set(canopic, "spirit", spirit);
                Set(canopic, "shatterSound", Clip("JarShatter"));
                Set(canopic, "curseSound", Clip("CurseBreak"));
                Set(canopic, "sealedSound", Clip("JarSealed"));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("CanopicJar_", god));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void BuildSarcophagusPrefab(string god)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = Reroot(SarcophagusModelPath, "Sarcophagus_" + god, "Sarcophagus_" + god, preview, p => true, out var parts);
                var caseShell = parts.First(p => p.name.StartsWith("SarcCase"));
                var lid = parts.First(p => p.name.StartsWith("SarcLid"));
                caseShell.name = "Case";
                lid.name = "Lid";
                // The prefab's +Z is the coffin's front: turn it round if the lid came in behind the case.
                if (lid.GetComponent<Renderer>().bounds.center.z < caseShell.GetComponent<Renderer>().bounds.center.z)
                    foreach (var part in parts) part.RotateAround(Vector3.zero, Vector3.up, 180f);
                caseShell.gameObject.AddComponent<MeshCollider>();
                Carve(caseShell.gameObject);
                lid.gameObject.AddComponent<BoxCollider>();
                var grind = root.AddComponent<AudioSource>();
                Spatial(grind, 2f, 18f);
                grind.clip = Clip("SarcophagusOpen");
                var dust = CreateBurst(root.transform, "Dust", new Vector3(0f, 1.1f, 0.4f), new Vector3(0.8f, 2f, 0.3f), 40,
                    MaterialFor("Mummy_Dust"), 0.1f, 0.03f, 0.09f);
                var sarcophagus = root.AddComponent<Sarcophagus>();
                var serialized = new SerializedObject(sarcophagus);
                serialized.FindProperty("godName").stringValue = god;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Set(sarcophagus, "lid", lid);
                Set(sarcophagus, "grind", grind);
                Set(sarcophagus, "dust", dust);
                Set(lid.gameObject.AddComponent<SarcophagusLid>(), "sarcophagus", sarcophagus);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("Sarcophagus_", god));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // The scarab from the mummy's chest, lying face up as a loot pickup.
        private static void BuildAmuletPrefab(string god)
        {
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var mummy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MummyPrefabPath), preview);
                var piece = Find(mummy.transform, "Piece_Amulet");
                var chest = Find(mummy.transform, "Piece_Torso").GetComponent<Renderer>().bounds.center;
                Vector3 outward = piece.GetComponent<Renderer>().bounds.center - chest;
                outward.y = 0f;
                var root = new GameObject("Amulet_" + god);
                SceneManager.MoveGameObjectToScene(root, preview);
                var model = new GameObject("Model");
                model.transform.SetParent(root.transform, false);
                model.AddComponent<MeshFilter>().sharedMesh = piece.GetComponent<MeshFilter>().sharedMesh;
                var renderer = model.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = piece.GetComponent<MeshRenderer>().sharedMaterials
                    .Select(m => m.name.Contains("Amulet") ? AmuletMaterial(god) : m).ToArray();
                model.transform.rotation = Quaternion.FromToRotation(outward.normalized, Vector3.up) * piece.rotation;
                model.transform.localScale = piece.lossyScale;
                model.transform.position -= renderer.bounds.center;
                Object.DestroyImmediate(mummy);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = renderer.bounds.center;
                collider.size = Vector3.Max(renderer.bounds.size, new Vector3(0.12f, 0.05f, 0.12f));
                var body = root.AddComponent<Rigidbody>();
                body.mass = 0.2f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var loot = root.AddComponent<LootItem>();
                var serializedLoot = new SerializedObject(loot);
                serializedLoot.FindProperty("itemName").stringValue = "Amulet of " + god;
                serializedLoot.FindProperty("value").intValue = AmuletValue;
                serializedLoot.ApplyModifiedPropertiesWithoutUndo();
                var marker = new SerializedObject(root.AddComponent<MummyAmulet>());
                marker.FindProperty("godName").stringValue = god;
                marker.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath("Amulet_", god));
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        public static string PrefabPath(string kind, string god) => PropFolder + "/" + kind + god + ".prefab";

        [MenuItem("Tools/Lockdown/Place Mummies and Canopic Jars")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == "EgyptCurse") { Selection.activeGameObject = existing; return; }
            var mummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MummyPrefabPath);
            if (mummyPrefab == null) throw new InvalidOperationException("Build the mummies and canopic jars first.");
            MoveCornerDecor(scene);
            Physics.SyncTransforms();

            var jarsRoot = new GameObject("CanopicJars");
            SceneManager.MoveGameObjectToScene(jarsRoot, scene);
            Undo.RegisterCreatedObjectUndo(jarsRoot, "Place canopic jars");
            var jars = new CanopicJar[Gods.Length];
            foreach (var (god, room, bounds) in JarRooms)
            {
                if (!TryFindWallSpot(bounds, out var position, out var facing))
                    throw new InvalidOperationException("No clear wall position for the " + god + " jar in " + room + ".");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("CanopicJar_", god)), scene);
                instance.transform.SetParent(jarsRoot.transform, true);
                instance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(facing));
                Physics.SyncTransforms();
                jars[Array.IndexOf(Gods, god)] = instance.GetComponentInChildren<CanopicJar>();
                Debug.Log(god + " jar placed in " + room + " at " + position);
            }

            var curseRoot = new GameObject("EgyptCurse");
            SceneManager.MoveGameObjectToScene(curseRoot, scene);
            curseRoot.transform.position = new Vector3(-7f, 0f, 18f);
            Undo.RegisterCreatedObjectUndo(curseRoot, "Place sarcophagi and mummies");
            var bodies = new List<MummyBody>();
            for (int i = 0; i < Gods.Length; i++)
            {
                var (god, position, facing) = TombSpots[i];
                var rotation = Quaternion.LookRotation(facing);
                if (Physics.CheckBox(position + Vector3.up * 1.2f, new Vector3(0.4f, 1f, 0.33f), rotation, ~0, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Something is in the way of the " + god + " sarcophagus at " + position + ".");
                Vector3 exit = position + facing * StepOut;
                if (!NavMesh.SamplePosition(exit, out var floor, 0.6f, NavMesh.AllAreas))
                    throw new InvalidOperationException("The " + god + " mummy has no walkable floor in front of its sarcophagus.");
                var sarcophagus = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Sarcophagus_", god)), scene);
                sarcophagus.transform.SetParent(curseRoot.transform, true);
                sarcophagus.transform.SetPositionAndRotation(position, rotation);
                var mummy = (GameObject)PrefabUtility.InstantiatePrefab(mummyPrefab, scene);
                mummy.name = "Mummy_" + god;
                mummy.transform.SetParent(curseRoot.transform, true);
                mummy.transform.SetPositionAndRotation(new Vector3(exit.x, floor.position.y, exit.z), rotation);
                var body = mummy.GetComponent<MummyBody>();
                Set(body, "boundJar", jars[i]);
                Set(body, "amuletDrop", AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath("Amulet_", god)));
                Set(sarcophagus.GetComponent<Sarcophagus>(), "mummy", body);
                var agent = new SerializedObject(mummy.GetComponent<MummyAgent>());
                agent.FindProperty("lane").intValue = i;
                var route = agent.FindProperty("patrolRoute");
                var points = PatrolLoops[i].Select(Snap).ToArray();
                route.arraySize = points.Length;
                for (int k = 0; k < points.Length; k++) route.GetArrayElementAtIndex(k).vector3Value = points[k];
                agent.ApplyModifiedProperties();
                bodies.Add(body);
                Physics.SyncTransforms();
            }
            SetArray(curseRoot.AddComponent<MummyCurse>(), "mummies", bodies.ToArray());
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = curseRoot;
            Debug.Log("Four sarcophagi placed in the Egypt gallery corners, each holding a mummy bound to its canopic jar. Save the scene.");
        }

        private static void MoveCornerDecor(Scene scene)
        {
            foreach (var (name, from, to) in DecorMoves)
            {
                var prop = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t => t.name == name && Vector2.Distance(new Vector2(t.position.x, t.position.z), from) < 0.3f);
                if (prop == null) continue;
                Undo.RecordObject(prop, "Clear Egypt corner");
                var before = prop.position;
                prop.position = new Vector3(to.x, before.y, to.y);
                Debug.Log("Moved " + name + " from " + before + " to " + prop.position + " to clear an Egypt gallery corner for a sarcophagus.");
            }
        }

        private static void Carve(GameObject target)
        {
            var obstacle = target.AddComponent<NavMeshObstacle>();
            var mesh = target.GetComponent<MeshFilter>().sharedMesh;
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = mesh.bounds.center;
            obstacle.size = mesh.bounds.size * 1.02f;
            obstacle.carving = true;
        }

        // A spot against a wall (not a doorway), facing into the room, with walking space in front.
        private static bool TryFindWallSpot(Rect room, out Vector3 position, out Vector3 facing)
        {
            var options = new List<(float score, Vector3 position, Vector3 facing)>();
            var walls = new[] { (Vector3.left, room.xMin), (Vector3.right, room.xMax), (Vector3.back, room.yMin), (Vector3.forward, room.yMax) };
            foreach (var (outward, line) in walls)
            {
                bool alongX = outward.z != 0f;
                float from = alongX ? room.xMin : room.yMin, to = alongX ? room.xMax : room.yMax;
                for (float s = from + 0.8f; s <= to - 0.8f; s += 0.3f)
                {
                    float inset = line - (alongX ? outward.z : outward.x) * 0.45f;
                    var spot = alongX ? new Vector3(s, 0f, inset) : new Vector3(inset, 0f, s);
                    if (Physics.CheckBox(spot + Vector3.up * 0.5f, new Vector3(0.26f, 0.42f, 0.26f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    // A wall directly behind: doorways and windows are rejected.
                    if (!Physics.Raycast(spot + Vector3.up * 1.2f, outward, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    var front = spot - outward * 0.8f;
                    if (Physics.CheckBox(front + Vector3.up * 0.6f, new Vector3(0.3f, 0.45f, 0.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!NavMesh.SamplePosition(front, out _, 0.4f, NavMesh.AllAreas)) continue;
                    options.Add((Mathf.Abs(s - (from + to) * 0.5f), spot, -outward));
                }
            }
            if (options.Count == 0) { position = default; facing = default; return false; }
            var best = options.OrderBy(o => o.score).First();
            position = best.position;
            facing = best.facing;
            return true;
        }

        private static Vector3 Snap(string route)
        {
            var point = Routes[route];
            return NavMesh.SamplePosition(point, out var hit, 2.5f, NavMesh.AllAreas) ? hit.position : point;
        }

        private static ParticleSystem CreateBurst(Transform parent, string name, Vector3 position, Vector3 box, int count,
            Material material, float gravity, float minSize, float maxSize)
        {
            var effect = new GameObject(name);
            effect.transform.SetParent(parent, false);
            effect.transform.localPosition = position;
            var particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.gravityModifier = gravity;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count, 1, 0.01f) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return particles;
        }

        private static void Spatial(AudioSource source, float min, float max)
        {
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = min;
            source.maxDistance = max;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }

        private static void ApplyMaterials(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = MaterialFor(materials[i].name);
                renderer.sharedMaterials = materials;
            }
        }

        private static Material AmuletMaterial(string god)
        {
            string path = MaterialFolder + "/Amulet_" + god + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var color = Palette["Jar_" + god].color;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Amulet_" + god };
            material.SetColor("_BaseColor", color.gamma);
            material.SetFloat("_Smoothness", 0.7f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.6f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material MaterialFor(string name)
        {
            if (!Palette.TryGetValue(name, out var spec)) throw new InvalidOperationException("Unknown mummy material: " + name);
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader is missing.");
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
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Generate " + name + ".wav with Blender/Mummy/generate_audio.py first.");
            return clip;
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException(target.GetType().Name + " has no field " + field);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
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
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}

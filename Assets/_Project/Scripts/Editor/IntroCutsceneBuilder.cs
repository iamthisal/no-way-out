using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lockdown.Intro;
using Lockdown.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    /// <summary>
    /// Builds the break-in cutscene: model import, materials, the outdoor set, the Timeline and its place in the
    /// museum. The actors are animated in museum coordinates (Blender/Intro/intro_layout.py). Safe to run again.
    /// </summary>
    public static class IntroCutsceneBuilder
    {
        private const string Models = "Assets/_Project/Models/Intro/", Materials = "Assets/_Project/Materials/Intro/";
        private const string Textures = "Assets/_Project/Textures/Intro/", Animations = "Assets/_Project/Animations/Intro/";
        private const string PrefabPath = "Assets/_Project/Prefabs/Intro/IntroCutscene.prefab";
        private const string TimelinePath = Animations + "IntroTimeline.playable", CameraClipPath = Animations + "IntroCamera.anim";
        private const string SoundPath = "Assets/_Project/Audio/Intro/IntroSoundtrack.wav";
        private const string TreesPath = "Assets/_Project/Models/Props/Courtyard/low-poly-trees/source/Trees.fbx";
        private const string MuseumPath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";

        public const float Length = 28.6f;
        public const float ThiefAppears = 4.4f, LadderRaised = 9f;
        /// <summary>Behind the flower pot in the courtyard, facing the locked glass door.</summary>
        public static readonly Vector3 PlayerStart = new Vector3(1.6f, 0.05f, 18.75f);
        public const float PlayerStartYaw = 180f;

        private static readonly string[] Actors = { "MuseumThief", "HeistVan", "HeistLadder" };

        // ---------------------------------------------------------------------------------------- models

        [MenuItem("Tools/Lockdown/Intro/1 Import Intro Models")]
        public static void ImportModels()
        {
            foreach (var folder in new[] { Materials, Textures, Animations, "Assets/_Project/Prefabs/Intro/" }) Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            CreateMaterials();
            foreach (var actor in Actors)
            {
                string path = Models + actor + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (importer == null) throw new FileNotFoundException(path);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.importCameras = importer.importLights = false;
                importer.addCollider = false;
                // Contacts are exact in the file, so nothing may be simplified away.
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.resampleCurves = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                foreach (var name in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(material => material.name).Distinct())
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), LoadMaterial(name));
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips) { clip.name = actor + "_Intro"; clip.loopTime = false; clip.loopPose = false; }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            var sound = (AudioImporter)AssetImporter.GetAtPath(SoundPath);
            var settings = sound.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            sound.defaultSampleSettings = settings;
            sound.SaveAndReimport();
        }

        private struct Look
        {
            public string Name; public Color Color; public float Metallic, Smoothness, Glow; public string Texture; public Vector2 Tiling;
            public Look(string name, float r, float g, float b, float smoothness = 0.2f, float metallic = 0f, float glow = 0f, string texture = null, float tile = 1f, float tileY = 0f)
            {
                Name = name; Color = new Color(r, g, b); Smoothness = smoothness; Metallic = metallic; Glow = glow; Texture = texture;
                Tiling = new Vector2(tile, tileY > 0f ? tileY : tile);
            }
        }

        private static readonly Look[] Looks =
        {
            new Look("Thief_Jacket", 0.15f, 0.18f, 0.23f), new Look("Thief_Trousers", 0.17f, 0.17f, 0.18f), new Look("Thief_Mask", 0.09f, 0.09f, 0.10f),
            new Look("Thief_Skin", 0.72f, 0.52f, 0.41f, 0.3f), new Look("Thief_Gloves", 0.12f, 0.11f, 0.10f, 0.35f), new Look("Thief_Boots", 0.15f, 0.12f, 0.10f, 0.35f),
            new Look("Thief_Bag", 0.43f, 0.34f, 0.23f), new Look("Thief_Strap", 0.27f, 0.21f, 0.14f), new Look("Thief_Eye", 0.9f, 0.9f, 0.88f, 0.6f),
            new Look("Thief_Pupil", 0.06f, 0.06f, 0.06f, 0.5f), new Look("Thief_Metal", 0.62f, 0.61f, 0.58f, 0.6f, 0.8f),
            new Look("Van_Paint", 0.24f, 0.29f, 0.34f, 0.6f, 0.25f), new Look("Van_Trim", 0.09f, 0.09f, 0.10f, 0.3f), new Look("Van_Glass", 0.04f, 0.05f, 0.06f, 0.92f),
            new Look("Van_Tyre", 0.07f, 0.07f, 0.07f, 0.1f), new Look("Van_Rim", 0.55f, 0.55f, 0.56f, 0.6f, 0.8f), new Look("Van_Lamp", 1f, 0.95f, 0.8f, 0.8f, 0f, 1.2f),
            new Look("Van_Tail", 0.6f, 0.05f, 0.05f, 0.7f, 0f, 0.4f), new Look("Van_Interior", 0.2f, 0.19f, 0.18f), new Look("Van_Plate", 0.8f, 0.72f, 0.3f),
            new Look("Ladder_Metal", 0.72f, 0.73f, 0.74f, 0.55f, 0.7f), new Look("Ladder_Foot", 0.12f, 0.12f, 0.12f),
            new Look("Intro_Grass", 0.36f, 0.44f, 0.22f, 0.05f, 0f, 0f, "Noise_Grass", 45f), new Look("Intro_Asphalt", 0.30f, 0.30f, 0.31f, 0.12f, 0f, 0f, "Noise_Fine", 40f, 3f),
            new Look("Intro_Paving", 0.60f, 0.58f, 0.54f, 0.1f, 0f, 0f, "Noise_Paving", 24f, 1.2f), new Look("Intro_Kerb", 0.66f, 0.65f, 0.62f), new Look("Intro_Line", 0.85f, 0.83f, 0.76f),
            new Look("Intro_Roof", 0.40f, 0.40f, 0.41f, 0.05f, 0f, 0f, "Noise_Fine", 3f), new Look("Intro_Duct", 0.60f, 0.62f, 0.63f, 0.5f, 0.6f), new Look("Intro_Vent", 0.16f, 0.17f, 0.18f, 0.2f),
            new Look("Intro_Leaves", 0.25f, 0.42f, 0.17f, 0.05f), new Look("Intro_Trunk", 0.33f, 0.24f, 0.17f, 0.05f),
        };

        private static void CreateMaterials()
        {
            NoiseTexture("Noise_Fine", 40f, 0.28f, 11);
            NoiseTexture("Noise_Grass", 14f, 0.45f, 23);
            NoiseTexture("Noise_Paving", 30f, 0.16f, 37, 4);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var look in Looks)
            {
                string path = Materials + look.Name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.SetColor("_BaseColor", look.Color);
                material.SetFloat("_Metallic", look.Metallic);
                material.SetFloat("_Smoothness", look.Smoothness);
                if (look.Texture != null)
                {
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + look.Texture + ".png"));
                    material.SetTextureScale("_BaseMap", look.Tiling);
                }
                if (look.Glow > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    material.SetColor("_EmissionColor", look.Color.linear * look.Glow);
                }
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>A small tiling grey noise, optionally with slab joints, that tints a base colour.</summary>
        private static void NoiseTexture(string name, float frequency, float depth, int seed, int slabs = 0)
        {
            string path = Textures + name + ".png";
            if (File.Exists(path)) return;
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size, value = 0f;
                    // Blending four shifted copies makes the noise tile.
                    for (int octave = 0; octave < 3; octave++)
                    {
                        float f = frequency * (1 << octave) / 4f;
                        float a = Mathf.PerlinNoise(seed + u * f, seed + v * f), b = Mathf.PerlinNoise(seed + (u - 1f) * f, seed + v * f);
                        float c = Mathf.PerlinNoise(seed + u * f, seed + (v - 1f) * f), d = Mathf.PerlinNoise(seed + (u - 1f) * f, seed + (v - 1f) * f);
                        value += Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v) / (1 << octave);
                    }
                    float shade = 1f - depth * Mathf.Clamp01(value / 1.75f * 1.6f - 0.3f);
                    if (slabs > 0 && ((x * slabs) % size < slabs * 2 || (y * slabs) % size < slabs * 2)) shade *= 0.72f;
                    texture.SetPixel(x, y, new Color(shade, shade, shade));
                }
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        private static Material LoadMaterial(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (material == null) throw new FileNotFoundException("No intro material for " + name);
            return material;
        }

        private static AnimationClip LoadClip(string actor) => AssetDatabase.LoadAllAssetsAtPath(Models + actor + ".fbx").OfType<AnimationClip>()
            .First(clip => !clip.name.StartsWith("__preview", StringComparison.Ordinal));

        // ---------------------------------------------------------------------------------------- prefab

        [MenuItem("Tools/Lockdown/Intro/2 Build Intro Cutscene")]
        public static void BuildPrefab()
        {
            if (SceneManager.GetActiveScene().path != MuseumPath) throw new InvalidOperationException("Open Museum_Main first: the set follows its roofs.");
            var root = new GameObject("IntroCutscene");
            try
            {
                var stage = new GameObject("Stage");
                stage.transform.SetParent(root.transform, false);
                var thief = AddActor("MuseumThief", stage.transform);
                var van = AddActor("HeistVan", stage.transform);
                var ladder = AddActor("HeistLadder", stage.transform);
                BuildSet(new GameObject("Set").transform, stage.transform);

                var rig = new GameObject("CameraRig").transform;
                rig.SetParent(stage.transform, false);
                var rigAnimator = rig.gameObject.AddComponent<Animator>();
                var lens = new GameObject("Camera");
                lens.transform.SetParent(rig, false);
                var camera = lens.AddComponent<Camera>();
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 220f;
                lens.AddComponent<AudioListener>();
                var sound = stage.AddComponent<AudioSource>();
                sound.playOnAwake = false;
                sound.spatialBlend = 0f;

                var director = stage.AddComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.extrapolationMode = DirectorWrapMode.Hold;
                var cameraClip = BuildCamera(thief);
                BuildTimeline(director, thief, van, ladder, rigAnimator, cameraClip, sound);

                var cutscene = root.AddComponent<IntroCutscene>();
                var data = new SerializedObject(cutscene);
                data.FindProperty("stage").objectReferenceValue = stage;
                data.FindProperty("director").objectReferenceValue = director;
                data.ApplyModifiedPropertiesWithoutUndo();
                stage.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        private static Animator AddActor(string name, Transform parent)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx"));
            model.name = name;
            model.transform.SetParent(parent, false);
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            // The bones travel through the museum, so bounds taken at the origin would be culled.
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.updateWhenOffscreen = true;
            return animator;
        }

        // ---------------------------------------------------------------------------------------- set

        private static void BuildSet(Transform set, Transform stage)
        {
            set.SetParent(stage, false);
            Slab(set, "Grass", "Intro_Grass", new Vector3(0f, -0.06f, 25f), new Vector3(240f, 0.1f, 240f));
            Slab(set, "Paving", "Intro_Paving", new Vector3(0f, -0.045f, 31.75f), new Vector3(60f, 0.09f, 3.3f));
            Slab(set, "Kerb", "Intro_Kerb", new Vector3(0f, -0.02f, 33.47f), new Vector3(60f, 0.06f, 0.16f));
            Slab(set, "Road", "Intro_Asphalt", new Vector3(0f, -0.05f, 36.6f), new Vector3(140f, 0.09f, 6.2f));
            for (int i = -10; i <= 10; i++) Slab(set, "RoadLine", "Intro_Line", new Vector3(i * 5f, -0.004f, 37.2f), new Vector3(2.2f, 0.01f, 0.12f));
            var ceilings = GameObject.Find("Museum").transform.Find("Ceilings");
            foreach (Transform ceiling in ceilings)
            {
                var bounds = ceiling.GetComponent<Renderer>().bounds;
                // Roofing felt stops short of each edge, leaving the pale slab visible as a parapet cap.
                Slab(set, "Roof_" + ceiling.name.Replace("Ceiling_", ""), "Intro_Roof", new Vector3(bounds.center.x, bounds.max.y + 0.004f, bounds.center.z),
                    new Vector3(bounds.size.x - 0.5f, 0.012f, bounds.size.z - 0.5f));
            }
            RoofUnit(set, new Vector3(-6.6f, 0f, 26.2f), 1.5f, 0.8f);
            RoofUnit(set, new Vector3(6.4f, 0f, 24.6f), 1.2f, 0.65f);
            RoofUnit(set, new Vector3(-6.2f, 0f, 17.6f), 1.2f, 0.65f);
            RoofUnit(set, new Vector3(6.8f, 0f, 17.0f), 1.6f, 0.85f);
            RoofUnit(set, new Vector3(1.5f, 0f, 8.5f), 1.7f, 0.9f);
            Slab(set, "Duct", "Intro_Duct", new Vector3(-8.6f, 4.05f, 22.5f), new Vector3(0.35f, 0.35f, 9f));
            Slab(set, "Duct", "Intro_Duct", new Vector3(8.9f, 4.05f, 20.5f), new Vector3(0.35f, 0.35f, 8f));
            foreach (var at in new[] { new Vector3(-2.6f, 0f, 27.6f), new Vector3(3.4f, 0f, 27.2f), new Vector3(-3.2f, 0f, 24.0f) }) Pipe(set, at);
            Trees(set);
            foreach (var part in set.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 0;
        }

        private static GameObject Slab(Transform parent, string name, string material, Vector3 centre, Vector3 size, PrimitiveType shape = PrimitiveType.Cube)
        {
            var slab = GameObject.CreatePrimitive(shape);
            slab.name = name;
            // Scenery only: the intro must never add anything to walk on or bake.
            Object.DestroyImmediate(slab.GetComponent<Collider>());
            slab.transform.SetParent(parent, false);
            slab.transform.position = centre;
            slab.transform.localScale = size;
            slab.GetComponent<MeshRenderer>().sharedMaterial = LoadMaterial(material);
            return slab;
        }

        private static void RoofUnit(Transform parent, Vector3 at, float width, float height)
        {
            float roof = 3.82f;
            Slab(parent, "RoofUnit", "Intro_Duct", new Vector3(at.x, roof + height * 0.5f + 0.1f, at.z), new Vector3(width, height, width * 0.75f));
            Slab(parent, "RoofUnitBase", "Intro_Vent", new Vector3(at.x, roof + 0.05f, at.z), new Vector3(width * 0.85f, 0.1f, width * 0.62f));
            for (int i = 0; i < 5; i++)
                Slab(parent, "RoofUnitLouvre", "Intro_Vent", new Vector3(at.x, roof + 0.22f + i * height * 0.15f, at.z + width * 0.38f), new Vector3(width * 0.8f, height * 0.06f, 0.03f));
            Slab(parent, "RoofUnitFan", "Intro_Vent", new Vector3(at.x, roof + height + 0.12f, at.z), new Vector3(width * 0.5f, 0.03f, width * 0.5f), PrimitiveType.Cylinder);
        }

        private static void Pipe(Transform parent, Vector3 at)
        {
            Slab(parent, "Pipe", "Intro_Duct", new Vector3(at.x, 4.02f, at.z), new Vector3(0.16f, 0.22f, 0.16f), PrimitiveType.Cylinder);
            Slab(parent, "PipeCap", "Intro_Vent", new Vector3(at.x, 4.27f, at.z), new Vector3(0.28f, 0.03f, 0.28f), PrimitiveType.Cylinder);
        }

        private static void Trees(Transform parent)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(TreesPath).OfType<Mesh>().ToDictionary(mesh => mesh.name);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TreesPath);
            var sources = model.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.name == "tree1" || filter.name == "tree2").ToArray();
            if (sources.Length == 0) throw new InvalidOperationException("Trees.fbx has no tree1 or tree2.");
            var random = new System.Random(1907);
            var spots = new List<Vector3>();
            for (int i = 0; i < 13; i++) spots.Add(new Vector3(-36f + i * 6f, 0f, 44.5f + (i % 3) * 2.2f));
            foreach (float x in new[] { -24f, -19f, 24f, 30f }) spots.Add(new Vector3(x, 0f, 24f + (float)random.NextDouble() * 5f));
            foreach (float x in new[] { -34f, -26f, -17f, -8f, 2f, 12f, 21f, 30f }) spots.Add(new Vector3(x, 0f, -18f - (float)random.NextDouble() * 6f));
            foreach (var spot in spots)
            {
                var source = sources[random.Next(sources.Length)];
                var tree = new GameObject("Tree");
                tree.transform.SetParent(parent, false);
                tree.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                var renderer = tree.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials
                    .Select(material => LoadMaterial(material.name.ToLowerInvariant().Contains("trunk") ? "Intro_Trunk" : "Intro_Leaves")).ToArray();
                tree.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f) * source.transform.rotation;
                tree.transform.localScale = Vector3.one;
                var bounds = renderer.bounds;
                float height = 4.6f + (float)random.NextDouble() * 2.4f;
                tree.transform.localScale = Vector3.one * (height / Mathf.Max(0.001f, bounds.size.y));
                bounds = renderer.bounds;
                tree.transform.position += spot - new Vector3(bounds.center.x, bounds.min.y + 0.05f, bounds.center.z);
            }
        }

        // ---------------------------------------------------------------------------------------- camera

        private sealed class Shot
        {
            public float Start, End, FieldOfView, FieldOfViewEnd;
            public Func<float, float, Vector3> Position, Target;      // (time, eased progress)
        }

        private static float VanX(float time)
        {
            const float start = 27f, stop = 1f, stopTime = 3.7f, brake = 1.9f;
            float travel = start - stop, speed = travel / (stopTime - brake / 2f);
            if (time <= stopTime - brake) return start - speed * time;
            float u = Mathf.Min(time, stopTime) - (stopTime - brake);
            return start - (speed * (stopTime - brake) + speed * u - 0.5f * speed / brake * u * u);
        }

        private static AnimationClip BuildCamera(Animator thief)
        {
            var thiefClip = LoadClip("MuseumThief");
            var bones = thief.GetComponentsInChildren<Transform>(true);
            Transform hips = bones.First(bone => bone.name == "Hips"), head = bones.First(bone => bone.name == "Head");
            Func<Transform, float, Vector3> at = (bone, time) => { thiefClip.SampleAnimation(thief.gameObject, time); return bone.position; };
            // Averaging over half a second keeps the camera from bobbing with every step.
            Func<Transform, float, Vector3> calm = (bone, time) =>
            {
                Vector3 sum = Vector3.zero;
                for (int i = -4; i <= 4; i++) sum += at(bone, Mathf.Clamp(time + i * 0.07f, 9f, Length));
                return sum / 9f;
            };
            Func<Vector3, Vector3, Func<float, float, Vector3>> move = (from, to) => (time, u) => Vector3.Lerp(from, to, u);
            Vector3 eye = PlayerStart + Vector3.up * 1.6f;

            var shots = new List<Shot>
            {
                // The van arrives and parks.
                new Shot { Start = 0f, End = 2.9f, FieldOfView = 36f, FieldOfViewEnd = 40f, Position = move(new Vector3(-8.5f, 1.15f, 32.3f), new Vector3(-7.4f, 1.35f, 32.5f)),
                    Target = (time, u) => new Vector3(VanX(time) - 1f, 1.3f, 35.2f) },
                new Shot { Start = 2.9f, End = 4.7f, FieldOfView = 50f, FieldOfViewEnd = 46f, Position = move(new Vector3(-4.6f, 6.4f, 28.6f), new Vector3(-3.6f, 5.9f, 28.9f)),
                    Target = (time, u) => new Vector3(Mathf.Lerp(1.6f, 1f, u), 0.9f, 34.2f) },
                // The thief gets out and checks the street.
                new Shot { Start = 4.7f, End = 7.05f, FieldOfView = 44f, FieldOfViewEnd = 40f, Position = move(new Vector3(-0.9f, 1.15f, 31.3f), new Vector3(-0.5f, 1.25f, 31.5f)),
                    Target = (time, u) => Vector3.Lerp(new Vector3(1.4f, 1.1f, 34.4f), new Vector3(1.35f, 1.05f, 33.7f), u) },
                new Shot { Start = 7.05f, End = 9f, FieldOfView = 34f, FieldOfViewEnd = 31f, Position = move(new Vector3(0.35f, 1.66f, 31.75f), new Vector3(0.55f, 1.62f, 31.85f)),
                    Target = (time, u) => new Vector3(1.33f, 1.52f, 33.6f) },
                // The ladder goes up and the thief climbs it.
                new Shot { Start = 9f, End = 11.85f, FieldOfView = 52f, FieldOfViewEnd = 48f, Position = move(new Vector3(-5.6f, 1.35f, 34.3f), new Vector3(-5.0f, 1.5f, 33.9f)),
                    Target = move(new Vector3(0f, 2.0f, 32.2f), new Vector3(0f, 2.3f, 31.4f)) },
                new Shot { Start = 11.85f, End = 15.25f, FieldOfView = 50f, FieldOfViewEnd = 46f, Position = move(new Vector3(1.9f, 0.45f, 33.6f), new Vector3(1.5f, 0.8f, 33.4f)),
                    Target = (time, u) => calm(hips, time) + Vector3.up * 0.45f },
                new Shot { Start = 15.25f, End = 18.4f, FieldOfView = 44f, FieldOfViewEnd = 40f, Position = move(new Vector3(2.9f, 4.95f, 25.7f), new Vector3(2.4f, 5.0f, 25.6f)),
                    Target = (time, u) => Vector3.Lerp(new Vector3(0.15f, 4.3f, 30.2f), calm(hips, time) + Vector3.up * 0.2f, Mathf.SmoothStep(0f, 1f, u * 1.4f)) },
                // Across the roof to the garden.
                new Shot { Start = 18.4f, End = 22f, FieldOfView = 50f, FieldOfViewEnd = 50f, Position = (time, u) => calm(hips, time) + Vector3.Lerp(new Vector3(-5.4f, 2.2f, 2.6f), new Vector3(-5.0f, 2.7f, 3.8f), u),
                    Target = (time, u) => calm(hips, time) + new Vector3(0.4f, 0f, -1.4f) },
                new Shot { Start = 22f, End = 23.7f, FieldOfView = 56f, FieldOfViewEnd = 53f, Position = move(new Vector3(-0.9f, 6.1f, 25.4f), new Vector3(-0.6f, 5.9f, 25.0f)),
                    Target = move(new Vector3(1.25f, 3.5f, 20.9f), new Vector3(1.35f, 3.2f, 20.7f)) },
                // The jump, seen from the flowers.
                new Shot { Start = 23.7f, End = 25.55f, FieldOfView = 62f, FieldOfViewEnd = 56f, Position = move(new Vector3(0.22f, 0.75f, 17.62f), new Vector3(0.26f, 0.85f, 17.7f)),
                    Target = (time, u) => calm(hips, time) + Vector3.up * 0.15f },
                // Into hiding, ending exactly at the player's eyes.
                new Shot { Start = 25.55f, End = Length, FieldOfView = 58f, FieldOfViewEnd = 75f,
                    Position = (time, u) => Vector3.Lerp(new Vector3(2.75f, 2.35f, 21.35f), eye, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.97f, u))),
                    Target = (time, u) => Vector3.Lerp(calm(head, time), eye + Vector3.back * 4f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.92f, u))) },
            };

            var px = new List<Keyframe>(); var py = new List<Keyframe>(); var pz = new List<Keyframe>();
            var rx = new List<Keyframe>(); var ry = new List<Keyframe>(); var rz = new List<Keyframe>(); var rw = new List<Keyframe>();
            var fov = new List<Keyframe>();
            Quaternion previous = Quaternion.identity;
            foreach (var shot in shots)
            {
                int steps = Mathf.CeilToInt((shot.End - shot.Start) * 30f);
                for (int i = 0; i <= steps; i++)
                {
                    float progress = i / (float)steps, time = Mathf.Lerp(shot.Start, shot.End, progress);
                    float u = Mathf.SmoothStep(0f, 1f, progress);
                    Vector3 position = shot.Position(time, u);
                    Quaternion rotation = Quaternion.LookRotation(shot.Target(time, u) - position, Vector3.up);
                    if (Quaternion.Dot(previous, rotation) < 0f) rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                    previous = rotation;
                    // A cut: the last key of a shot sits just before the first key of the next.
                    float key = i == steps && shot.End < Length ? time - 0.0005f : time;
                    px.Add(new Keyframe(key, position.x)); py.Add(new Keyframe(key, position.y)); pz.Add(new Keyframe(key, position.z));
                    rx.Add(new Keyframe(key, rotation.x)); ry.Add(new Keyframe(key, rotation.y)); rz.Add(new Keyframe(key, rotation.z)); rw.Add(new Keyframe(key, rotation.w));
                    fov.Add(new Keyframe(key, Mathf.Lerp(shot.FieldOfView, shot.FieldOfViewEnd, u)));
                }
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CameraClipPath);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, CameraClipPath); }
            clip.ClearCurves();
            clip.frameRate = 30f;
            SetCurve(clip, typeof(Transform), "localPosition.x", px); SetCurve(clip, typeof(Transform), "localPosition.y", py); SetCurve(clip, typeof(Transform), "localPosition.z", pz);
            SetCurve(clip, typeof(Transform), "localRotation.x", rx); SetCurve(clip, typeof(Transform), "localRotation.y", ry);
            SetCurve(clip, typeof(Transform), "localRotation.z", rz); SetCurve(clip, typeof(Transform), "localRotation.w", rw);
            SetCurve(clip, typeof(Camera), "field of view", fov);
            EditorUtility.SetDirty(clip);
            thiefClip.SampleAnimation(thief.gameObject, 0f);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, Type type, string property, List<Keyframe> keys)
        {
            var curve = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            clip.SetCurve("Camera", type, property, curve);
        }

        // ---------------------------------------------------------------------------------------- timeline

        private static void BuildTimeline(PlayableDirector director, Animator thief, Animator van, Animator ladder, Animator rig, AnimationClip camera, AudioSource sound)
        {
            AssetDatabase.DeleteAsset(TimelinePath);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, TimelinePath);
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = Length;
            director.playableAsset = timeline;
            Animate(timeline, director, "Thief", thief, LoadClip("MuseumThief"));
            Animate(timeline, director, "Van", van, LoadClip("HeistVan"));
            Animate(timeline, director, "Ladder", ladder, LoadClip("HeistLadder"));
            Animate(timeline, director, "Camera", rig, camera);
            Show(timeline, director, "Show thief", thief.gameObject, ThiefAppears, Length);
            Show(timeline, director, "Show ladder", ladder.gameObject, LadderRaised, Length);
            // The same ladder rides on the van until it is raised.
            var rack = van.GetComponentsInChildren<Transform>(true).First(part => part.name == "Van_RackLadder").gameObject;
            Show(timeline, director, "Show ladder on van", rack, 0f, LadderRaised);
            var track = timeline.CreateTrack<AudioTrack>(null, "Soundtrack");
            var audio = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
            var clip = track.CreateClip(audio);
            clip.start = 0d;
            clip.duration = Math.Min(audio.length, Length);
            director.SetGenericBinding(track, sound);
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();
        }

        private static void Animate(TimelineAsset timeline, PlayableDirector director, string name, Animator animator, AnimationClip animation)
        {
            var track = timeline.CreateTrack<AnimationTrack>(null, name);
            var clip = track.CreateClip(animation);
            clip.start = 0d;
            clip.duration = Math.Min(animation.length, Length);
            clip.displayName = name;
            director.SetGenericBinding(track, animator);
        }

        private static void Show(TimelineAsset timeline, PlayableDirector director, string name, GameObject target, float from, float to)
        {
            var track = timeline.CreateTrack<ActivationTrack>(null, name);
            var clip = track.CreateDefaultClip();
            clip.start = from;
            clip.duration = to - from;
            director.SetGenericBinding(track, target);
        }

        // ---------------------------------------------------------------------------------------- scene

        [MenuItem("Tools/Lockdown/Intro/3 Place Intro in Museum")]
        public static void PlaceInMuseum()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MuseumPath) throw new InvalidOperationException("Open Museum_Main first.");
            var roots = scene.GetRootGameObjects();
            var existing = roots.FirstOrDefault(root => root.GetComponent<IntroCutscene>() != null);
            if (existing == null)
            {
                existing = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
                existing.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            // The building and its light stay on; everything else waits for the intro to end.
            var scenery = roots.Where(root => root.name == "Museum" || root.GetComponent<Light>() != null).ToArray();
            var data = new SerializedObject(existing.GetComponent<IntroCutscene>());
            var list = data.FindProperty("scenery");
            list.arraySize = scenery.Length;
            for (int i = 0; i < scenery.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = scenery[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            var player = roots.Select(root => root.GetComponent<FirstPersonController>()).First(found => found != null);
            player.transform.SetPositionAndRotation(PlayerStart, Quaternion.Euler(0f, PlayerStartYaw, 0f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}

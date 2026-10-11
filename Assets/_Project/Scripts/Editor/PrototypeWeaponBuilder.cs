using System;
using System.IO;
using Lockdown.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class PrototypeWeaponBuilder
    {
        public const string WeaponPath = "Assets/_Project/Prefabs/Weapons/PrototypePistol.prefab";
        private const string ImpactPath = "Assets/_Project/Prefabs/Weapons/PistolImpact.prefab";
        private const string SoundPath = "Assets/_Project/Audio/Weapons/PrototypePistolShot.wav";

        [MenuItem("Tools/Lockdown/Build Prototype Pistol")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the pistol.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(FirstPersonPlayerBuilder.PrefabPath) == null)
                throw new InvalidOperationException("Build the first-person player first.");
            EnsureFolder("Assets/_Project/Prefabs/Weapons");
            EnsureFolder("Assets/_Project/Materials/Weapons");
            EnsureFolder("Assets/_Project/Audio/Weapons");
            var metal = Material("PistolMetal", new Color(0.22f, 0.24f, 0.26f), false);
            var grip = Material("PistolGrip", new Color(0.07f, 0.08f, 0.07f), false);
            var accent = Material("PistolAccent", new Color(0.24f, 0.62f, 0.45f), false);
            var flash = Material("PistolFlash", new Color(1f, 0.65f, 0.15f), true);
            CreateSound();

            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                SceneManager.MoveGameObjectToScene(impact, preview);
                impact.name = "PistolImpact";
                impact.transform.localScale = new Vector3(0.045f, 0.045f, 0.008f);
                UnityEngine.Object.DestroyImmediate(impact.GetComponent<Collider>());
                impact.GetComponent<Renderer>().sharedMaterial = flash;
                PrefabUtility.SaveAsPrefabAsset(impact, ImpactPath);

                var weapon = new GameObject("PrototypePistol");
                SceneManager.MoveGameObjectToScene(weapon, preview);
                Part(weapon.transform, "Slide", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.06f), new Vector3(0.085f, 0.075f, 0.28f), metal);
                Part(weapon.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.09f, -0.03f), new Vector3(0.075f, 0.17f, 0.09f), grip).localRotation = Quaternion.Euler(15f, 0f, 0f);
                Part(weapon.transform, "GripAccent", PrimitiveType.Cube, new Vector3(0.039f, -0.075f, -0.025f), new Vector3(0.004f, 0.055f, 0.04f), accent);
                Part(weapon.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0.2f), new Vector3(0.037f, 0.024f, 0.037f), grip).localRotation = Quaternion.Euler(90f, 0f, 0f);
                Part(weapon.transform, "FrontSight", PrimitiveType.Cube, new Vector3(0f, 0.065f, 0.16f), new Vector3(0.012f, 0.018f, 0.025f), accent);
                Part(weapon.transform, "RearSight", PrimitiveType.Cube, new Vector3(0f, 0.065f, -0.055f), new Vector3(0.042f, 0.014f, 0.015f), grip);
                Part(weapon.transform, "TriggerGuard", PrimitiveType.Cube, new Vector3(0f, -0.055f, 0.075f), new Vector3(0.045f, 0.018f, 0.075f), metal);
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(weapon.transform, false);
                muzzle.localPosition = new Vector3(0f, 0.015f, 0.235f);
                var muzzleFlash = Part(muzzle, "MuzzleFlash", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.035f), new Vector3(0.085f, 0.085f, 0.13f), flash).gameObject;
                muzzleFlash.SetActive(false);
                var audio = weapon.AddComponent<AudioSource>();
                audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;
                audio.volume = 0.25f;
                var tracer = weapon.AddComponent<LineRenderer>();
                tracer.positionCount = 2;
                tracer.useWorldSpace = true;
                tracer.startWidth = 0.007f;
                tracer.endWidth = 0.004f;
                tracer.sharedMaterial = flash;
                tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                tracer.receiveShadows = false;
                tracer.enabled = false;
                var shooting = weapon.AddComponent<FirstPersonWeapon>();
                var serialized = new SerializedObject(shooting);
                serialized.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
                serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
                serialized.FindProperty("muzzleFlash").objectReferenceValue = muzzleFlash;
                serialized.FindProperty("impactPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPath);
                serialized.FindProperty("shotAudio").objectReferenceValue = audio;
                serialized.FindProperty("tracer").objectReferenceValue = tracer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(weapon, WeaponPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var player = PrefabUtility.LoadPrefabContents(FirstPersonPlayerBuilder.PrefabPath);
            try
            {
                var holder = player.transform.Find("PlayerCamera/WeaponHolder");
                if (holder == null) throw new InvalidOperationException("The player is missing WeaponHolder.");
                if (holder.GetComponentInChildren<FirstPersonWeapon>(true) == null)
                {
                    var pistol = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponPath), player.scene);
                    pistol.transform.SetParent(holder, false);
                }
                PlayerEquipmentBuilder.Configure(player);
                PrefabUtility.SaveAsPrefabAsset(player, FirstPersonPlayerBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            Debug.Log("Prototype pistol, materials, shot audio, and impact prefab created and equipped on FirstPersonPlayer.");
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material Material(string name, Color color, bool unlit)
        {
            string path = "Assets/_Project/Materials/Weapons/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Required URP shader was not found.");
            var material = new Material(shader) { name = name, color = color };
            if (!unlit) { material.SetFloat("_Metallic", name == "PistolMetal" ? 0.6f : 0f); material.SetFloat("_Smoothness", 0.3f); }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreateSound()
        {
            if (File.Exists(SoundPath)) return;
            const int sampleRate = 22050;
            const int sampleCount = 3308;
            var random = new System.Random(42);
            using (var writer = new BinaryWriter(File.Create(SoundPath)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + sampleCount * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(sampleRate); writer.Write(sampleRate * 2);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(sampleCount * 2);
                for (int i = 0; i < sampleCount; i++)
                {
                    float t = i / (float)sampleRate;
                    float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                    float sample = (noise * 0.65f + Mathf.Sin(t * 2f * Mathf.PI * 85f) * 0.35f) * Mathf.Exp(-t * 45f) * Mathf.Min(1f, i / 12f);
                    writer.Write((short)(sample * 23000f));
                }
            }
            AssetDatabase.ImportAsset(SoundPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}

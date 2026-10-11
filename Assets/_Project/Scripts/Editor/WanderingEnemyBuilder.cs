using System;
using Lockdown.Combat;
using Lockdown.Enemies;
using Lockdown.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class WanderingEnemyBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/WanderingEnemy.prefab";
        private const string MuseumPath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";

        [MenuItem("Tools/Lockdown/Build Wandering Enemy")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the enemy.");
            EnsureFolder("Assets/_Project/Prefabs/Characters");
            EnsureFolder("Assets/_Project/Materials/Enemies");
            var uniform = Material("EnemyUniform", new Color(0.68f, 0.12f, 0.2f));
            var dark = Material("EnemyBoots", new Color(0.08f, 0.09f, 0.1f));
            var helmet = Material("EnemyHelmet", new Color(0.68f, 0.72f, 0.75f));
            var visor = Material("EnemyVisor", new Color(0.05f, 0.2f, 0.23f));
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("WanderingEnemy");
                SceneManager.MoveGameObjectToScene(root, preview);
                var body = root.AddComponent<CapsuleCollider>();
                body.height = 1.8f;
                body.radius = 0.32f;
                body.center = Vector3.up * 0.9f;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.height = 1.8f;
                agent.radius = 0.32f;
                agent.speed = 1.4f;
                agent.angularSpeed = 180f;
                agent.acceleration = 5f;
                agent.stoppingDistance = 0.15f;
                root.AddComponent<Health>();
                var model = new GameObject("Model").transform;
                model.SetParent(root.transform, false);
                var torso = Part(model, "Torso", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.55f, 0.6f, 0.3f), uniform);
                Part(model, "Belt", PrimitiveType.Cube, new Vector3(0f, 0.86f, 0f), new Vector3(0.56f, 0.09f, 0.32f), dark);
                Part(model, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.61f, 0f), new Vector3(0.37f, 0.38f, 0.35f), helmet);
                Part(model, "Visor", PrimitiveType.Cube, new Vector3(0f, 1.62f, 0.17f), new Vector3(0.29f, 0.11f, 0.045f), visor);
                Part(model, "Badge", PrimitiveType.Cube, new Vector3(-0.13f, 1.3f, 0.156f), new Vector3(0.07f, 0.09f, 0.015f), helmet);
                var leftArm = Limb(model, "LeftArm", new Vector3(-0.36f, 1.39f, 0f), new Vector3(0.15f, 0.55f, 0.18f), uniform);
                var rightArm = Limb(model, "RightArm", new Vector3(0.36f, 1.39f, 0f), new Vector3(0.15f, 0.55f, 0.18f), uniform);
                var leftLeg = Limb(model, "LeftLeg", new Vector3(-0.16f, 0.83f, 0f), new Vector3(0.2f, 0.7f, 0.23f), dark);
                var rightLeg = Limb(model, "RightLeg", new Vector3(0.16f, 0.83f, 0f), new Vector3(0.2f, 0.7f, 0.23f), dark);
                Part(leftLeg, "Boot", PrimitiveType.Cube, new Vector3(0f, -0.75f, 0.045f), new Vector3(0.22f, 0.16f, 0.34f), dark);
                Part(rightLeg, "Boot", PrimitiveType.Cube, new Vector3(0f, -0.75f, 0.045f), new Vector3(0.22f, 0.16f, 0.34f), dark);
                var wander = root.AddComponent<WanderingEnemy>();
                var serialized = new SerializedObject(wander);
                serialized.FindProperty("model").objectReferenceValue = model;
                serialized.FindProperty("leftArm").objectReferenceValue = leftArm;
                serialized.FindProperty("rightArm").objectReferenceValue = rightArm;
                serialized.FindProperty("leftLeg").objectReferenceValue = leftLeg;
                serialized.FindProperty("rightLeg").objectReferenceValue = rightLeg;
                serialized.FindProperty("bodyRenderer").objectReferenceValue = torso.GetComponent<Renderer>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AddShooting(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            AssetDatabase.SaveAssets();
            Debug.Log("Wandering enemy prefab and placeholder character materials created.");
        }

        [MenuItem("Tools/Lockdown/Place Wandering Enemy In Museum")]
        public static void PlaceInMuseum()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MuseumPath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            foreach (var existing in UnityEngine.Object.FindObjectsByType<WanderingEnemy>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (existing.gameObject.scene == scene) { Selection.activeGameObject = existing.gameObject; return; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build the wandering enemy first.");
            Vector3 position = new Vector3(0f, 0f, -2.5f);
            var filter = new NavMeshQueryFilter { agentTypeID = prefab.GetComponent<NavMeshAgent>().agentTypeID, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(position, out var hit, 1f, filter))
                throw new InvalidOperationException("The museum NavMesh is not available near the central hall spawn.");
            var enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            enemy.transform.SetPositionAndRotation(hit.position, Quaternion.Euler(0f, 180f, 0f));
            Undo.RegisterCreatedObjectUndo(enemy, "Place wandering enemy");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = enemy;
            Debug.Log("One wandering enemy placed in the central hall at " + hit.position);
        }

        [MenuItem("Tools/Lockdown/Add Basic Enemy Shooting")]
        public static void AddBasicShooting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before adding shooting.");
            var enemy = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                AddShooting(enemy);
                PrefabUtility.SaveAsPrefabAsset(enemy, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(enemy); }
            var player = PrefabUtility.LoadPrefabContents(FirstPersonPlayerBuilder.PrefabPath);
            try
            {
                if (player.GetComponent<PlayerVitals>() == null) player.AddComponent<PlayerVitals>();
                PrefabUtility.SaveAsPrefabAsset(player, FirstPersonPlayerBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            Debug.Log("Basic enemy shooting equipped; player health, damage feedback, and death handling added.");
        }

        private static void AddShooting(GameObject root)
        {
            if (root.GetComponent<EnemyShooting>() != null) return;
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weapons/PistolMetal.mat");
            var grip = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weapons/PistolGrip.mat");
            var flash = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Weapons/PistolFlash.mat");
            if (metal == null || grip == null || flash == null)
                throw new InvalidOperationException("Build the prototype pistol assets first.");
            var gun = new GameObject("EnemyPistol").transform;
            gun.SetParent(root.transform.Find("Model"), false);
            gun.localPosition = new Vector3(0.36f, 1.18f, 0.2f);
            Part(gun, "Slide", PrimitiveType.Cube, new Vector3(0f, 0f, 0.12f), new Vector3(0.08f, 0.08f, 0.32f), metal);
            Part(gun, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.08f, 0f), new Vector3(0.07f, 0.16f, 0.08f), grip);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(gun, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.3f);
            var muzzleFlash = Part(muzzle, "MuzzleFlash", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.03f), new Vector3(0.08f, 0.08f, 0.12f), flash).gameObject;
            muzzleFlash.SetActive(false);
            var audio = gun.gameObject.AddComponent<AudioSource>();
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Weapons/PrototypePistolShot.wav");
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 18f;
            audio.volume = 0.4f;
            var tracer = gun.gameObject.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.useWorldSpace = true;
            tracer.startWidth = 0.015f;
            tracer.endWidth = 0.008f;
            tracer.sharedMaterial = flash;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracer.enabled = false;
            var shooting = root.AddComponent<EnemyShooting>();
            var serialized = new SerializedObject(shooting);
            serialized.FindProperty("gunPivot").objectReferenceValue = gun;
            serialized.FindProperty("muzzle").objectReferenceValue = muzzle;
            serialized.FindProperty("muzzleFlash").objectReferenceValue = muzzleFlash;
            serialized.FindProperty("tracer").objectReferenceValue = tracer;
            serialized.FindProperty("shotAudio").objectReferenceValue = audio;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Limb(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            Part(pivot, "Mesh", PrimitiveType.Cube, Vector3.down * scale.y * 0.5f, scale, material);
            return pivot;
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

        private static Material Material(string name, Color color)
        {
            string path = "Assets/_Project/Materials/Enemies/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader was not found.");
            var material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
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

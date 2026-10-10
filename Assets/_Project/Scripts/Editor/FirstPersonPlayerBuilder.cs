using System;
using Lockdown.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class FirstPersonPlayerBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Characters/FirstPersonPlayer.prefab";
        public const string TestScenePath = "Assets/_Project/Scenes/Player/Player_Test.unity";
        private const string MuseumScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";

        [MenuItem("Tools/Lockdown/Build First-Person Player")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the player.");

            EnsureFolder("Assets/_Project/Prefabs/Characters");
            EnsureFolder("Assets/_Project/Scenes/Player");
            EnsureFolder("Assets/_Project/Materials/PlayerTest");
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (input == null) throw new InvalidOperationException("The project input actions asset is missing.");

            var previousScene = SceneManager.GetActiveScene();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var player = new GameObject("FirstPersonPlayer");
                SceneManager.MoveGameObjectToScene(player, preview);
                player.tag = "Player";
                var body = player.AddComponent<CharacterController>();
                body.height = 1.8f;
                body.radius = 0.3f;
                body.center = new Vector3(0f, 0.9f, 0f);
                body.stepOffset = 0.3f;
                body.slopeLimit = 45f;
                body.skinWidth = 0.03f;
                body.minMoveDistance = 0f;

                var cameraObject = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(player.transform, false);
                cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.fieldOfView = 75f;
                camera.nearClipPlane = 0.03f;
                camera.farClipPlane = 150f;

                var holder = new GameObject("WeaponHolder");
                holder.transform.SetParent(cameraObject.transform, false);
                holder.transform.localPosition = new Vector3(0.2f, -0.2f, 0.45f);

                var controller = player.AddComponent<FirstPersonController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("playerCamera").objectReferenceValue = camera;
                serialized.FindProperty("inputActions").objectReferenceValue = input;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var test = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(test);
            var floorMaterial = Material("Floor", new Color(0.32f, 0.36f, 0.37f));
            var wallMaterial = Material("Walls", new Color(0.74f, 0.77f, 0.73f));
            var obstacleMaterial = Material("Obstacles", new Color(0.2f, 0.52f, 0.38f));
            Cube("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(18f, 0.5f, 24f), floorMaterial);
            Cube("Wall_Front", new Vector3(0f, 2f, 12f), new Vector3(18f, 4f, 0.4f), wallMaterial);
            Cube("Wall_Back", new Vector3(0f, 2f, -12f), new Vector3(18f, 4f, 0.4f), wallMaterial);
            Cube("Wall_Left", new Vector3(-9f, 2f, 0f), new Vector3(0.4f, 4f, 24f), wallMaterial);
            Cube("Wall_Right", new Vector3(9f, 2f, 0f), new Vector3(0.4f, 4f, 24f), wallMaterial);
            Cube("Cover", new Vector3(0f, 0.6f, 4f), new Vector3(2f, 1.2f, 1f), obstacleMaterial);
            Cube("Step_Low", new Vector3(-4f, 0.1f, 2f), new Vector3(2f, 0.2f, 1f), obstacleMaterial);
            Cube("Step_Middle", new Vector3(-4f, 0.2f, 3f), new Vector3(2f, 0.4f, 1f), obstacleMaterial);
            Cube("Step_High", new Vector3(-4f, 0.3f, 4f), new Vector3(2f, 0.6f, 1f), obstacleMaterial);
            var lightObject = new GameObject("Directional Light", typeof(Light));
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
            InstantiatePlayer(test, new Vector3(0f, 0.05f, -7f), Quaternion.identity);
            EditorSceneManager.SaveScene(test, TestScenePath);
            EditorSceneManager.CloseScene(test, true);
            if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
            AssetDatabase.SaveAssets();
            Debug.Log("First-person player prefab and Player_Test scene created.");
        }

        [MenuItem("Tools/Lockdown/Place Player In Museum")]
        public static void PlaceInMuseum()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != MuseumScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var existing = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            if (existing != null) throw new InvalidOperationException("A player controller already exists in the scene.");

            var marker = GameObject.Find("Spawn_Player");
            var position = marker != null ? marker.transform.position : new Vector3(0f, 0f, -9.5f);
            var rotation = marker != null ? marker.transform.rotation : Quaternion.identity;
            position.y += 0.05f;
            var player = InstantiatePlayer(scene, position, rotation);
            Undo.RegisterCreatedObjectUndo(player, "Place first-person player");
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera.gameObject.scene != scene || camera.transform.IsChildOf(player.transform)) continue;
                Undo.RecordObject(camera, "Disable old museum camera");
                camera.enabled = false;
                var listener = camera.GetComponent<AudioListener>();
                if (listener != null) { Undo.RecordObject(listener, "Disable old audio listener"); listener.enabled = false; }
            }
            Physics.SyncTransforms();
            var body = player.GetComponent<CharacterController>();
            var lower = position + Vector3.up * (body.radius + 0.04f);
            var upper = position + Vector3.up * (body.height - body.radius);
            foreach (var hit in Physics.OverlapCapsule(lower, upper, body.radius, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(player.transform)) Debug.LogWarning("Player spawn overlaps " + hit.name, hit);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = player;
            Debug.Log("First-person player placed at museum spawn " + position);
        }

        private static GameObject InstantiatePlayer(Scene scene, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build the player prefab first.");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.SetPositionAndRotation(position, rotation);
            return player;
        }

        private static void Cube(string name, Vector3 position, Vector3 size, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = size;
            cube.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/_Project/Materials/PlayerTest/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader is unavailable.");
            material = new Material(shader) { name = name, color = color };
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

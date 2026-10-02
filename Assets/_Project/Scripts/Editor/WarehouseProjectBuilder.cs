using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.EditorTools
{
    public static class WarehouseProjectBuilder
    {
        private const string ProjectRoot = "Assets/_Project";
        private const string ScenesRoot = ProjectRoot + "/Scenes";
        private const string MaterialsRoot = ProjectRoot + "/Materials";
        private const string PrefabsRoot = ProjectRoot + "/Prefabs";

        [MenuItem("Warehouse Lockdown/Build Starter Project")]
        public static void BuildStarterProject()
        {
            EnsureProjectFolders();
            CreateStarterMaterials();
            CreateStarterPrefabs();
            CreateStarterScenes();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Warehouse Lockdown starter project generated.");
        }

        [MenuItem("Warehouse Lockdown/Create Starter Scenes")]
        public static void CreateStarterScenes()
        {
            EnsureProjectFolders();

            CreateScene("Bootstrap", "Bootstrap", new Color(0.05f, 0.06f, 0.07f));
            CreateScene("Env", "Warehouse Environment", new Color(0.10f, 0.11f, 0.12f));
            CreateScene("Interactables", "Interactables", new Color(0.08f, 0.09f, 0.10f));
            CreateScene("Agents", "AI Agents", new Color(0.07f, 0.08f, 0.10f));
            CreateScene("ModelShowcase", "Model Showcase", new Color(0.12f, 0.11f, 0.10f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Warehouse Lockdown/Ensure Project Folders")]
        public static void EnsureProjectFolders()
        {
            EnsureFolder(ProjectRoot);
            EnsureFolder(ScenesRoot);
            EnsureFolder(MaterialsRoot);
            EnsureFolder(PrefabsRoot);
            EnsureFolder(PrefabsRoot + "/Characters");
            EnsureFolder(PrefabsRoot + "/Environment");
            EnsureFolder(PrefabsRoot + "/Props");
            EnsureFolder(PrefabsRoot + "/FX");
            EnsureFolder(PrefabsRoot + "/UI");
            EnsureFolder(ProjectRoot + "/Models");
            EnsureFolder(ProjectRoot + "/Textures");
            EnsureFolder(ProjectRoot + "/Audio");
            EnsureFolder(ProjectRoot + "/Lighting");
            EnsureFolder(ProjectRoot + "/PostProcessing");
        }

        private static void CreateStarterMaterials()
        {
            CreateMaterial("WarehouseFloor", new Color(0.20f, 0.22f, 0.23f));
            CreateMaterial("HazardYellow", new Color(1.00f, 0.73f, 0.12f));
            CreateMaterial("SecurityRed", new Color(0.85f, 0.12f, 0.10f));
            CreateMaterial("DroneBlue", new Color(0.20f, 0.58f, 0.95f));
            CreateMaterial("TerminalGreen", new Color(0.14f, 0.85f, 0.48f));
        }

        private static void CreateStarterPrefabs()
        {
            CreateCubePrefab("Crate", PrefabsRoot + "/Props/Crate.prefab", new Vector3(1.4f, 1.4f, 1.4f), "WarehouseFloor");
            CreateCubePrefab("OverrideTerminal", PrefabsRoot + "/Props/OverrideTerminal.prefab", new Vector3(0.8f, 1.6f, 0.35f), "TerminalGreen");
            CreateCubePrefab("BlastDoor", PrefabsRoot + "/Environment/BlastDoor.prefab", new Vector3(4.0f, 3.0f, 0.45f), "SecurityRed");
            CreateCubePrefab("AgentSpawnAnchor", PrefabsRoot + "/Characters/AgentSpawnAnchor.prefab", new Vector3(0.4f, 0.4f, 0.4f), "DroneBlue");
        }

        private static void CreateScene(string sceneName, string label, Color backgroundColor)
        {
            string scenePath = ScenesRoot + "/" + sceneName + ".unity";
            if (File.Exists(scenePath))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new GameObject(label);
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 6f, -10f);
            cameraObject.transform.rotation = Quaternion.Euler(32f, 0f, 0f);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Blockout Floor";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(18f, 0.2f, 18f);
            ApplyMaterial(floor, "WarehouseFloor");

            AddSceneSpecificBlockout(sceneName);

            root.transform.SetAsFirstSibling();
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void AddSceneSpecificBlockout(string sceneName)
        {
            if (sceneName == "Env")
            {
                for (int i = -2; i <= 2; i++)
                {
                    GameObject shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shelf.name = "Warehouse Shelf Blockout";
                    shelf.transform.position = new Vector3(i * 3f, 1.5f, 2.5f);
                    shelf.transform.localScale = new Vector3(0.4f, 3f, 5f);
                    ApplyMaterial(shelf, "HazardYellow");
                }
            }

            if (sceneName == "Interactables")
            {
                InstantiatePrefab("Assets/_Project/Prefabs/Props/OverrideTerminal.prefab", new Vector3(-2f, 0.9f, 0f));
                InstantiatePrefab("Assets/_Project/Prefabs/Environment/BlastDoor.prefab", new Vector3(2.5f, 1.5f, 0f));
            }

            if (sceneName == "Agents")
            {
                InstantiatePrefab("Assets/_Project/Prefabs/Characters/AgentSpawnAnchor.prefab", new Vector3(-3f, 0.4f, 0f));
                InstantiatePrefab("Assets/_Project/Prefabs/Characters/AgentSpawnAnchor.prefab", new Vector3(0f, 0.4f, 0f));
                InstantiatePrefab("Assets/_Project/Prefabs/Characters/AgentSpawnAnchor.prefab", new Vector3(3f, 0.4f, 0f));
            }
        }

        private static void CreateMaterial(string materialName, Color color)
        {
            string path = MaterialsRoot + "/" + materialName + ".mat";
            if (File.Exists(path))
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            AssetDatabase.CreateAsset(material, path);
        }

        private static void CreateCubePrefab(string name, string path, Vector3 scale, string materialName)
        {
            if (File.Exists(path))
            {
                return;
            }

            GameObject instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = name;
            instance.transform.localScale = scale;
            ApplyMaterial(instance, materialName);

            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        private static void InstantiatePrefab(string path, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
        }

        private static void ApplyMaterial(GameObject target, string materialName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/" + materialName + ".mat");
            Renderer renderer = target.GetComponent<Renderer>();
            if (material != null && renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path);
            string folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent.Replace('\\', '/'));
                AssetDatabase.CreateFolder(parent.Replace('\\', '/'), folderName);
            }
        }
    }
}

using System;
using Lockdown.Player;
using Lockdown.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class PlayerEquipmentBuilder
    {
        public const string HandsPath = "Assets/_Project/Prefabs/Characters/FirstPersonHands.prefab";
        public const string GripPath = "Assets/_Project/Prefabs/Characters/PistolGripHand.prefab";
        private const string SkinPath = "Assets/_Project/Materials/Player/HandsSkin.mat";

        [MenuItem("Tools/Lockdown/Build Player Hands And Switching")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building player hands.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeWeaponBuilder.WeaponPath) == null)
                throw new InvalidOperationException("Build the prototype pistol first.");
            EnsureFolder("Assets/_Project/Materials/Player");
            var skin = AssetDatabase.LoadAssetAtPath<Material>(SkinPath);
            if (skin == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP Lit shader was not found.");
                skin = new Material(shader) { name = "HandsSkin", color = new Color(0.72f, 0.49f, 0.35f) };
                skin.SetFloat("_Smoothness", 0.2f);
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var hands = new GameObject("EmptyHands");
                SceneManager.MoveGameObjectToScene(hands, preview);
                Fist(hands.transform, "LeftHand", new Vector3(-0.24f, -0.28f, 0.48f), -1f, skin);
                Fist(hands.transform, "RightHand", new Vector3(0.24f, -0.28f, 0.48f), 1f, skin);
                PrefabUtility.SaveAsPrefabAsset(hands, HandsPath);
                var grip = new GameObject("PistolGripHand");
                SceneManager.MoveGameObjectToScene(grip, preview);
                Part(grip.transform, "Palm", PrimitiveType.Cube, new Vector3(0f, -0.1f, -0.03f), new Vector3(0.12f, 0.085f, 0.075f), skin);
                var arm = Part(grip.transform, "Forearm", PrimitiveType.Capsule, new Vector3(0f, -0.22f, -0.16f), new Vector3(0.095f, 0.18f, 0.095f), skin);
                arm.localRotation = Quaternion.Euler(50f, 0f, 0f);
                for (int i = 0; i < 4; i++)
                    Part(grip.transform, "Finger" + i, PrimitiveType.Cube, new Vector3(0.043f, -0.065f - i * 0.022f, 0.005f), new Vector3(0.035f, 0.019f, 0.08f), skin);
                var thumb = Part(grip.transform, "Thumb", PrimitiveType.Capsule, new Vector3(-0.053f, -0.055f, -0.012f), new Vector3(0.033f, 0.042f, 0.033f), skin);
                thumb.localRotation = Quaternion.Euler(15f, 0f, -25f);
                PrefabUtility.SaveAsPrefabAsset(grip, GripPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }

            var player = PrefabUtility.LoadPrefabContents(FirstPersonPlayerBuilder.PrefabPath);
            try
            {
                Configure(player);
                PrefabUtility.SaveAsPrefabAsset(player, FirstPersonPlayerBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
            Debug.Log("Player now starts with empty hands; 1 selects hands and 2 selects the owned pistol.");
        }

        public static void Configure(GameObject player)
        {
            var handsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandsPath);
            var gripPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GripPath);
            if (handsPrefab == null || gripPrefab == null) return;
            var camera = player.transform.Find("PlayerCamera");
            var weapon = player.GetComponentInChildren<FirstPersonWeapon>(true);
            if (camera == null || weapon == null) throw new InvalidOperationException("Player camera or pistol is missing.");
            var hands = camera.Find("EmptyHands");
            if (hands == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(handsPrefab, player.scene);
                instance.transform.SetParent(camera, false);
                hands = instance.transform;
            }
            if (weapon.transform.Find("PistolGripHand") == null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(gripPrefab, player.scene);
                instance.transform.SetParent(weapon.transform, false);
            }
            var equipment = player.GetComponent<PlayerEquipment>();
            bool isNew = equipment == null;
            if (isNew) equipment = player.AddComponent<PlayerEquipment>();
            var serialized = new SerializedObject(equipment);
            serialized.FindProperty("emptyHands").objectReferenceValue = hands.gameObject;
            serialized.FindProperty("pistol").objectReferenceValue = weapon;
            if (isNew) serialized.FindProperty("startWithPistol").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Fist(Transform parent, string name, Vector3 position, float side, Material skin)
        {
            var hand = new GameObject(name).transform;
            hand.SetParent(parent, false);
            hand.localPosition = position;
            hand.localRotation = Quaternion.Euler(8f, -side * 12f, side * 8f);
            Part(hand, "Palm", PrimitiveType.Cube, Vector3.zero, new Vector3(0.105f, 0.08f, 0.075f), skin);
            var arm = Part(hand, "Forearm", PrimitiveType.Capsule, new Vector3(0f, -0.12f, -0.1f), new Vector3(0.095f, 0.18f, 0.095f), skin);
            arm.localRotation = Quaternion.Euler(50f, 0f, 0f);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.039f + i * 0.026f;
                Part(hand, "Knuckle" + i, PrimitiveType.Sphere, new Vector3(x, 0.03f, 0.027f), new Vector3(0.026f, 0.034f, 0.04f), skin);
                Part(hand, "Finger" + i, PrimitiveType.Cube, new Vector3(x, -0.005f, 0.052f), new Vector3(0.024f, 0.052f, 0.024f), skin);
            }
            var thumb = Part(hand, "Thumb", PrimitiveType.Capsule, new Vector3(-side * 0.054f, -0.012f, 0.035f), new Vector3(0.032f, 0.038f, 0.033f), skin);
            thumb.localRotation = Quaternion.Euler(0f, 0f, side * 30f);
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

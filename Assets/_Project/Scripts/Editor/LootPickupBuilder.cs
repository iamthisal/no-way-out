using System;
using Lockdown.Player;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    public static class LootPickupBuilder
    {
        [MenuItem("Tools/Lockdown/Configure Museum Loot Pickup")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                scene.path != "Assets/_Project/Scenes/Museum/Museum_Main.unity")
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var monitor = Find(scene, "P_Lobby_Monitor");
            var tray = Find(scene, "P_Lobby_BrochureTray");
            if (monitor == null || tray == null)
                throw new InvalidOperationException("The two reception-counter placeholder objects are missing.");
            var player = PrefabUtility.LoadPrefabContents(FirstPersonPlayerBuilder.PrefabPath);
            try
            {
                ConfigurePlayer(player);
                PrefabUtility.SaveAsPrefabAsset(player, FirstPersonPlayerBuilder.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            ConfigureItem(monitor, "Prototype Relic", 250);
            ConfigureItem(tray, "Prototype Trinket", 100);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigurePlayer(GameObject player)
        {
            if (player.GetComponent<LootInventory>() == null) player.AddComponent<LootInventory>();
            if (player.GetComponent<PlayerInteraction>() == null) player.AddComponent<PlayerInteraction>();
        }

        private static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            return null;
        }

        private static void ConfigureItem(GameObject target, string name, int value)
        {
            var collider = target.GetComponent<Collider>();
            if (collider == null) collider = Undo.AddComponent<BoxCollider>(target);
            Undo.RecordObject(collider, "Configure loot collider");
            collider.isTrigger = false;
            var loot = target.GetComponent<LootItem>();
            bool isNew = loot == null;
            if (isNew) loot = Undo.AddComponent<LootItem>(target);
            if (!isNew) return;
            var serialized = new SerializedObject(loot);
            serialized.FindProperty("itemName").stringValue = name;
            serialized.FindProperty("value").intValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}

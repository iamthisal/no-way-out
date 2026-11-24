using System;
using System.Linq;
using Lockdown.AI.Agents;
using Lockdown.Player;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    /// <summary>Places the spawn points and connects the spawn director. Adds objects only; museum geometry is untouched.</summary>
    public static class SpawnDirectorBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";
        private const string SettingsPath = "Assets/_Project/Settings/AI/Museum_DirectorSettings.asset";
        private const string Characters = "Assets/_Project/Prefabs/Characters/";
        private const float SearchRadius = 2f;

        private readonly struct Place
        {
            public readonly string Room;
            public readonly Vector3 Position;
            public readonly SpawnPoint.Zones Zones;
            public readonly SpawnPoint.Kinds Kinds;
            public Place(string room, float x, float z, SpawnPoint.Zones zones, SpawnPoint.Kinds kinds)
            { Room = room; Position = new Vector3(x, 0f, z); Zones = zones; Kinds = kinds; }
        }

        private const SpawnPoint.Zones Open = SpawnPoint.Zones.OpenGallery;
        private const SpawnPoint.Zones Corridor = SpawnPoint.Zones.Corridor;
        private const SpawnPoint.Zones Staff = SpawnPoint.Zones.Staff;
        private const SpawnPoint.Kinds Any = SpawnPoint.Kinds.All;
        private const SpawnPoint.Kinds Guard = SpawnPoint.Kinds.Guard;
        private const SpawnPoint.Kinds Archer = SpawnPoint.Kinds.Archer;
        private const SpawnPoint.Kinds Hound = SpawnPoint.Kinds.Hound;

        // Rooms are named after their floor slab. Points in closed rooms stay unused until a door opens.
        private static readonly Place[] Places =
        {
            new Place("Lobby", -3.5f, -9.5f, Staff, Guard),
            new Place("Security", 9.5f, -9.6f, Staff, Guard),
            new Place("Toilet", -8.6f, -9.6f, Staff, Guard | Hound),
            new Place("Storage", 12.3f, 3.6f, Staff, Guard | Hound),
            new Place("MaintenanceSouth", 6.2f, -4.5f, Staff | Corridor, Any),
            new Place("MaintenanceEast", 15.5f, -4.6f, Staff | Corridor, Any),
            new Place("Exit", 15.5f, 9.4f, Staff, Guard),
            new Place("ArtGallery", -14.6f, -1.8f, Open, Any),
            new Place("ArtGallery", -14.6f, 3.6f, Open, Guard | Hound),
            new Place("Temporary", -14.6f, 6.4f, Open, Any),
            new Place("Exhibition", -5.6f, 12.8f, Open, Any),
            new Place("Exhibition", 5.6f, 12.8f, Open, Any),
            new Place("Egypt", -9.2f, 15.4f, Open | Corridor, Any),
            new Place("StoneAge", -9.2f, 28.4f, Open, Any),
            new Place("GreeceRome", 2.6f, 23.2f, Open | Corridor, Archer | Hound),
            new Place("AsianHeritage", 9.2f, 28.4f, Open, Any),
            new Place("NaturalHistory", 9.4f, 20.4f, Open, Any)
        };

        [MenuItem("Tools/Lockdown/Place Spawn Points")]
        public static void PlaceSpawnPoints()
        {
            var scene = RequireMuseum();
            var director = GetOrCreate(scene, "SpawnDirector");
            var root = director.transform.Find("SpawnPoints");
            if (root == null)
            {
                root = new GameObject("SpawnPoints").transform;
                root.SetParent(director.transform, false);
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Place spawn points");
            }
            foreach (Transform child in root.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            Physics.SyncTransforms();
            for (int i = 0; i < Places.Length; i++)
            {
                var place = Places[i];
                if (!TryFindClearFloor(place, out var floor))
                    throw new InvalidOperationException("No clear floor near the spawn point in " + place.Room + " at " + place.Position);
                var point = new GameObject($"Spawn_{i:00}_{place.Room}").transform;
                point.SetParent(root, false);
                point.position = floor;
                Vector3 inward = RoomCentre(place.Room, floor) - floor;
                inward.y = 0f;
                if (inward.sqrMagnitude > 0.01f) point.rotation = Quaternion.LookRotation(inward);
                point.gameObject.AddComponent<SpawnPoint>().Configure(place.Room, place.Zones, place.Kinds);
                Undo.RegisterCreatedObjectUndo(point.gameObject, "Place spawn points");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Placed {Places.Length} spawn points. Save the scene.");
        }

        [MenuItem("Tools/Lockdown/Connect Spawn Director in Museum")]
        public static void Connect()
        {
            var scene = RequireMuseum();
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null) throw new InvalidOperationException("Museum_Main has no player.");
            if (Object.FindAnyObjectByType<GlassAlarm>() == null) throw new InvalidOperationException("Museum_Main has no GlassAlarm.");
            PlaceSpawnPoints();
            var director = GetOrCreate(scene, "SpawnDirector");
            GetOrAdd<AlarmStimulusRelay>(director);
            var agent = GetOrAdd<SpawnDirectorAgent>(director);
            GetOrAdd<DirectorOverlay>(director);
            var data = new SerializedObject(agent);
            data.FindProperty("settings").objectReferenceValue = LoadSettings();
            data.FindProperty("guardPrefab").objectReferenceValue = Prefab("Pursuer");
            data.FindProperty("archerPrefab").objectReferenceValue = Prefab("SkeletonArcher");
            data.FindProperty("houndPrefab").objectReferenceValue = Prefab("InvestigatingHound");
            data.FindProperty("safeRoomId").stringValue = RoomAt(player.transform.position);
            data.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Spawn director connected. Safe start room: " + agent.SafeRoomId + ". Save the scene.");
        }

        private static Scene RequireMuseum()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            return scene;
        }

        private static bool TryFindClearFloor(Place place, out Vector3 floor)
        {
            Vector3 wanted = place.Position;
            Vector3 centre = RoomCentre(place.Room, wanted);
            centre.y = 0f;
            var routes = new MuseumRoutes();
            for (float radius = 0f; radius <= SearchRadius; radius += 0.25f)
                for (int i = 0; i < (radius > 0f ? 16 : 1); i++)
                {
                    float angle = i * Mathf.PI / 8f;
                    var candidate = wanted + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 0.3f, NavMesh.AllAreas)
                        || Mathf.Abs(hit.position.y - wanted.y) > 0.3f || !SpawnPointSurvey.IsClear(hit.position)
                        // Skip pockets that props cut off from the rest of the room.
                        || float.IsInfinity(routes.Length(hit.position, centre, 2.5f))) continue;
                    floor = hit.position;
                    return true;
                }
            floor = wanted;
            return false;
        }

        private static Transform Floors()
        {
            var museum = GameObject.Find("Museum");
            var floors = museum != null ? museum.transform.Find("Floors") : null;
            if (floors == null) throw new InvalidOperationException("Museum/Floors was not found.");
            return floors;
        }

        private static string RoomAt(Vector3 position)
        {
            foreach (Transform floor in Floors())
            {
                if (!floor.TryGetComponent<Renderer>(out var renderer)) continue;
                var bounds = renderer.bounds;
                if (position.x >= bounds.min.x && position.x <= bounds.max.x && position.z >= bounds.min.z && position.z <= bounds.max.z)
                    return floor.name.Replace("Floor_", "");
            }
            return "";
        }

        private static Vector3 RoomCentre(string room, Vector3 fallback)
        {
            var floor = Floors().Find("Floor_" + room);
            return floor != null && floor.TryGetComponent<Renderer>(out var renderer) ? renderer.bounds.center : fallback;
        }

        private static DirectorSettings LoadSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<DirectorSettings>(SettingsPath);
            if (settings != null) return settings;
            settings = ScriptableObject.CreateInstance<DirectorSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static GameObject Prefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Characters + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + name);
            return prefab;
        }

        private static GameObject GetOrCreate(Scene scene, string name)
        {
            var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
            if (existing != null) return existing;
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            Undo.RegisterCreatedObjectUndo(created, "Create " + name);
            return created;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component =>
            owner.TryGetComponent<T>(out var component) ? component : Undo.AddComponent<T>(owner);
    }
}

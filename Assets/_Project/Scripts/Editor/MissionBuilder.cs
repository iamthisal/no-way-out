using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lockdown.Editor
{
    // Mission step 1: the Storage room is locked, and its keycard waits on a plinth at the back of Stone Age.
    public static class MissionBuilder
    {
        public const string StorageKeyId = "StorageKeycard";
        public const string StorageKeyName = "Storage keycard";
        public const string KeycardPrefabPath = "Assets/_Project/Prefabs/Props/StorageKeycard.prefab";
        public const string ReaderPrefabPath = "Assets/_Project/Prefabs/Props/DoorLock_and_Keycard_Combined.prefab";
        public static readonly string[] StorageDoors = { "Door_D06_Storage", "Door_D07_Storage", "Door_D08_Storage" };
        private const string MaterialFolder = "Assets/_Project/Materials/Missions";
        private const string PropMaterialFolder = "Assets/_Project/Materials/World/Props/Lobby";
        private const string ScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";
        // The nook between Stone Age's west wall and its wall case, out of sight from the Greece / Rome doorway.
        private static readonly Vector3 KeycardSpot = new Vector3(-9.75f, 0f, 28.55f);
        // The card faces out of the nook, toward a player coming round the standing stones.
        private static readonly Vector3 KeycardFacing = new Vector3(0.45f, 0f, -1f);
        private static readonly Vector3 StorageCentre = new Vector3(9.5f, 0f, 1f);
        private const float ReaderHeight = 1.37f;
        // A reader this close to a door's leaf (the World Builder mounted several) belongs to that door.
        private const float ReaderReach = 1.6f;

        private const string AudioFolder = "Assets/_Project/Audio/Missions";
        // Mission step 2: the guard's loop through the public rooms and the back galleries. Egypt will be
        // barred, and the Storage and Security rooms are behind doors, so he walks the dead-end north row
        // out and back. He starts in Central Hall heading north, away from the player in the lobby.
        private static readonly (string room, Vector2 spot)[] GuardRoute =
        {
            // Room centres hold their centrepiece exhibits, so the stops stand beside them.
            ("Central Hall", new Vector2(0f, 1.5f)),
            ("Artifact Exhibition", new Vector2(0f, 11.2f)),
            ("Natural History", new Vector2(5f, 18f)),
            ("Asian Heritage", new Vector2(8.7f, 26f)),
            ("Greece / Rome", new Vector2(0f, 27.7f)),
            ("Stone Age", new Vector2(-6.5f, 23.6f)),
            ("Greece / Rome", new Vector2(0f, 27.7f)),
            ("Asian Heritage", new Vector2(8.7f, 26f)),
            ("Natural History", new Vector2(5f, 18f)),
            ("Artifact Exhibition", new Vector2(-2f, 10f)),
            ("Temporary", new Vector2(-11.7f, 8f)),
            ("Art Gallery", new Vector2(-10f, 1f)),
            ("Lobby", new Vector2(0f, -7.5f)),
        };

        private static readonly Dictionary<string, (Color color, float smoothness, float emission)> Palette =
            new Dictionary<string, (Color, float, float)>
            {
                // A faint glow keeps the card readable in the dim corner it waits in.
                { "Mission_Keycard", (new Color(0.86f, 0.87f, 0.88f), 0.55f, 0.35f) },
                { "Mission_KeycardStripe", (new Color(0.10f, 0.45f, 0.95f), 0.6f, 3f) },
            };

        [MenuItem("Tools/Lockdown/Missions/Build Storage Keycard")]
        public static void BuildKeycard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the keycard.");
            EnsureFolder(MaterialFolder);
            var root = new GameObject("StorageKeycard");
            try
            {
                // A walnut plinth with a brass cap, matching the gallery's display cases. Its front is local +Z.
                var plinth = Slab(root.transform, "Plinth", new Vector3(0f, 0.475f, 0f), new Vector3(0.42f, 0.95f, 0.42f), PropMaterial("Walnut"));
                plinth.AddComponent<BoxCollider>();
                var obstacle = plinth.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                Slab(root.transform, "Cap", new Vector3(0f, 0.97f, 0f), new Vector3(0.46f, 0.04f, 0.46f), PropMaterial("AgedBrass")).AddComponent<BoxCollider>();
                Slab(root.transform, "Holder", new Vector3(0f, 1.005f, 0f), new Vector3(0.15f, 0.03f, 0.05f), PropMaterial("Charcoal"));

                // A large staff keycard standing in the holder, leaning back so it catches the eye from the room.
                var card = new GameObject("Keycard");
                card.transform.SetParent(root.transform, false);
                card.transform.localPosition = new Vector3(0f, 1.012f, 0f);
                card.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                Slab(card.transform, "Body", new Vector3(0f, 0.0375f, 0f), new Vector3(0.12f, 0.075f, 0.005f), MaterialFor("Mission_Keycard"));
                Slab(card.transform, "Stripe", new Vector3(0f, 0.056f, 0.0028f), new Vector3(0.12f, 0.016f, 0.0005f), MaterialFor("Mission_KeycardStripe"));
                // Larger than the card so it is easy to look at from beside the plinth.
                var target = card.AddComponent<BoxCollider>();
                target.center = new Vector3(0f, 0.04f, 0f);
                target.size = new Vector3(0.22f, 0.12f, 0.12f);
                var pickup = card.AddComponent<KeyPickup>();
                var serialized = new SerializedObject(pickup);
                serialized.FindProperty("keyId").stringValue = StorageKeyId;
                serialized.FindProperty("displayName").stringValue = StorageKeyName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, KeycardPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Storage keycard prefab built at " + KeycardPrefabPath + ".");
        }

        [MenuItem("Tools/Lockdown/Missions/Place Storage Keycard and Locks")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == "Missions") { Selection.activeGameObject = existing; return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KeycardPrefabPath) == null) BuildKeycard();
            var reader = AssetDatabase.LoadAssetAtPath<GameObject>(ReaderPrefabPath)
                ?? throw new InvalidOperationException("Missing card reader prefab: " + ReaderPrefabPath);
            Physics.SyncTransforms();
            if (Physics.CheckBox(KeycardSpot + new Vector3(0f, 0.55f, 0f), new Vector3(0.24f, 0.5f, 0.24f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Something is in the way of the keycard plinth at " + KeycardSpot + ".");

            var missions = new GameObject("Missions");
            SceneManager.MoveGameObjectToScene(missions, scene);
            Undo.RegisterCreatedObjectUndo(missions, "Place storage keycard and locks");
            var keycard = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KeycardPrefabPath), scene);
            keycard.transform.SetParent(missions.transform, true);
            keycard.transform.SetPositionAndRotation(KeycardSpot, Quaternion.LookRotation(KeycardFacing));

            var readers = new GameObject("StorageReaders").transform;
            readers.SetParent(missions.transform, false);
            var doors = GameObject.Find("Museum/Doors") ?? throw new InvalidOperationException("Museum/Doors is missing.");
            foreach (var name in StorageDoors)
            {
                var doorRoot = doors.transform.Find(name);
                var door = doorRoot != null ? doorRoot.GetComponentInChildren<Door>(true) : null;
                if (door == null) throw new InvalidOperationException("Missing door " + name + ".");
                // Reuse the readers already beside this door, and add one only where the outside has none.
                var indicators = ReadersBeside(scene, door, out bool outsideCovered);
                if (!outsideCovered) indicators.Add(PlaceReader(reader, readers, door, name));
                var doorLock = door.GetComponent<DoorLock>();
                if (doorLock == null) doorLock = Undo.AddComponent<DoorLock>(door.gameObject);
                var serialized = new SerializedObject(doorLock);
                serialized.FindProperty("keyId").stringValue = StorageKeyId;
                serialized.FindProperty("keyName").stringValue = StorageKeyName;
                var list = serialized.FindProperty("indicators");
                list.arraySize = indicators.Count;
                for (int i = 0; i < indicators.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = indicators[i];
                serialized.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = missions;
            Debug.Log("Storage doors locked; the keycard waits in Stone Age at " + KeycardSpot + ". Save the scene.");
        }

        [MenuItem("Tools/Lockdown/Missions/Place Guard Patrol and Alarm")]
        public static void PlaceGuard()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var guards = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Lockdown.Enemies.WanderingEnemy>(true)).ToArray();
            if (guards.Length != 1) throw new InvalidOperationException("Expected one wandering enemy to become the guard, found " + guards.Length + ".");
            var guard = guards[0];
            var missions = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Missions");
            if (missions == null) throw new InvalidOperationException("Place the Storage keycard and locks first.");

            var stops = new Vector3[GuardRoute.Length];
            for (int i = 0; i < stops.Length; i++)
            {
                var (room, spot) = GuardRoute[i];
                if (!NavMesh.SamplePosition(new Vector3(spot.x, 0f, spot.y), out var hit, 1f, NavMesh.AllAreas))
                    throw new InvalidOperationException("The guard's stop in " + room + " is off the NavMesh.");
                stops[i] = hit.position;
            }
            var path = new NavMeshPath();
            for (int i = 0; i < stops.Length; i++)
            {
                var next = stops[(i + 1) % stops.Length];
                if (!NavMesh.CalculatePath(stops[i], next, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("No walkable path from " + GuardRoute[i].room + " to " + GuardRoute[(i + 1) % stops.Length].room + ".");
            }
            var walker = new SerializedObject(guard);
            var route = walker.FindProperty("patrolRoute");
            route.arraySize = stops.Length;
            for (int i = 0; i < stops.Length; i++) route.GetArrayElementAtIndex(i).vector3Value = stops[i];
            walker.ApplyModifiedProperties();

            var senses = guard.GetComponent<Lockdown.Enemies.GuardSenses>();
            if (senses == null) senses = Undo.AddComponent<Lockdown.Enemies.GuardSenses>(guard.gameObject);
            var whistle = guard.transform.Find("Whistle");
            if (whistle == null)
            {
                whistle = new GameObject("Whistle").transform;
                Undo.RegisterCreatedObjectUndo(whistle.gameObject, "Add guard whistle");
                whistle.SetParent(guard.transform, false);
                whistle.localPosition = new Vector3(0f, 1.55f, 0.2f);
                var source = whistle.gameObject.AddComponent<AudioSource>();
                source.clip = Clip("GuardWhistle");
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 3f;
                source.maxDistance = 30f;
            }
            var serializedSenses = new SerializedObject(senses);
            serializedSenses.FindProperty("whistle").objectReferenceValue = whistle.GetComponent<AudioSource>();
            serializedSenses.ApplyModifiedProperties();

            var alarm = missions.GetComponentInChildren<MuseumAlarm>(true);
            if (alarm == null)
            {
                var holder = new GameObject("MuseumAlarm");
                Undo.RegisterCreatedObjectUndo(holder, "Add museum alarm");
                holder.transform.SetParent(missions.transform, false);
                alarm = holder.AddComponent<MuseumAlarm>();
                var siren = holder.AddComponent<AudioSource>();
                siren.clip = Clip("AlarmSiren");
                siren.loop = true;
                siren.playOnAwake = false;
                siren.spatialBlend = 0f;
                siren.volume = 0.35f;
                var serializedAlarm = new SerializedObject(alarm);
                serializedAlarm.FindProperty("siren").objectReferenceValue = siren;
                serializedAlarm.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = guard.gameObject;
            Debug.Log("The guard now patrols " + stops.Length + " stops from Central Hall to Stone Age and back through the west galleries. Save the scene.");
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Generate " + name + ".wav with Blender/Missions/generate_audio.py first.");
            return clip;
        }

        private static List<Renderer> ReadersBeside(Scene scene, Door door, out bool outsideCovered)
        {
            var bounds = LeafBounds(door);
            Vector3 outward = Outward(bounds);
            outsideCovered = false;
            var found = new List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (light.name != "DoorLock_GreenIndicator") continue;
                    Vector3 offset = light.transform.position - bounds.center;
                    offset.y = 0f;
                    if (offset.magnitude > ReaderReach) continue;
                    found.Add(light);
                    if (Vector3.Dot(offset, outward) > 0f) outsideCovered = true;
                }
            return found;
        }

        private static Bounds LeafBounds(Door door)
        {
            var leaf = door.GetComponent<BoxCollider>();
            if (leaf == null) throw new InvalidOperationException(door.transform.parent.name + " has no leaf collider.");
            return leaf.bounds;
        }

        // The horizontal wall normal pointing out of the Storage room.
        private static Vector3 Outward(Bounds bounds)
        {
            Vector3 outward = bounds.size.z > bounds.size.x ? Vector3.right : Vector3.forward;
            return Vector3.Dot(bounds.center - StorageCentre, outward) < 0f ? -outward : outward;
        }

        // A card reader on the corridor side of a Storage door, just past the latch edge of the closed leaf.
        private static Renderer PlaceReader(GameObject prefab, Transform parent, Door door, string name)
        {
            var bounds = LeafBounds(door);
            bool wallAlongZ = bounds.size.z > bounds.size.x;
            Vector3 along = wallAlongZ ? Vector3.forward : Vector3.right;
            Vector3 outward = Outward(bounds);
            // The latch edge is the end of the leaf away from the hinge.
            if (Vector3.Dot(bounds.center - door.transform.position, along) < 0f) along = -along;
            float halfLength = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(along.x), 0f, Mathf.Abs(along.z)));
            Vector3 position = bounds.center + along * (halfLength + 0.25f);
            // The leaf sits in the middle of a 0.2 m wall; the reader's back goes on the outer face.
            position = new Vector3(position.x, ReaderHeight, position.z) + outward * 0.105f
                - outward * Vector3.Dot(bounds.center - door.transform.position, outward);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            instance.name = "Reader_" + name.Replace("Door_", "");
            instance.transform.SetParent(parent, true);
            // The reader's face is its local -Z.
            instance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-outward));
            return Indicator(instance.transform);
        }

        private static Renderer Indicator(Transform reader) =>
            reader.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name.Contains("Indicator"))
            ?? throw new InvalidOperationException(reader.name + " has no indicator light.");

        private static GameObject Slab(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            Object.DestroyImmediate(slab.GetComponent<Collider>());
            slab.transform.SetParent(parent, false);
            slab.transform.localPosition = position;
            slab.transform.localScale = size;
            slab.GetComponent<Renderer>().sharedMaterial = material;
            return slab;
        }

        private static Material PropMaterial(string name)
        {
            string path = PropMaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("Missing museum material " + path + ".");
            return material;
        }

        private static Material MaterialFor(string name)
        {
            if (!Palette.TryGetValue(name, out var spec)) throw new InvalidOperationException("Unknown mission material: " + name);
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? throw new InvalidOperationException("URP Lit shader is missing.");
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", spec.color.gamma);
            material.SetFloat("_Smoothness", spec.smoothness);
            if (spec.emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", spec.color * spec.emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

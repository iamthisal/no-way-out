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
    // The museum's mission set-up: the staff keycard, the door locks, the guard's patrol, the alarm and the
    // Security console.
    public static class MissionBuilder
    {
        public const string StaffKeyId = "StaffKeycard";
        public const string StaffKeyName = "staff keycard";
        public const string KeycardPrefabPath = "Assets/_Project/Prefabs/Props/StaffKeycard.prefab";
        public const string ReaderPrefabPath = "Assets/_Project/Prefabs/Props/DoorLock_and_Keycard_Combined.prefab";
        private const string MaterialFolder = "Assets/_Project/Materials/Missions";
        private const string PropMaterialFolder = "Assets/_Project/Materials/World/Props/Lobby";
        private const string ScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";
        // The nook between Stone Age's west wall and its wall case, out of sight from the Greece / Rome doorway.
        private static readonly Vector3 KeycardSpot = new Vector3(-9.75f, 0f, 28.55f);
        // The card faces out of the nook, toward a player coming round the standing stones.
        private static readonly Vector3 KeycardFacing = new Vector3(0.45f, 0f, -1f);

        public enum LockKind { None, Keycard, Breakable, Sealed }
        // Every mission door, with a point on the side its lock keeps the player out of; the reader that shows
        // the lock goes on the other side. Storage is open, and its back doors into the Staff Corridor take the
        // keycard. From the corridor the hammer breaks into the Security room. Its lobby door stays sealed so
        // the keycard cannot be skipped.
        public static readonly (string door, LockKind kind, Vector3 lockedSide)[] DoorLocks =
        {
            ("Door_D06_Storage", LockKind.None, Vector3.zero),
            ("Door_D07_Storage", LockKind.Keycard, new Vector3(15.5f, 0f, 1f)),
            ("Door_D08_Storage", LockKind.Keycard, new Vector3(9.5f, 0f, -4.5f)),
            ("Door_D09_Security", LockKind.Breakable, new Vector3(8f, 0f, -8.5f)),
            ("Door_D10_Security", LockKind.Sealed, new Vector3(8f, 0f, -8.5f)),
        };
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
                { "Mission_RobotShell", (new Color(0.82f, 0.84f, 0.86f), 0.7f, 0f) },
                { "Mission_RobotTrim", (new Color(0.92f, 0.52f, 0.08f), 0.5f, 0f) },
                { "Mission_RobotHead", (new Color(0.12f, 0.13f, 0.15f), 0.6f, 0f) },
                { "Mission_RobotEye", (new Color(0.1f, 0.4f, 1f), 0.8f, 2f) },
                { "Mission_RobotPad", (new Color(0.05f, 0.35f, 0.45f), 0.4f, 0.8f) },
            };

        // Mission step 5: two security robots rest on chargers in the Security room, beside the sealed lobby
        // door they use as their bay door. Both docks are clear of the inward swing of the corridor door.
        public const string RobotPrefabPath = "Assets/_Project/Prefabs/Characters/SecurityRobot.prefab";
        private const string BayDoor = "Door_D10_Security";
        private static readonly Vector3[] RobotDocks = { new Vector3(6f, 0f, -7.3f), new Vector3(6f, 0f, -8.9f) };

        [MenuItem("Tools/Lockdown/Missions/Build Staff Keycard")]
        public static void BuildKeycard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the keycard.");
            EnsureFolder(MaterialFolder);
            var root = new GameObject("StaffKeycard");
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
                serialized.FindProperty("keyId").stringValue = StaffKeyId;
                serialized.FindProperty("displayName").stringValue = StaffKeyName;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, KeycardPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Staff keycard prefab built at " + KeycardPrefabPath + ".");
        }

        // Safe to run again: it places the keycard if it is missing and brings every door lock in line with DoorLocks.
        [MenuItem("Tools/Lockdown/Missions/Place Staff Keycard and Door Locks")]
        public static void Place()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KeycardPrefabPath) == null) BuildKeycard();
            var missions = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Missions");
            if (missions == null)
            {
                missions = new GameObject("Missions");
                SceneManager.MoveGameObjectToScene(missions, scene);
                Undo.RegisterCreatedObjectUndo(missions, "Place mission set-up");
            }
            if (missions.GetComponentInChildren<KeyPickup>(true) == null)
            {
                Physics.SyncTransforms();
                if (Physics.CheckBox(KeycardSpot + new Vector3(0f, 0.55f, 0f), new Vector3(0.24f, 0.5f, 0.24f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    throw new InvalidOperationException("Something is in the way of the keycard plinth at " + KeycardSpot + ".");
                var keycard = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(KeycardPrefabPath), scene);
                Undo.RegisterCreatedObjectUndo(keycard, "Place staff keycard");
                keycard.transform.SetParent(missions.transform, true);
                keycard.transform.SetPositionAndRotation(KeycardSpot, Quaternion.LookRotation(KeycardFacing));
            }
            ConfigureLocks(scene, missions.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = missions;
            Debug.Log("The staff keycard waits in Stone Age at " + KeycardSpot + "; mission door locks updated. Save the scene.");
        }

        private static void ConfigureLocks(Scene scene, Transform missions)
        {
            var reader = AssetDatabase.LoadAssetAtPath<GameObject>(ReaderPrefabPath)
                ?? throw new InvalidOperationException("Missing card reader prefab: " + ReaderPrefabPath);
            var readers = missions.Find("DoorReaders");
            // Scenes from the first version of this step kept their readers in a Storage-only group.
            var legacy = missions.Find("StorageReaders");
            if (readers == null && legacy != null)
            {
                Undo.RecordObject(legacy.gameObject, "Rename door readers");
                legacy.name = "DoorReaders";
                readers = legacy;
            }
            if (readers == null)
            {
                readers = new GameObject("DoorReaders").transform;
                Undo.RegisterCreatedObjectUndo(readers.gameObject, "Add door readers");
                readers.SetParent(missions, false);
            }
            var doors = GameObject.Find("Museum/Doors") ?? throw new InvalidOperationException("Museum/Doors is missing.");
            // Unlocked doors first, so a reader placed for an old lock is gone before readers are counted.
            foreach (var (name, kind, lockedSide) in DoorLocks.OrderBy(entry => entry.kind != LockKind.None))
            {
                var doorRoot = doors.transform.Find(name);
                var door = doorRoot != null ? doorRoot.GetComponentInChildren<Door>(true) : null;
                if (door == null) throw new InvalidOperationException("Missing door " + name + ".");
                var doorLock = door.GetComponent<DoorLock>();
                if (kind == LockKind.None)
                {
                    if (doorLock != null) Undo.DestroyObjectImmediate(doorLock);
                    var added = readers.Find("Reader_" + name.Replace("Door_", ""));
                    if (added != null) Undo.DestroyObjectImmediate(added.gameObject);
                    continue;
                }
                // Reuse the readers already beside this door, and add one only where the near side has none.
                var indicators = ReadersBeside(scene, door, lockedSide, out bool nearSideCovered);
                if (!nearSideCovered) indicators.Add(PlaceReader(reader, readers, door, name, lockedSide));
                if (doorLock == null) doorLock = Undo.AddComponent<DoorLock>(door.gameObject);
                var serialized = new SerializedObject(doorLock);
                serialized.FindProperty("keyId").stringValue = kind == LockKind.Keycard ? StaffKeyId : "";
                serialized.FindProperty("keyName").stringValue = kind == LockKind.Keycard ? StaffKeyName : "";
                serialized.FindProperty("breakable").boolValue = kind == LockKind.Breakable;
                var list = serialized.FindProperty("indicators");
                list.arraySize = indicators.Count;
                for (int i = 0; i < indicators.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = indicators[i];
                serialized.ApplyModifiedProperties();
            }
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

        // The Security room's desk computer switches the alarm system off for good. The skeleton archer leaves
        // the museum until the dinosaur-room step brings skeletons back.
        [MenuItem("Tools/Lockdown/Missions/Place Security Console")]
        public static void PlaceSecurityConsole()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var monitor = GameObject.Find("Museum/Decoration/Decor_Security/P_Security_DeskMonitor")
                ?? throw new InvalidOperationException("The Security room's desk monitor is missing.");
            if (monitor.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(monitor);
            var console = monitor.GetComponent<AlarmConsole>();
            if (console == null) console = Undo.AddComponent<AlarmConsole>(monitor);
            var screen = monitor.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "M_Museum_DeepBlue")
                ?? throw new InvalidOperationException("The desk monitor has no screen.");
            var serializedConsole = new SerializedObject(console);
            serializedConsole.FindProperty("screen").objectReferenceValue = screen;
            serializedConsole.ApplyModifiedProperties();

            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<Lockdown.Enemies.SkeletonBody>(true) != null)
                {
                    Debug.Log("Removed " + root.name + " from the museum until the dinosaur-room step.");
                    Undo.DestroyObjectImmediate(root);
                }
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = monitor;
            Debug.Log("The Security desk computer now switches the alarm system off for good. Save the scene.");
        }

        // The pistol waits in the Security room locker; the museum's player starts without it.
        [MenuItem("Tools/Lockdown/Missions/Place Pistol Locker")]
        public static void PlacePistolLocker()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var locker = GameObject.Find("Museum/FixedCover/Props_Security/P_Security_Locker")
                ?? throw new InvalidOperationException("The Security room's locker is missing.");
            if (locker.GetComponent<Collider>() == null) throw new InvalidOperationException("The locker needs a collider to be looked at.");
            if (locker.GetComponent<PistolPickup>() == null) Undo.AddComponent<PistolPickup>(locker);
            var players = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Lockdown.Player.PlayerEquipment>(true)).ToArray();
            if (players.Length != 1) throw new InvalidOperationException("Expected one player in the museum, found " + players.Length + ".");
            var equipment = new SerializedObject(players[0]);
            equipment.FindProperty("ownsPistol").boolValue = false;
            equipment.FindProperty("startWithPistol").boolValue = false;
            equipment.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = locker;
            Debug.Log("The pistol now waits in the Security room locker. Save the scene.");
        }

        [MenuItem("Tools/Lockdown/Missions/Build Security Robot")]
        public static void BuildRobot()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the robot.");
            EnsureFolder(MaterialFolder);
            var root = new GameObject("SecurityRobot");
            try
            {
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = 0.32f;
                agent.height = 0.95f;
                agent.speed = 2.6f;
                agent.angularSpeed = 300f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.3f;
                var health = root.AddComponent<Lockdown.Combat.Health>();
                var serializedHealth = new SerializedObject(health);
                // Three pistol shots (25 each), or one hammer blow.
                serializedHealth.FindProperty("maxHealth").floatValue = 75f;
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
                var hitBox = root.AddComponent<SphereCollider>();
                hitBox.center = new Vector3(0f, 0.45f, 0f);
                hitBox.radius = 0.42f;

                var belly = new GameObject("Belly").transform;
                belly.SetParent(root.transform, false);
                belly.localPosition = new Vector3(0f, 0.35f, 0f);
                Slab(belly, "Shell", Vector3.zero, Vector3.one * 0.7f, MaterialFor("Mission_RobotShell"), PrimitiveType.Sphere);
                // Two painted bands across the belly show it rolling.
                Slab(belly, "BandA", Vector3.zero, new Vector3(0.712f, 0.02f, 0.712f), MaterialFor("Mission_RobotTrim"), PrimitiveType.Cylinder);
                Slab(belly, "BandB", Vector3.zero, new Vector3(0.712f, 0.02f, 0.712f), MaterialFor("Mission_RobotTrim"), PrimitiveType.Cylinder)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var head = new GameObject("Head").transform;
                head.SetParent(root.transform, false);
                head.localPosition = new Vector3(0f, 0.7f, 0f);
                Slab(head, "Dome", Vector3.zero, new Vector3(0.46f, 0.3f, 0.46f), MaterialFor("Mission_RobotHead"), PrimitiveType.Sphere);
                var eye = Slab(head, "Eye", new Vector3(0f, 0.04f, 0.2f), Vector3.one * 0.13f, MaterialFor("Mission_RobotEye"), PrimitiveType.Sphere);
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(head, false);
                muzzle.localPosition = new Vector3(0f, 0.04f, 0.29f);
                var beam = muzzle.gameObject.AddComponent<LineRenderer>();
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.startWidth = beam.endWidth = 0.025f;
                beam.sharedMaterial = LaserMaterial();
                beam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beam.receiveShadows = false;
                beam.enabled = false;

                var laser = root.AddComponent<Lockdown.Enemies.RobotLaser>();
                SetReferences(laser, ("muzzle", muzzle), ("beam", beam), ("eye", eye.GetComponent<Renderer>()),
                    ("zap", Sound(root.transform, "Zap", "RobotLaser", false, 1f)));
                var robot = root.AddComponent<Lockdown.Enemies.SecurityRobot>();
                SetReferences(robot, ("belly", belly), ("head", head),
                    ("hum", Sound(root.transform, "Hum", "RobotHum", true, 0.45f)),
                    ("powerUp", Sound(root.transform, "PowerUp", "RobotPowerUp", false, 0.8f)),
                    ("breakdown", Sound(root.transform, "Breakdown", "RobotBreakdown", false, 1f)));
                EnsureFolder("Assets/_Project/Prefabs/Characters");
                PrefabUtility.SaveAsPrefabAsset(root, RobotPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("Security robot prefab built at " + RobotPrefabPath + ".");
        }

        [MenuItem("Tools/Lockdown/Missions/Place Security Robots")]
        public static void PlaceRobots()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            var missions = scene.GetRootGameObjects().FirstOrDefault(r => r.name == "Missions")
                ?? throw new InvalidOperationException("Place the mission set-up and the alarm first.");
            if (missions.GetComponentInChildren<MuseumAlarm>(true) == null) throw new InvalidOperationException("Place the guard patrol and alarm first.");
            var existing = missions.transform.Find("RobotBay");
            if (existing != null) { Selection.activeGameObject = existing.gameObject; return; }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath) == null) BuildRobot();
            var doorRoot = GameObject.Find("Museum/Doors/" + BayDoor) ?? throw new InvalidOperationException("Missing door " + BayDoor + ".");
            var door = doorRoot.GetComponentInChildren<Door>(true);

            var bay = new GameObject("RobotBay");
            Undo.RegisterCreatedObjectUndo(bay, "Place security robots");
            bay.transform.SetParent(missions.transform, false);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabPath);
            var robots = new List<Lockdown.Enemies.SecurityRobot>();
            for (int i = 0; i < RobotDocks.Length; i++)
            {
                if (!NavMesh.SamplePosition(RobotDocks[i], out var floor, 0.3f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Robot dock " + RobotDocks[i] + " is off the NavMesh.");
                // The NavMesh floats a little above the floor; the charger sits on the floor itself.
                Slab(bay.transform, "Charger_" + (i + 1), new Vector3(RobotDocks[i].x, 0.011f, RobotDocks[i].z), new Vector3(0.8f, 0.01f, 0.8f),
                    MaterialFor("Mission_RobotPad"), PrimitiveType.Cylinder);
                var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                robot.name = "SecurityRobot_" + (i + 1);
                robot.transform.SetParent(bay.transform, true);
                // Facing the bay door, ready to roll out.
                robot.transform.SetPositionAndRotation(floor.position, Quaternion.LookRotation(Vector3.left));
                robots.Add(robot.GetComponent<Lockdown.Enemies.SecurityRobot>());
            }

            // A barrier in the doorway keeps the player out while the door stands open for the robots.
            var leaf = LeafBounds(door);
            var barrier = new GameObject("RobotBarrier");
            barrier.layer = LayerMask.NameToLayer("Ignore Raycast");
            barrier.transform.SetParent(bay.transform, false);
            barrier.transform.position = new Vector3(leaf.center.x, 1.2f, leaf.center.z);
            var block = barrier.AddComponent<BoxCollider>();
            block.size = leaf.size.z > leaf.size.x ? new Vector3(0.3f, 2.4f, leaf.size.z) : new Vector3(leaf.size.x, 2.4f, 0.3f);
            block.enabled = false;

            var robotBay = bay.AddComponent<Lockdown.Enemies.RobotBay>();
            var serialized = new SerializedObject(robotBay);
            serialized.FindProperty("door").objectReferenceValue = door;
            serialized.FindProperty("barrier").objectReferenceValue = block;
            var list = serialized.FindProperty("robots");
            list.arraySize = robots.Count;
            for (int i = 0; i < robots.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = robots[i];
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = bay;
            Debug.Log("Two security robots wait on their chargers in the Security room; " + BayDoor + " is their bay door. Save the scene.");
        }

        private static AudioSource Sound(Transform parent, string name, string clip, bool loop, float volume)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var source = holder.AddComponent<AudioSource>();
            source.clip = Clip(clip);
            source.loop = loop;
            source.volume = volume;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 22f;
            return source;
        }

        private static void SetReferences(Object target, params (string field, Object value)[] fields)
        {
            var serialized = new SerializedObject(target);
            foreach (var (field, value) in fields)
            {
                var property = serialized.FindProperty(field) ?? throw new InvalidOperationException(target.GetType().Name + " has no field " + field);
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material LaserMaterial()
        {
            string path = MaterialFolder + "/Mission_Laser.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? throw new InvalidOperationException("URP Unlit shader is missing.");
            material = new Material(shader) { name = "Mission_Laser" };
            material.SetColor("_BaseColor", new Color(1f, 0.12f, 0.08f));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static AudioClip Clip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".wav");
            if (clip == null) throw new InvalidOperationException("Generate " + name + ".wav with Blender/Missions/generate_audio.py first.");
            return clip;
        }

        private static List<Renderer> ReadersBeside(Scene scene, Door door, Vector3 lockedSide, out bool nearSideCovered)
        {
            var bounds = LeafBounds(door);
            Vector3 nearSide = NearSide(bounds, lockedSide);
            nearSideCovered = false;
            var found = new List<Renderer>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (light.name != "DoorLock_GreenIndicator") continue;
                    Vector3 offset = light.transform.position - bounds.center;
                    offset.y = 0f;
                    if (offset.magnitude > ReaderReach) continue;
                    found.Add(light);
                    if (Vector3.Dot(offset, nearSide) > 0f) nearSideCovered = true;
                }
            return found;
        }

        private static Bounds LeafBounds(Door door)
        {
            var leaf = door.GetComponent<BoxCollider>();
            if (leaf == null) throw new InvalidOperationException(door.transform.parent.name + " has no leaf collider.");
            return leaf.bounds;
        }

        // The horizontal wall normal pointing toward the side the player comes from, away from the locked side.
        private static Vector3 NearSide(Bounds bounds, Vector3 lockedSide)
        {
            Vector3 outward = bounds.size.z > bounds.size.x ? Vector3.right : Vector3.forward;
            return Vector3.Dot(bounds.center - lockedSide, outward) < 0f ? -outward : outward;
        }

        // A card reader on the player's side of a locked door, just past the latch edge of the closed leaf.
        private static Renderer PlaceReader(GameObject prefab, Transform parent, Door door, string name, Vector3 lockedSide)
        {
            var bounds = LeafBounds(door);
            bool wallAlongZ = bounds.size.z > bounds.size.x;
            Vector3 along = wallAlongZ ? Vector3.forward : Vector3.right;
            Vector3 outward = NearSide(bounds, lockedSide);
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

        private static GameObject Slab(Transform parent, string name, Vector3 position, Vector3 size, Material material,
            PrimitiveType shape = PrimitiveType.Cube)
        {
            var slab = GameObject.CreatePrimitive(shape);
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

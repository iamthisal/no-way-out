using System;
using Lockdown.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Lockdown.Editor
{
    /// <summary>Gives the boundary doors D00 (entrance), D19 (courtyard) and D20 (exit) somewhere to lead.</summary>
    public static class BoundaryDoorBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Museum/Museum_Main.unity";
        private const string FloorMaterial = "Assets/_Project/Materials/World/Floors/Lobby and CentralHall and Artifact and Art and StaffCorridor and Exit.mat";
        private const string CeilingMaterial = "Assets/_Project/Materials/World/Architecture/M_Ceiling_WhitePlaster.mat";
        // Existing ~4 m wall finishes, reused so each vestibule matches the room its door opens from.
        private const string EntranceWallMaterial = "Assets/_Project/Materials/World/Walls/Lobby/Wall_02_End_North.mat";
        private const string ExitWallMaterial = "Assets/_Project/Materials/World/Walls/Exit/Wall_37_Solid_East.mat";
        private const float RoomSize = 4f;
        private const float WallHeight = 3.5f;
        private const float Thickness = 0.2f;

        [MenuItem("Tools/Lockdown/Build Boundary Door Rooms")]
        public static void BuildRooms()
        {
            var scene = MuseumScene();
            var museum = Require(scene.GetRootGameObjects(), "Museum");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build boundary door rooms");
            // D00 is in the Z=-11 wall; its threshold stone covers the floor to Z=-11.24.
            BuildVestibule(museum, "EntranceVestibule", new Vector3(0f, 0f, -13f), -1f, 0.24f, EntranceWallMaterial);
            // D20 (X=14.5..16.5) is in the Z=12 wall. The room sits flush with the X=17 facade and
            // leaves space west of the hinge for the door leaf when it is swung open.
            BuildVestibule(museum, "ExitVestibule", new Vector3(15f, 0f, 14f), 1f, 0f, ExitWallMaterial);
            BuildCourtyard(museum);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Boundary door rooms built. Rebake the museum NavMesh, then save the scene.");
        }

        [MenuItem("Tools/Lockdown/Configure Boundary Doors")]
        public static void ConfigureDoors()
        {
            var scene = MuseumScene();
            var museum = Require(scene.GetRootGameObjects(), "Museum");
            var windows = Require(museum, "Windows");
            var doors = Require(museum, "Doors");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure boundary doors");

            // D00: the palace double door's leaves swing south into the entrance vestibule.
            var entrance = windows.Find("Entrance_ClosedGlassPlaceholder");
            if (entrance != null)
            {
                var model = Require(Require(entrance, "Visual"), "Entrance_PalaceDoubleDoor");
                // Unpack the model instance so its leaves can move beneath hinges while the surround stays.
                if (PrefabUtility.IsPartOfPrefabInstance(model))
                    PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(model.gameObject),
                        PrefabUnpackMode.Completely, InteractionMode.UserAction);
                var root = DoorRoot(doors, "Door_D00_Entrance", new Vector3(0f, 1.25f, -11f));
                Hinge(root, "Hinge_Left", Require(model, "LeftLeaf"), 1f, 95f);
                Hinge(root, "Hinge_Right", Require(model, "RightLeaf"), -1f, -95f);
                var visuals = DoorRoot(root, "Visuals", root.position);
                Undo.SetTransformParent(model, visuals, "Keep door surround fixed");
                Undo.SetTransformParent(Require(entrance, "Entrance_StoneThreshold"), visuals, "Keep door threshold fixed");
                Undo.DestroyObjectImmediate(entrance.gameObject);
            }

            // D19: the glass pane swings south into the Exhibition hall, clear of the courtyard planter.
            var courtyard = windows.Find("Courtyard_LockedGlassDoor");
            if (courtyard != null)
            {
                var root = DoorRoot(doors, "Door_D19_Courtyard", new Vector3(0f, 1.25f, 14f));
                Undo.RecordObject(courtyard.gameObject, "Rename courtyard door");
                courtyard.name = "Courtyard_GlassDoor";
                Hinge(root, "Hinge", courtyard, 1f, 95f);
            }

            // D20: the exit door is one model across both leaves, so it swings as a single panel
            // north into the exit vestibule; the Exit room side has its electrical panel and extinguisher.
            var exit = windows.Find("Exit_ClosedDoorPlaceholder");
            if (exit != null)
            {
                var root = DoorRoot(doors, "Door_D20_Exit", new Vector3(15.5f, 1.25f, 12f));
                Hinge(root, "Hinge", Require(exit, "Exit_MuseumDoubleDoor"), 1f, -95f);
                Undo.DestroyObjectImmediate(exit.gameObject);
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Boundary doors configured. Rebake the museum NavMesh, then save the scene.");
        }

        private static Transform DoorRoot(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go.transform;
        }

        // Matches DoorBuilder: an unscaled hinge at the panel edge carries the panel, a box collider,
        // a carving NavMeshObstacle and the Door. extent is +1 when the panel lies east of its hinge.
        private static void Hinge(Transform root, string name, Transform panel, float extent, float openAngle)
        {
            var bounds = Bounds(panel);
            var hinge = DoorRoot(root, name, new Vector3(extent > 0f ? bounds.min.x : bounds.max.x,
                bounds.center.y, bounds.center.z));
            Undo.SetTransformParent(panel, hinge, "Attach moving door panel");
            if (panel.parent != hinge) throw new InvalidOperationException(panel.name + " could not be attached to its hinge.");

            foreach (var part in panel.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(part.gameObject, "Make door movable");
                GameObjectUtility.SetStaticEditorFlags(part.gameObject, 0);
                // The NavMesh bakes WorldSolid colliders; a moving panel must not be baked shut.
                part.gameObject.layer = 0;
                foreach (var collider in part.GetComponents<Collider>()) Undo.DestroyObjectImmediate(collider);
            }

            var box = Undo.AddComponent<BoxCollider>(hinge.gameObject);
            box.center = new Vector3(extent * bounds.size.x * 0.5f, 0f, 0f);
            box.size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(0.08f, bounds.size.z));
            var obstacle = Undo.AddComponent<NavMeshObstacle>(hinge.gameObject);
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = box.center;
            obstacle.size = box.size;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            var door = new SerializedObject(Undo.AddComponent<Door>(hinge.gameObject));
            door.FindProperty("openAngle").floatValue = openAngle;
            door.ApplyModifiedPropertiesWithoutUndo();
        }

        // A 4 x 4 m room centred on (centre.x, centre.z); direction is -1 when it extends south of its door wall.
        private static void BuildVestibule(Transform museum, string room, Vector3 centre, float direction,
            float floorInset, string wallMaterial)
        {
            var floors = Require(museum, "Floors");
            if (floors.Find("Floor_" + room) != null) return;
            float half = RoomSize * 0.5f;
            float farZ = centre.z + direction * half;
            var wall = Load(wallMaterial);

            float floorDepth = RoomSize - floorInset;
            Block("Floor_" + room, floors, new Vector3(centre.x, -Thickness * 0.5f, centre.z + direction * floorInset * 0.5f),
                new Vector3(RoomSize, Thickness, floorDepth), Load(FloorMaterial));
            Block("Ceiling_" + room, Require(museum, "Ceilings"), new Vector3(centre.x, WallHeight + Thickness * 0.5f, centre.z),
                new Vector3(RoomSize + Thickness, Thickness, RoomSize + Thickness), Load(CeilingMaterial));

            var walls = Require(museum, "Walls");
            Block("Wall_" + room + "_West", walls, new Vector3(centre.x - half, WallHeight * 0.5f, centre.z),
                new Vector3(Thickness, WallHeight, RoomSize), wall);
            Block("Wall_" + room + "_East", walls, new Vector3(centre.x + half, WallHeight * 0.5f, centre.z),
                new Vector3(Thickness, WallHeight, RoomSize), wall);
            Block("Wall_" + room + "_Far", walls, new Vector3(centre.x, WallHeight * 0.5f, farZ),
                new Vector3(RoomSize + Thickness, WallHeight, Thickness), wall);

            // The museum lights are baked; this realtime light works before the next lighting bake.
            var group = new GameObject(room);
            Undo.RegisterCreatedObjectUndo(group, "Create vestibule lighting");
            group.transform.SetParent(Require(museum, "Lighting"), false);
            var lightObject = new GameObject("Light_" + room);
            Undo.RegisterCreatedObjectUndo(lightObject, "Create vestibule light");
            lightObject.transform.SetParent(group.transform, false);
            lightObject.transform.position = new Vector3(centre.x, WallHeight - 0.4f, centre.z);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = new Color(1f, 0.945f, 0.863f);
            light.intensity = 2f;
            light.range = 5.5f;
            light.shadows = LightShadows.None;
        }

        // The walled courtyard keeps its visual ground and planting; this adds collision only.
        private static void BuildCourtyard(Transform museum)
        {
            var floors = Require(museum, "Floors");
            if (floors.Find("Floor_Courtyard") != null) return;
            var decoration = Require(Require(museum, "Decoration"), "CourtYard");
            var ground = Require(decoration, "Courtyard_VisualGround").GetComponent<Renderer>().bounds;
            BoxBlocker("Floor_Courtyard", floors, ground.center, ground.size);

            var props = Require(museum, "FixedCover").Find("Props_Courtyard");
            if (props == null)
            {
                var propsObject = new GameObject("Props_Courtyard");
                Undo.RegisterCreatedObjectUndo(propsObject, "Create courtyard props");
                props = propsObject.transform;
                props.SetParent(Require(museum, "FixedCover"), false);
            }

            FitPlanterCollider(museum);
            var trunk = Require(Require(decoration, "Trees"), "tree1").position;
            CapsuleBlocker("Courtyard_Tree_Blocker", props, new Vector3(trunk.x, 0f, trunk.z), 0.3f, 2.5f);
            // Shrink the leafy bounds slightly so the blockers follow the solid parts.
            var bush = Bounds(Require(Require(decoration, "Trees"), "tree2"));
            BoxBlocker("Courtyard_Bush_Blocker", props, bush.center, Vector3.Scale(bush.size, new Vector3(0.85f, 1f, 0.85f)));
            var rock = Bounds(Require(decoration, "moss rock"));
            BoxBlocker("Courtyard_Rock_Blocker", props, rock.center, Vector3.Scale(rock.size, new Vector3(0.9f, 1f, 0.9f)));
        }

        /// <summary>
        /// Gives the courtyard's large flower vase (maceta) an exact mesh collider and removes the
        /// old capsule blocker. A capsule cannot be shorter than its width, so the 2.3 m wide pot's
        /// capsule rose to 2.35 m and stopped shots in the air about 1 m above the 1.34 m pot. The
        /// plant's leaves have no collider, so shots pass through them as before.
        /// </summary>
        public static void FitPlanterCollider(Transform museum)
        {
            var decoration = Require(Require(museum, "Decoration"), "CourtYard");
            var pot = Require(Require(decoration, "Saintpaulia_Flowers"), "maceta");
            var filter = pot.GetComponent<MeshFilter>();
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null) throw new InvalidOperationException("The courtyard vase has no mesh.");
            var oldBlocker = Require(museum, "FixedCover").Find("Props_Courtyard/Courtyard_Planter_Blocker");
            if (oldBlocker != null) Undo.DestroyObjectImmediate(oldBlocker.gameObject);
            PropColliderBuilder.EnsureReadable(mesh);
            var collider = pot.GetComponent<MeshCollider>();
            if (collider == null) collider = Undo.AddComponent<MeshCollider>(pot.gameObject);
            Undo.RecordObject(collider, "Fit courtyard vase collider");
            collider.sharedMesh = mesh;
            collider.convex = false;
            Undo.RecordObject(pot.gameObject, "Fit courtyard vase collider");
            pot.gameObject.layer = WorldSolidLayer();
        }

        private static void Block(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(block, "Create " + name);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = size;
            block.layer = WorldSolidLayer();
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(block, StaticEditorFlags.ContributeGI);
        }

        private static void BoxBlocker(string name, Transform parent, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name, typeof(BoxCollider));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.layer = WorldSolidLayer();
            go.GetComponent<BoxCollider>().size = size;
        }

        private static void CapsuleBlocker(string name, Transform parent, Vector3 basePosition, float radius, float height)
        {
            var go = new GameObject(name, typeof(CapsuleCollider));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = basePosition;
            go.layer = WorldSolidLayer();
            var capsule = go.GetComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = Mathf.Max(height, radius * 2f);
            capsule.center = Vector3.up * capsule.height * 0.5f;
        }

        private static Bounds Bounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static int WorldSolidLayer()
        {
            int layer = LayerMask.NameToLayer("WorldSolid");
            if (layer < 0) throw new InvalidOperationException("The WorldSolid layer is missing.");
            return layer;
        }

        private static Material Load(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) throw new InvalidOperationException("Missing material: " + path);
            return material;
        }

        private static Scene MuseumScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath)
                throw new InvalidOperationException("Open Museum_Main in Edit mode first.");
            return scene;
        }

        private static Transform Require(GameObject[] roots, string name)
        {
            foreach (var root in roots)
                if (root.name == name) return root.transform;
            throw new InvalidOperationException("Missing scene root: " + name);
        }

        private static Transform Require(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) throw new InvalidOperationException("Missing " + parent.name + "/" + name);
            return child;
        }
    }
}

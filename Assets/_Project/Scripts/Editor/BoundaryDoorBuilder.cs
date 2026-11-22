using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
            // D20 is in the Z=12 wall.
            BuildVestibule(museum, "ExitVestibule", new Vector3(15.5f, 0f, 14f), 1f, 0f, ExitWallMaterial);
            BuildCourtyard(museum);
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Boundary door rooms built. Rebake the museum NavMesh, then save the scene.");
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

            var planter = Bounds(Require(Require(decoration, "Saintpaulia_Flowers"), "maceta"));
            var trunk = Require(Require(decoration, "Trees"), "tree1").position;
            CapsuleBlocker("Courtyard_Planter_Blocker", props,
                new Vector3(planter.center.x, 0f, planter.center.z), planter.extents.x, planter.size.y);
            CapsuleBlocker("Courtyard_Tree_Blocker", props, new Vector3(trunk.x, 0f, trunk.z), 0.3f, 2.5f);
            // Shrink the leafy bounds slightly so the blockers follow the solid parts.
            var bush = Bounds(Require(Require(decoration, "Trees"), "tree2"));
            BoxBlocker("Courtyard_Bush_Blocker", props, bush.center, Vector3.Scale(bush.size, new Vector3(0.85f, 1f, 0.85f)));
            var rock = Bounds(Require(decoration, "moss rock"));
            BoxBlocker("Courtyard_Rock_Blocker", props, rock.center, Vector3.Scale(rock.size, new Vector3(0.9f, 1f, 0.9f)));
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

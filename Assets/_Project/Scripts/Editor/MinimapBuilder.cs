using System.IO;
using Lockdown.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lockdown.Editor
{
    /// <summary>
    /// Bakes a flat floor plan of the museum from its WorldSolid colliders and wires the
    /// top-left minimap to the player. Re-run after changing museum geometry.
    /// </summary>
    public static class MinimapBuilder
    {
        private const string TexturePath = "Assets/_Project/Textures/UI/MuseumMinimap.png";
        private const string MinimapObjectName = "HUD_Minimap";
        private const float PixelsPerMetre = 10f;
        private const float Padding = 0.5f;
        // Walls reach the ceiling, furniture does not: probe at two heights to tell them apart.
        private const float WallProbeHeight = 2.4f;
        private const float PropProbeHeight = 0.9f;

        private static readonly Color32 Outside = new Color32(0, 0, 0, 0);
        private static readonly Color32 FloorColor = new Color32(52, 60, 72, 235);
        private static readonly Color32 PropColor = new Color32(104, 112, 126, 245);
        private static readonly Color32 WallColor = new Color32(222, 222, 216, 255);

        [MenuItem("Tools/Lockdown/Build Museum Minimap")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Exit Play mode before building the minimap.");
            var museum = GameObject.Find("Museum");
            if (museum == null) throw new System.InvalidOperationException("Open Museum_Main first: no 'Museum' root found.");
            var player = Object.FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null) throw new System.InvalidOperationException("No FirstPersonController in the open scene.");

            int mask = LayerMask.GetMask("WorldSolid");
            Bounds bounds = SolidBounds(museum, mask);
            Vector2 min = new Vector2(bounds.min.x - Padding, bounds.min.z - Padding);
            Vector2 max = new Vector2(bounds.max.x + Padding, bounds.max.z + Padding);
            Texture2D plan = BakeFloorPlan(min, max, mask);

            var minimapObject = GameObject.Find(MinimapObjectName);
            if (minimapObject == null)
            {
                minimapObject = new GameObject(MinimapObjectName);
                Undo.RegisterCreatedObjectUndo(minimapObject, "Build Museum Minimap");
            }
            var minimap = minimapObject.GetComponent<PlayerMinimap>();
            if (minimap == null) minimap = Undo.AddComponent<PlayerMinimap>(minimapObject);
            Undo.RecordObject(minimap, "Build Museum Minimap");
            minimap.Configure(player.transform, plan, min, max);
            EditorUtility.SetDirty(minimap);
            EditorSceneManager.MarkSceneDirty(minimapObject.scene);
            Debug.Log($"Minimap baked to {TexturePath} ({plan.width}x{plan.height}) for X {min.x:F2}..{max.x:F2}, Z {min.y:F2}..{max.y:F2}. Save the scene.");
        }

        private static Bounds SolidBounds(GameObject museum, int mask)
        {
            Bounds bounds = default;
            bool any = false;
            foreach (var collider in museum.GetComponentsInChildren<Collider>())
            {
                if ((mask & (1 << collider.gameObject.layer)) == 0 || collider.isTrigger) continue;
                if (!any) { bounds = collider.bounds; any = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (!any) throw new System.InvalidOperationException("The museum has no WorldSolid colliders.");
            return bounds;
        }

        private static Texture2D BakeFloorPlan(Vector2 min, Vector2 max, int mask)
        {
            Physics.SyncTransforms();
            int width = Mathf.CeilToInt((max.x - min.x) * PixelsPerMetre);
            int height = Mathf.CeilToInt((max.y - min.y) * PixelsPerMetre);
            float cell = 1f / PixelsPerMetre;
            var half = new Vector3(cell * 0.5f, 0.05f, cell * 0.5f);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float worldX = min.x + (x + 0.5f) * cell;
                float worldZ = min.y + (y + 0.5f) * cell;
                Color32 color = Outside;
                if (Physics.CheckBox(new Vector3(worldX, WallProbeHeight, worldZ), half, Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                    color = WallColor;
                else if (Physics.CheckBox(new Vector3(worldX, PropProbeHeight, worldZ), half, Quaternion.identity, mask, QueryTriggerInteraction.Ignore))
                    color = PropColor;
                else if (Physics.Raycast(new Vector3(worldX, 0.5f, worldZ), Vector3.down, 1f, mask, QueryTriggerInteraction.Ignore))
                    color = FloorColor;
                // Texture rows start at the bottom, so row 0 is the plan's southern edge.
                pixels[y * width + x] = color;
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}

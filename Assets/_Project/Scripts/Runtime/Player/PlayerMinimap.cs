using UnityEngine;

namespace Lockdown.Player
{
    /// <summary>
    /// North-up floor-plan minimap in the top-left corner. The map image stays fixed;
    /// only the player marker moves and turns with the player's heading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerMinimap : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Texture2D floorPlan;
        [Tooltip("World X/Z of the floor plan's bottom-left corner.")]
        [SerializeField] private Vector2 worldMin;
        [Tooltip("World X/Z of the floor plan's top-right corner.")]
        [SerializeField] private Vector2 worldMax = Vector2.one;
        [Tooltip("Map height as a fraction of the screen height.")]
        [SerializeField, Range(0.1f, 0.6f)] private float screenHeightFraction = 0.32f;
        [SerializeField] private float margin = 16f;
        [SerializeField] private float markerSize = 14f;
        [SerializeField] private Color markerColor = new Color(1f, 0.82f, 0.2f);

        private Texture2D marker;

        public Transform Player => player;
        public Texture2D FloorPlan => floorPlan;

        public void Configure(Transform target, Texture2D plan, Vector2 min, Vector2 max)
        {
            player = target;
            floorPlan = plan;
            worldMin = min;
            worldMax = max;
        }

        /// <summary>
        /// Maps a world position to map coordinates in [0,1]: x grows east, y grows north.
        /// Positions outside the plan are clamped to its edge.
        /// </summary>
        public static Vector2 WorldToMap(Vector3 position, Vector2 min, Vector2 max)
        {
            return new Vector2(
                Mathf.Clamp01(Mathf.InverseLerp(min.x, max.x, position.x)),
                Mathf.Clamp01(Mathf.InverseLerp(min.y, max.y, position.z)));
        }

        /// <summary>The map rectangle on screen, in GUI coordinates (origin top-left).</summary>
        public Rect MapRect(float screenHeight)
        {
            float height = Mathf.Max(64f, screenHeight * screenHeightFraction);
            float aspect = (worldMax.x - worldMin.x) / Mathf.Max(0.01f, worldMax.y - worldMin.y);
            return new Rect(margin, margin, height * aspect, height);
        }

        private void Awake()
        {
            if (player == null)
            {
                var controller = FindAnyObjectByType<FirstPersonController>();
                if (controller != null) player = controller.transform;
            }
        }

        private void OnDestroy()
        {
            if (marker != null) Destroy(marker);
        }

        private void OnGUI()
        {
            if (floorPlan == null || player == null || Event.current.type != EventType.Repaint) return;
            Rect map = MapRect(Screen.height);
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;

            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(new Rect(map.x - 4f, map.y - 4f, map.width + 8f, map.height + 8f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(map, floorPlan, ScaleMode.StretchToFill, true);

            // GUI y runs downwards, so north (map y = 1) is at the top of the rectangle.
            Vector2 uv = WorldToMap(player.position, worldMin, worldMax);
            Vector2 centre = new Vector2(map.x + uv.x * map.width, map.y + (1f - uv.y) * map.height);
            if (marker == null) marker = CreateMarker();
            GUIUtility.RotateAroundPivot(player.eulerAngles.y, centre);
            GUI.color = markerColor;
            GUI.DrawTexture(new Rect(centre.x - markerSize * 0.5f, centre.y - markerSize * 0.5f, markerSize, markerSize), marker);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        // A white arrowhead pointing up (north at zero yaw); tinted when drawn.
        private static Texture2D CreateMarker()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "MinimapMarker", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Texture rows start at the bottom, so the tip is at the top row. u is the
                // distance from the centre line; the base has a notch to read as an arrow.
                float v = (y + 0.5f) / size;
                float u = Mathf.Abs((x + 0.5f) / size - 0.5f) * 2f;
                bool inside = u <= 1f - v && v >= 0.35f * (1f - u);
                pixels[y * size + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}

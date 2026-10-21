using System;
using Lockdown.Interfaces;
using Lockdown.Player;
using UnityEngine;

namespace Lockdown.World
{
    /// <summary>A reusable crate with a moving cover and individually collectible contents.</summary>
    [DisallowMultipleComponent]
    public sealed class LootContainer : MonoBehaviour, IInteractable
    {
        public enum ModelKind { CoveredCrate, ExhibitionCrate, StorageCrate, Chest }

        [Serializable]
        public sealed class Contents
        {
            public string itemName = "Stored Relic";
            [Min(1)] public int value = 150;
            public Mesh mesh;
            public Material material;
            public Mesh accentMesh;
            public Material accentMaterial;
        }

        [SerializeField] private ModelKind modelKind;
        [SerializeField] private Material woodMaterial;
        [SerializeField] private Contents[] contents = new Contents[0];
        [SerializeField, Min(1f)] private float degreesPerSecond = 150f;
        private Transform lid;
        private Quaternion closedRotation;
        private Collider closedBlocker;
        private LootItem[] items = new LootItem[0];
        private Collider[] itemColliders = new Collider[0];
        private float currentAngle;
        private const float OpenAngle = 110f;

        public bool IsOpen { get; private set; }
        public bool IsMoving => !Mathf.Approximately(currentAngle, IsOpen ? OpenAngle : 0f);
        public bool IsAccessible => isActiveAndEnabled && IsOpen && !IsMoving;
        public bool IsEmpty => Array.TrueForAll(items, item => item == null || item.IsCollected);
        private LootItem NextItem => Array.Find(items, item => item != null && !item.IsCollected);
        public string InteractionLabel => IsMoving ? (IsOpen ? "Opening crate..." : "Closing crate...") :
            IsAccessible && NextItem != null ? NextItem.InteractionLabel :
            (IsOpen ? "Close " : "Open ") + (IsEmpty ? "empty crate" : "crate");

        private void Awake()
        {
            // Imported storage crates are decorative cover, not loot containers.
            // Remove only this behaviour at runtime; retain the authored visuals and colliders.
            if (modelKind == ModelKind.StorageCrate)
            {
                enabled = false;
                Destroy(this);
                return;
            }

            closedBlocker = GetComponent<Collider>();
            Transform interior;
            Bounds bounds;
            if (modelKind == ModelKind.Chest)
            {
                lid = FindChild("LidPivot");
                interior = lid != null ? lid.parent : transform;
                bounds = new Bounds(new Vector3(0f, 0.275f, 0f), new Vector3(0.7f, 0.45f, 0.5f));
            }
            else if (modelKind != ModelKind.StorageCrate && FindChild("Walnut") is Transform wood)
            {
                interior = wood.parent;
                bool covered = modelKind == ModelKind.CoveredCrate;
                bounds = covered
                    ? new Bounds(new Vector3(-0.008685f, -0.037275f, 0.01124f), new Vector3(0.87345f, 0.92545f, 0.86022f))
                    : new Bounds(new Vector3(0f, -0.02381f, 0.00593f), new Vector3(0.90722f, 0.95238f, 0.86956f));
                lid = CreateLid(interior, bounds, wood.GetComponent<Renderer>().sharedMaterial);
                if (covered)
                {
                    MoveCover("M_Museum_DustCloth");
                    MoveCover("WarmIvory");
                }
                // The source meshes have open tops and inward-facing interior surfaces.
                foreach (var filter in interior.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.transform.IsChildOf(lid) || filter.sharedMesh == null ||
                        filter.GetComponent<Collider>() != null) continue;
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                }
            }
            else
            {
                // Imported storage props are solid meshes; replace them with a matching hollow crate.
                interior = transform;
                var box = GetComponent<BoxCollider>();
                bounds = box != null ? new Bounds(box.center, box.size) : new Bounds(Vector3.zero, Vector3.one);
                foreach (var renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                BuildStorageShell(interior, bounds);
                lid = CreateLid(interior, bounds, woodMaterial);
            }

            if (lid == null)
            {
                Debug.LogError("LootContainer needs a lid: " + name, this);
                enabled = false;
                return;
            }
            closedRotation = lid.localRotation;
            var coin = modelKind == ModelKind.Chest ? FindChild("Gold_Coin") : null;
            if (coin != null) ConfigureExistingCoin(coin);
            else CreateContents(interior, bounds);
            SetContentsAccessible(false);
        }

        private void ConfigureExistingCoin(Transform coin)
        {
            var bounds = new Bounds();
            bool initialized = false;
            foreach (var renderer in coin.GetComponentsInChildren<Renderer>(true))
            {
                var world = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = coin.InverseTransformPoint(world.center + Vector3.Scale(world.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                            (corner & 4) == 0 ? -1 : 1)));
                    if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                    else bounds.Encapsulate(point);
                }
            }
            var collider = coin.GetComponent<BoxCollider>();
            if (collider == null) collider = coin.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            var loot = coin.GetComponent<LootItem>();
            if (loot == null) loot = coin.gameObject.AddComponent<LootItem>();
            loot.ConfigureContainerLoot("Ancient Gold Coin", 500, this);
            items = new[] { loot };
            itemColliders = coin.GetComponentsInChildren<Collider>(true);
        }

        private Transform FindChild(string childName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child != transform && child.name == childName) return child;
            return null;
        }

        private Transform CreateLid(Transform parent, Bounds bounds, Material material)
        {
            var hinge = new GameObject("CrateLidHinge").transform;
            hinge.SetParent(parent, false);
            hinge.localPosition = new Vector3(bounds.center.x, bounds.max.y, bounds.max.z);
            CreateBoard(hinge, "Wooden lid", new Vector3(0f, 0.018f, -bounds.extents.z),
                new Vector3(bounds.size.x, 0.036f, bounds.size.z), material);
            return hinge;
        }

        private void MoveCover(string childName)
        {
            var cover = FindChild(childName);
            if (cover != null) cover.SetParent(lid, true);
        }

        private void BuildStorageShell(Transform parent, Bounds bounds)
        {
            const float thickness = 0.045f;
            var size = bounds.size;
            var center = bounds.center;
            CreateBoard(parent, "Crate floor", new Vector3(center.x, bounds.min.y + thickness * 0.5f, center.z),
                new Vector3(size.x, thickness, size.z), woodMaterial);
            foreach (float side in new[] { -1f, 1f })
            {
                CreateBoard(parent, "Crate side", center + Vector3.right * side * (size.x - thickness) * 0.5f,
                    new Vector3(thickness, size.y, size.z), woodMaterial);
                CreateBoard(parent, "Crate end", center + Vector3.forward * side * (size.z - thickness) * 0.5f,
                    new Vector3(size.x - thickness * 2f, size.y, thickness), woodMaterial);
            }
        }

        private static void CreateBoard(Transform parent, string boardName, Vector3 position, Vector3 size, Material material)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = boardName;
            board.transform.SetParent(parent, false);
            board.transform.localPosition = position;
            board.transform.localScale = size;
            if (material != null) board.GetComponent<Renderer>().sharedMaterial = material;
        }

        private void CreateContents(Transform parent, Bounds bounds)
        {
            items = new LootItem[contents.Length];
            itemColliders = new Collider[contents.Length];
            for (int i = 0; i < contents.Length; i++)
            {
                var spec = contents[i];
                if (spec == null || spec.mesh == null) continue;
                var item = new GameObject(spec.itemName);
                item.transform.SetParent(parent, false);
                var visual = new GameObject("Relic model");
                visual.transform.SetParent(item.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = spec.mesh;
                visual.AddComponent<MeshRenderer>().sharedMaterial = spec.material;
                var meshBounds = spec.mesh.bounds;
                if (spec.accentMesh != null)
                {
                    var accent = new GameObject("Relic trim");
                    accent.transform.SetParent(visual.transform, false);
                    accent.AddComponent<MeshFilter>().sharedMesh = spec.accentMesh;
                    accent.AddComponent<MeshRenderer>().sharedMaterial = spec.accentMaterial;
                    meshBounds.Encapsulate(spec.accentMesh.bounds);
                }
                float cellWidth = bounds.size.x * 0.8f / Mathf.Max(1, contents.Length);
                float scale = Mathf.Min(cellWidth * 0.85f / Mathf.Max(0.001f, meshBounds.size.x),
                    bounds.size.y * 0.75f / Mathf.Max(0.001f, meshBounds.size.y),
                    bounds.size.z * 0.65f / Mathf.Max(0.001f, meshBounds.size.z));
                visual.transform.localScale = Vector3.one * scale;
                visual.transform.localPosition = -meshBounds.center * scale;
                item.transform.localPosition = new Vector3(bounds.center.x + (i - (contents.Length - 1f) * 0.5f) * cellWidth,
                    bounds.min.y + 0.05f + meshBounds.extents.y * scale, bounds.center.z);
                var collider = item.AddComponent<BoxCollider>();
                collider.size = meshBounds.size * scale;
                var loot = item.AddComponent<LootItem>();
                loot.ConfigureContainerLoot(spec.itemName, spec.value, this);
                items[i] = loot;
                itemColliders[i] = collider;
            }
        }

        private void Update()
        {
            currentAngle = Mathf.MoveTowards(currentAngle, IsOpen ? OpenAngle : 0f,
                Mathf.Max(1f, degreesPerSecond) * Time.deltaTime);
            lid.localRotation = closedRotation * Quaternion.Euler(currentAngle, 0f, 0f);
            SetContentsAccessible(IsAccessible);
        }

        private void SetContentsAccessible(bool accessible)
        {
            foreach (var collider in itemColliders) if (collider != null) collider.enabled = accessible;
        }

        public bool TryInteract(GameObject interactor)
        {
            if (!isActiveAndEnabled || lid == null || interactor == null) return false;
            var interaction = interactor.GetComponent<PlayerInteraction>();
            if (interaction == null || interaction.CurrentTarget != (IInteractable)this) return false;
            if (IsMoving) return false;
            // Tall crate walls can hide the contents from a standing player. The open
            // crate also offers its next item, using the same sight/reach checks.
            if (IsAccessible && NextItem != null) return NextItem.TryCollectFromContainer(interactor, this);
            IsOpen = !IsOpen;
            if (closedBlocker != null) closedBlocker.enabled = !IsOpen;
            SetContentsAccessible(false);
            return true;
        }
    }
}

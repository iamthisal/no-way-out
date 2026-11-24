using UnityEngine;

namespace Lockdown.World
{
    [DisallowMultipleComponent]
    public sealed class MuseumMovableProps : MonoBehaviour
    {
        [SerializeField] private BoxCollider doorCrate;
        [SerializeField] private Door storageDoor;
        [SerializeField] private Transform cartModel;
        [SerializeField] private Transform partitionModel;
        [SerializeField] private Material boxMaterial;
        [SerializeField] private AudioClip boxImpact;

        private void Awake()
        {
            if (doorCrate != null && storageDoor != null)
            {
                var crate = doorCrate.gameObject.AddComponent<MovableProp>();
                crate.Configure(45f, 2f, false, null);
                crate.AllowSidewaysPush();
                storageDoor.SetBarricade(doorCrate);
            }
            AddBarricade("cleaning cart", cartModel, new Vector3(16.27f, 0f, 1.85f),
                new Vector3(1.35f, 1.25f, 0.75f), 90f, 22f, 2.8f);
            AddBarricade("exhibition partition", partitionModel, new Vector3(-8.55f, 0f, 6.65f),
                new Vector3(1.95f, 1.95f, 0.52f), 90f, 30f, 2.2f);

            Vector3 boxSize = new Vector3(0.38f, 0.31f, 0.33f);
            GameObject box = CreateProp("empty cardboard box", new Vector3(8.2f, 0f, 0.4f), boxSize);
            AddBoxPanel(box.transform, "Cardboard", Vector3.zero, boxSize, new Color(0.62f, 0.39f, 0.21f));
            AddBoxPanel(box.transform, "Packing tape", Vector3.up * (boxSize.y * 0.5f),
                new Vector3(0.07f, 0.003f, boxSize.z), new Color(0.82f, 0.64f, 0.35f));
            box.AddComponent<MovableProp>().Configure(0.6f, 9f, true, boxImpact);
        }

        private void AddBarricade(string label, Transform source, Vector3 floorPosition, Vector3 size,
            float yaw, float mass, float speed)
        {
            if (source == null) return;
            GameObject prop = CreateProp(label, floorPosition, size);
            var visual = new GameObject("Model").transform;
            visual.SetParent(prop.transform, false);

            // Reuse the art without the original prop's collision, scripts, or baked lighting data.
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var mesh in source.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null || !mesh.TryGetComponent<MeshRenderer>(out var appearance) || !appearance.enabled)
                    continue;

                var copy = new GameObject(mesh.name);
                copy.transform.SetParent(visual, false);
                copy.transform.localPosition = mesh.transform.position - source.position;
                copy.transform.localRotation = mesh.transform.rotation;
                copy.transform.localScale = mesh.transform.lossyScale;
                copy.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var renderer = copy.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = appearance.sharedMaterials;
                if (!hasBounds) bounds = renderer.bounds;
                else bounds.Encapsulate(renderer.bounds);
                hasBounds = true;
            }

            if (!hasBounds)
            {
                Destroy(prop);
                return;
            }

            Vector3 modelSize = bounds.size;
            Vector3 center = bounds.center - prop.transform.position;
            if (modelSize.z > modelSize.x)
            {
                var turn = Quaternion.Euler(0f, 90f, 0f);
                foreach (Transform mesh in visual)
                {
                    mesh.localPosition = turn * mesh.localPosition;
                    mesh.localRotation = turn * mesh.localRotation;
                }
                modelSize = new Vector3(modelSize.z, modelSize.y, modelSize.x);
                center = turn * center;
            }

            modelSize = Vector3.Max(modelSize, Vector3.one * 0.01f);
            visual.localScale = new Vector3(size.x / modelSize.x, size.y / modelSize.y, size.z / modelSize.z);
            visual.localPosition = -Vector3.Scale(center, visual.localScale);
            prop.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            prop.AddComponent<MovableProp>().Configure(mass, speed, false, null);
        }

        private GameObject CreateProp(string label, Vector3 floorPosition, Vector3 size)
        {
            var item = new GameObject(label);
            item.transform.SetParent(transform, false);
            item.transform.position = floorPosition + Vector3.up * (size.y * 0.5f + 0.015f);
            item.AddComponent<BoxCollider>().size = size;
            return item;
        }

        private void AddBoxPanel(Transform parent, string label, Vector3 position, Vector3 size, Color color)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = label;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = position;
            panel.transform.localScale = size;
            Collider collider = panel.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            var renderer = panel.GetComponent<MeshRenderer>();
            if (boxMaterial != null) renderer.sharedMaterial = boxMaterial;
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(tint);
        }
    }
}

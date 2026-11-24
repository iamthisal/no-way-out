using System;
using Lockdown.Combat;
using Lockdown.Interfaces;
using Lockdown.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(Health), typeof(LootInventory))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float reach = 2.5f;
        private const float ChestAimTolerance = 0.2f;
        private FirstPersonController controller;
        private Health health;
        private LootInventory inventory;
        private InteractionFeedback feedback;
        private InputAction interactAction;
        private GUIStyle promptStyle;
        private GUIStyle lootStyle;
        private GUIStyle entryStyle;
        private GUIStyle objectiveStyle;
        private MuseumExit museumExit;
        private bool showInventory;

        public float Reach => reach;
        public bool CanInteract => isActiveAndEnabled && controller != null && controller.enabled &&
            controller.CanUseWeapon && health != null && !health.IsDead;
        public IInteractable CurrentTarget => CanInteract ? FindTarget() : null;

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            health = GetComponent<Health>();
            inventory = GetComponent<LootInventory>();
            feedback = GetComponent<InteractionFeedback>();
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonWest");
        }

        private void OnEnable() { interactAction?.Enable(); }
        private void Start()
        {
            museumExit = Array.Find(FindObjectsByType<MuseumExit>(FindObjectsInactive.Exclude),
                exit => exit.gameObject.scene == gameObject.scene);
        }
        private void OnDisable() { interactAction?.Disable(); showInventory = false; }
        private void OnDestroy() { interactAction?.Dispose(); }

        private void Update()
        {
            if (!CanInteract) { showInventory = false; return; }
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                showInventory = !showInventory;
            if (interactAction.WasPressedThisFrame()) TryInteract();
        }

        public bool TryInteract()
        {
            var target = CurrentTarget;
            if (target == null) return false;
            int previousItemCount = inventory.ItemCount;
            bool succeeded = target.TryInteract(gameObject);
            if (feedback != null)
                feedback.Report(target, succeeded, inventory, previousItemCount);
            return succeeded;
        }

        private IInteractable FindTarget()
        {
            var camera = controller.PlayerCamera;
            if (camera == null) return null;
            var aim = new Ray(camera.transform.position, camera.transform.forward);
            var direct = FirstWorldCollider(aim, reach);
            var target = direct != null ? direct.GetComponentInParent<IInteractable>() : null;
            if (target != null) return target;

            // Give small chests a little aim tolerance, while requiring a clear ray to the chest itself.
            var nearbyHits = Physics.SphereCastAll(aim, ChestAimTolerance, reach, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(nearbyHits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in nearbyHits)
            {
                if (hit.distance <= 0f) continue;
                var chest = hit.collider.GetComponentInParent<LootContainer>();
                if (chest == null || !chest.IsChest || !chest.isActiveAndEnabled) continue;
                var point = Vector3.Lerp(hit.point, hit.collider.bounds.center, 0.02f);
                var direction = point - aim.origin;
                if (direction.sqrMagnitude > reach * reach) continue;
                var visible = FirstWorldCollider(new Ray(aim.origin, direction), reach);
                if (visible != null && visible.transform.IsChildOf(chest.transform)) return chest;
            }
            return null;
        }

        private Collider FirstWorldCollider(Ray ray, float distance)
        {
            var hits = Physics.RaycastAll(ray, distance,
                ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                return hit.collider;
            }
            return null;
        }

        private void OnGUI()
        {
            if (promptStyle == null)
            {
                promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }, wordWrap = true };
                lootStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleRight,
                    normal = { textColor = Color.white } };
                entryStyle = new GUIStyle(lootStyle) { fontSize = 16, wordWrap = true };
                objectiveStyle = new GUIStyle(promptStyle) { fontStyle = FontStyle.Bold };
            }
            float width = Mathf.Min(300f, Screen.width - 48f);
            GUI.Label(new Rect(Screen.width - width - 24f, 20f, width, 32f),
                "Loot " + inventory.ItemCount + "  |  $" + inventory.TotalValue, lootStyle);
            if (!CanInteract) return;
            if (museumExit != null)
            {
                float objectiveWidth = Mathf.Min(460f, Screen.width * 0.48f);
                var objectiveRect = new Rect((Screen.width - objectiveWidth) * 0.5f, 16f, objectiveWidth, 60f);
                GUI.Box(objectiveRect, GUIContent.none);
                GUI.Label(objectiveRect, museumExit.GetObjective(inventory), objectiveStyle);
            }
            var equipment = GetComponent<PlayerEquipment>();
            if (equipment == null || !equipment.IsPistolEquipped)
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            var target = CurrentTarget;
            if (target != null)
            {
                float promptWidth = Mathf.Min(440f, Screen.width - 32f);
                string binding = interactAction.activeControl != null && interactAction.activeControl.device is Gamepad ? "X" : "E";
                var door = target as Door ?? (target as DoorReader)?.Door;
                string label = target is MuseumExit exit ? exit.GetPrompt(inventory) :
                    target is SecurityConsole console ? console.GetPrompt(inventory) :
                    door != null ? door.GetPrompt(inventory) : target.InteractionLabel;
                GUI.Label(new Rect((Screen.width - promptWidth) * 0.5f, Screen.height * 0.5f + 24f, promptWidth, 64f),
                    "[" + binding + "] " + label, promptStyle);
            }
            if (!showInventory) return;
            float y = 60f;
            if (inventory.ItemCount == 0)
                GUI.Label(new Rect(Screen.width - width - 24f, y, width, 32f), "Empty", entryStyle);
            foreach (var item in inventory.Items)
            {
                string label = item.Name + (item.Value > 0 ? "  $" + item.Value : "");
                float height = entryStyle.CalcHeight(new GUIContent(label), width);
                if (y + height > Screen.height - 100f) break;
                GUI.Label(new Rect(Screen.width - width - 24f, y, width, height), label, entryStyle);
                y += height + 6f;
            }
        }
    }
}

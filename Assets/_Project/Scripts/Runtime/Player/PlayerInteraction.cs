using System;
using Lockdown.Combat;
using Lockdown.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonController), typeof(Health), typeof(LootInventory))]
    public sealed class PlayerInteraction : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float reach = 2.5f;
        private FirstPersonController controller;
        private Health health;
        private LootInventory inventory;
        private InputAction interactAction;
        private GUIStyle promptStyle;
        private GUIStyle lootStyle;
        private GUIStyle entryStyle;
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
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            interactAction.AddBinding("<Gamepad>/buttonWest");
        }

        private void OnEnable() { interactAction?.Enable(); }
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
            return target != null && target.TryInteract(gameObject);
        }

        private IInteractable FindTarget()
        {
            var camera = controller.PlayerCamera;
            if (camera == null) return null;
            var hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward, reach,
                ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                // The first world collider blocks anything behind it, even if it is not interactive.
                return hit.collider.GetComponentInParent<IInteractable>();
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
            }
            float width = Mathf.Min(300f, Screen.width - 48f);
            GUI.Label(new Rect(Screen.width - width - 24f, 20f, width, 32f),
                "Loot " + inventory.ItemCount + "  |  $" + inventory.TotalValue, lootStyle);
            if (!CanInteract) return;
            var equipment = GetComponent<PlayerEquipment>();
            if (equipment == null || !equipment.IsPistolEquipped)
                GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
            var target = CurrentTarget;
            if (target != null)
            {
                float promptWidth = Mathf.Min(440f, Screen.width - 32f);
                string binding = interactAction.activeControl != null && interactAction.activeControl.device is Gamepad ? "X" : "E";
                GUI.Label(new Rect((Screen.width - promptWidth) * 0.5f, Screen.height * 0.5f + 24f, promptWidth, 64f),
                    "[" + binding + "] " + target.InteractionLabel, promptStyle);
            }
            if (!showInventory) return;
            float y = 60f;
            if (inventory.ItemCount == 0)
                GUI.Label(new Rect(Screen.width - width - 24f, y, width, 32f), "Empty", entryStyle);
            foreach (var item in inventory.Items)
            {
                string label = item.Name + "  $" + item.Value;
                float height = entryStyle.CalcHeight(new GUIContent(label), width);
                if (y + height > Screen.height - 100f) break;
                GUI.Label(new Rect(Screen.width - width - 24f, y, width, height), label, entryStyle);
                y += height + 6f;
            }
        }
    }
}

using Lockdown.Combat;
using Lockdown.Weapons;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(FirstPersonController), typeof(Health))]
    public sealed class PlayerEquipment : MonoBehaviour
    {
        [SerializeField] private GameObject emptyHands;
        [SerializeField] private FirstPersonWeapon pistol;
        [SerializeField] private FirstPersonHammer hammer;
        [SerializeField] private bool startWithPistol;
        // Off in the museum, where the pistol waits in the Security room locker.
        [SerializeField] private bool ownsPistol = true;

        private FirstPersonController controller;
        private Health health;
        private InputAction handsAction;
        private InputAction pistolAction;
        private InputAction hammerAction;
        private enum Slot { Hands, Pistol, Hammer }

        public bool IsPistolEquipped => pistol != null && pistol.gameObject.activeSelf;
        public GameObject EmptyHands => emptyHands;
        public bool HasPistol => ownsPistol;
        public bool HasHammer { get; private set; }
        public bool IsHammerEquipped => hammer != null && hammer.gameObject.activeInHierarchy;

        private void Awake()
        {
            controller = GetComponent<FirstPersonController>();
            health = GetComponent<Health>();
            if (emptyHands == null || pistol == null)
            {
                Debug.LogError("PlayerEquipment requires empty hands and a pistol.", this);
                enabled = false;
                return;
            }
            handsAction = new InputAction("SelectHands", InputActionType.Button, "<Keyboard>/1");
            handsAction.AddBinding("<Gamepad>/dpad/left");
            pistolAction = new InputAction("SelectPistol", InputActionType.Button, "<Keyboard>/2");
            pistolAction.AddBinding("<Gamepad>/dpad/right");
            hammerAction = new InputAction("SelectHammer", InputActionType.Button, "<Keyboard>/3");
            hammerAction.AddBinding("<Gamepad>/dpad/up");
        }

        private void OnEnable()
        {
            handsAction?.Enable();
            pistolAction?.Enable();
            hammerAction?.Enable();
        }

        // Let the pistol initialize once before hiding it so switching never resets ammunition.
        private void Start() { ApplySelection(startWithPistol && ownsPistol ? Slot.Pistol : Slot.Hands); }

        private void Update()
        {
            if (!CanSwitch()) return;
            if (handsAction.WasPressedThisFrame()) TryEquipHands();
            else if (pistolAction.WasPressedThisFrame()) TryEquipPistol();
            else if (hammerAction.WasPressedThisFrame()) TryEquipHammer();
        }

        public bool TryEquipHands() => Select(Slot.Hands);
        public bool TryEquipPistol() => Select(Slot.Pistol);
        public bool TryEquipHammer() => Select(Slot.Hammer);

        public bool TryCollectHammer()
        {
            if (!CanSwitch() || HasHammer || hammer == null) return false;
            HasHammer = true;
            ApplySelection(Slot.Hammer);
            return true;
        }

        public bool TryCollectPistol()
        {
            if (!CanSwitch() || ownsPistol) return false;
            ownsPistol = true;
            ApplySelection(Slot.Pistol);
            return true;
        }

        private bool Select(Slot slot)
        {
            if (!CanSwitch()) return false;
            if (slot == Slot.Hammer && (!HasHammer || hammer == null)) return false;
            if (slot == Slot.Pistol && !ownsPistol) return false;
            ApplySelection(slot);
            return true;
        }

        private bool CanSwitch() => isActiveAndEnabled && controller != null && controller.enabled &&
            controller.CanUseWeapon && health != null && !health.IsDead;

        private void ApplySelection(Slot slot)
        {
            if (emptyHands == null || pistol == null) return;
            emptyHands.SetActive(slot == Slot.Hands);
            pistol.gameObject.SetActive(slot == Slot.Pistol);
            if (hammer != null) hammer.gameObject.SetActive(slot == Slot.Hammer && HasHammer);
        }

        private void OnDisable()
        {
            handsAction?.Disable();
            pistolAction?.Disable();
            hammerAction?.Disable();
        }

        private void OnDestroy()
        {
            handsAction?.Dispose();
            pistolAction?.Dispose();
            hammerAction?.Dispose();
        }
    }
}

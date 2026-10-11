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
        [SerializeField] private bool startWithPistol;

        private FirstPersonController controller;
        private Health health;
        private InputAction handsAction;
        private InputAction pistolAction;

        public bool IsPistolEquipped => pistol != null && pistol.gameObject.activeSelf;
        public GameObject EmptyHands => emptyHands;

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
        }

        private void OnEnable()
        {
            handsAction?.Enable();
            pistolAction?.Enable();
        }

        // Let the pistol initialize once before hiding it so switching never resets ammunition.
        private void Start() { ApplySelection(startWithPistol); }

        private void Update()
        {
            if (!CanSwitch()) return;
            if (handsAction.WasPressedThisFrame()) TryEquipHands();
            else if (pistolAction.WasPressedThisFrame()) TryEquipPistol();
        }

        public bool TryEquipHands() => Select(false);
        public bool TryEquipPistol() => Select(true);

        private bool Select(bool usePistol)
        {
            if (!CanSwitch()) return false;
            ApplySelection(usePistol);
            return true;
        }

        private bool CanSwitch() => isActiveAndEnabled && controller != null && controller.enabled &&
            controller.CanUseWeapon && health != null && !health.IsDead;

        private void ApplySelection(bool usePistol)
        {
            if (emptyHands == null || pistol == null) return;
            emptyHands.SetActive(!usePistol);
            pistol.gameObject.SetActive(usePistol);
        }

        private void OnDisable()
        {
            handsAction?.Disable();
            pistolAction?.Disable();
        }

        private void OnDestroy()
        {
            handsAction?.Dispose();
            pistolAction?.Dispose();
        }
    }
}

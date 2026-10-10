using UnityEngine;
using UnityEngine.InputSystem;

namespace Lockdown.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 4f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 6.5f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.1f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Min(1f)] private float terminalSpeed = 40f;

        [Header("Look")]
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Min(1f)] private float gamepadLookSpeed = 150f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 85f;

        private CharacterController controller;
        private InputActionAsset ownedActions;
        private InputActionMap playerActions;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private float verticalVelocity;
        private float pitch;
        private bool cursorCaptured;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public bool IsGrounded => controller != null && controller.isGrounded;
        public bool IsCursorCaptured => cursorCaptured;
        public Camera PlayerCamera => playerCamera;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (playerCamera == null || inputActions == null)
            {
                Debug.LogError("FirstPersonController requires a camera and input actions.", this);
                enabled = false;
                return;
            }

            // Each prefab instance owns its actions so disabling one player cannot disable another.
            ownedActions = Instantiate(inputActions);
            playerActions = ownedActions.FindActionMap("Player", true);
            moveAction = playerActions.FindAction("Move", true);
            lookAction = playerActions.FindAction("Look", true);
            jumpAction = playerActions.FindAction("Jump", true);
            sprintAction = playerActions.FindAction("Sprint", true);
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void OnEnable()
        {
            if (playerActions == null) return;
            playerActions.Enable();
            CaptureCursor();
        }

        private void OnDisable()
        {
            playerActions?.Disable();
            if (cursorCaptured) ReleaseCursor();
        }

        private void OnDestroy()
        {
            if (ownedActions != null) Destroy(ownedActions);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && cursorCaptured) ReleaseCursor();
        }

        public void CaptureCursor()
        {
            cursorCaptured = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ReleaseCursor()
        {
            cursorCaptured = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (playerActions == null) return;

            bool captureClick = false;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ReleaseCursor();
            else if (!cursorCaptured && Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CaptureCursor();
                captureClick = true;
            }

            bool acceptsInput = cursorCaptured;
            if (acceptsInput && !captureClick) UpdateLook(Time.deltaTime);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (acceptsInput && jumpAction.WasPressedThisFrame() && controller.isGrounded)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            Vector2 input = acceptsInput ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
            float speed = acceptsInput && sprintAction.IsPressed() ? sprintSpeed : walkSpeed;
            Vector3 movement = (transform.right * input.x + transform.forward * input.y) * speed;
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -terminalSpeed);
            movement.y = verticalVelocity;
            CollisionFlags collisions = controller.Move(movement * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            if (transform.position.y < spawnPosition.y - 20f) Respawn();
        }

        private void UpdateLook(float deltaTime)
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            bool gamepad = lookAction.activeControl != null && lookAction.activeControl.device is Gamepad;
            float sensitivity = gamepad ? gamepadLookSpeed * deltaTime : mouseSensitivity;
            transform.Rotate(0f, look.x * sensitivity, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y * sensitivity, -pitchLimit, pitchLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Respawn()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            controller.enabled = true;
            verticalVelocity = 0f;
            pitch = 0f;
            playerCamera.transform.localRotation = Quaternion.identity;
        }

        private void OnValidate()
        {
            gravity = Mathf.Min(gravity, -0.1f);
            sprintSpeed = Mathf.Max(sprintSpeed, walkSpeed);
        }
    }
}

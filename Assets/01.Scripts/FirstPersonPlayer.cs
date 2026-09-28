using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TideSea
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonPlayer : MonoBehaviour
    {
        [Header("View")]
        public Camera playerCamera;
        [Range(0.01f, 0.5f)] public float mouseSensitivity = 0.1f;
        [Range(30f, 89f)] public float pitchLimit = 85;
        [Header("Movement")]
        [Min(0)] public float walkSpeed = 4.5f;
        [Min(0)] public float sprintSpeed = 7f;
        [Min(0)] public float jumpHeight = 1.1f;
        [Min(0.1f)] public float gravity = 20f;
        [Header("Recovery")]
        public Vector3 spawnPosition;
        public float spawnYaw;
        public float respawnBelowY = -90;

        CharacterController body;
        float verticalSpeed;
        float pitch;
        public bool HasControl => Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        public bool Grounded => body != null && body.isGrounded;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();
            if (!playerCamera) { Debug.LogError("FirstPersonPlayer needs a camera.", this); enabled = false; return; }
            pitch = Mathf.DeltaAngle(0, playerCamera.transform.localEulerAngles.x);
        }
        void Start() { CaptureCursor(); }
        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ReleaseCursor();
            else if (Application.isFocused && mouse != null && mouse.leftButton.wasPressedThisFrame
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) CaptureCursor();

            bool control = HasControl;
            if (control && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0, delta.x, 0, Space.World);
                pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
                playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            Vector2 movement = Vector2.zero;
            bool sprint = false;
            if (control && keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) { Respawn(); return; }
                movement = new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                movement = Vector2.ClampMagnitude(movement, 1);
                sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            }
            bool grounded = body.isGrounded;
            if (grounded && verticalSpeed < 0) verticalSpeed = -2;
            if (control && grounded && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                verticalSpeed = Mathf.Sqrt(2 * gravity * jumpHeight);
            verticalSpeed = Mathf.Max(verticalSpeed - gravity * Time.deltaTime, -50);
            Vector3 horizontal = (transform.right * movement.x + transform.forward * movement.y) * (sprint ? sprintSpeed : walkSpeed);
            CollisionFlags collisions = body.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if (transform.position.y < respawnBelowY) Respawn();
        }
        public void Respawn()
        {
            if (!body) body = GetComponent<CharacterController>();
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0, spawnYaw, 0));
            pitch = 0; verticalSpeed = 0;
            if (playerCamera) playerCamera.transform.localRotation = Quaternion.identity;
            body.enabled = true;
        }
        public void CaptureCursor() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        public void ReleaseCursor() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        void OnApplicationFocus(bool focused) { if (!focused) ReleaseCursor(); }
        void OnDisable() { ReleaseCursor(); }
    }
}

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

        private const float GroundedSpeed = -2f;
        private const float MaxFallSpeed = -50f;

        private CharacterController body;
        private float verticalSpeed;
        private float pitch;

        public bool HasControl => Application.isFocused && Cursor.lockState == CursorLockMode.Locked;
        public bool Grounded => body != null && body.isGrounded;

        private void Awake()
        {
            body = GetComponent<CharacterController>();

            if (!playerCamera)
                playerCamera = GetComponentInChildren<Camera>();

            if (!playerCamera)
            {
                Debug.LogError("FirstPersonPlayer needs a camera.", this);
                enabled = false;
                return;
            }

            pitch = Mathf.DeltaAngle(0, playerCamera.transform.localEulerAngles.x);
        }

        private void Start() => CaptureCursor();

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            UpdateCursor(keyboard, mouse);

            if (HasControl && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                Respawn();
                return;
            }

            if (HasControl && mouse != null)
                RotateView(mouse.delta.ReadValue());

            Move(keyboard);

            if (transform.position.y < respawnBelowY)
                Respawn();
        }

        private void UpdateCursor(Keyboard keyboard, Mouse mouse)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                return;
            }

            // UI 위가 아닌 화면을 클릭하면 다시 시점을 조작할 수 있게 한다.
            if (Application.isFocused && mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                CaptureCursor();
            }
        }

        private void RotateView(Vector2 mouseDelta)
        {
            float yawDelta = mouseDelta.x * mouseSensitivity;
            float pitchDelta = mouseDelta.y * mouseSensitivity;

            // 좌우 입력은 캐릭터 전체를 Y축으로 회전시킨다.
            transform.Rotate(Vector3.up, yawDelta, Space.World);

            // 상하 입력은 카메라만 X축으로 회전시킨다.
            pitch = Mathf.Clamp(pitch - pitchDelta, -pitchLimit, pitchLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        private void Move(Keyboard keyboard)
        {
            bool hasKeyboardControl = HasControl && keyboard != null;
            Vector2 input = hasKeyboardControl ? ReadMovement(keyboard) : Vector2.zero;
            bool sprint = hasKeyboardControl &&
                          (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            bool grounded = body.isGrounded;

            // 지면에 붙어 있도록 작은 하강 속도를 유지한다.
            if (grounded && verticalSpeed < 0)
                verticalSpeed = GroundedSpeed;

            if (hasKeyboardControl && grounded && keyboard.spaceKey.wasPressedThisFrame)
                verticalSpeed = Mathf.Sqrt(2 * gravity * jumpHeight);

            verticalSpeed = Mathf.Max(verticalSpeed - gravity * Time.deltaTime, MaxFallSpeed);

            float speed = sprint ? sprintSpeed : walkSpeed;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;
            CollisionFlags collisions = body.Move((horizontal + Vector3.up * verticalSpeed) * Time.deltaTime);

            // 천장과 충돌한 뒤 위쪽 속도가 남아 재충돌하는 현상을 막는다.
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0)
                verticalSpeed = 0;
        }

        private static Vector2 ReadMovement(Keyboard keyboard)
        {
            var input = new Vector2(
                (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));

            return Vector2.ClampMagnitude(input, 1);
        }

        public void Respawn()
        {
            if (!body)
                body = GetComponent<CharacterController>();

            // CharacterController를 잠시 꺼야 위치를 즉시 안전하게 변경할 수 있다.
            body.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, Quaternion.Euler(0, spawnYaw, 0));
            pitch = 0;
            verticalSpeed = 0;

            if (playerCamera)
                playerCamera.transform.localRotation = Quaternion.identity;

            body.enabled = true;
        }

        public void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                ReleaseCursor();
        }

        private void OnDisable() => ReleaseCursor();
    }
}

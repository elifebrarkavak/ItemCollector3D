using UnityEngine;
using UnityEngine.InputSystem;

namespace ItemCollector3D.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonPlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 12f;
        [SerializeField, Min(0f)] private float gravity = 25f;
        [SerializeField] private Transform movementReference;

        [Header("Input")]
        [SerializeField] private bool readKeyboardInput = true;

        private CharacterController characterController;
        private Vector2 externalMoveInput;
        private float verticalVelocity;

        public Vector2 MoveInput { get; private set; }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            MoveInput = externalMoveInput.sqrMagnitude > 0.0001f
                ? Vector2.ClampMagnitude(externalMoveInput, 1f)
                : ReadKeyboardInput();

            Move(MoveInput);
        }

        public void SetMoveInput(Vector2 value)
        {
            externalMoveInput = Vector2.ClampMagnitude(value, 1f);
        }

        public void ClearMoveInput()
        {
            externalMoveInput = Vector2.zero;
        }

        public void SetMovementReference(Transform reference)
        {
            movementReference = reference;
        }

        private Vector2 ReadKeyboardInput()
        {
            if (!readKeyboardInput || Keyboard.current == null)
            {
                return Vector2.zero;
            }

            Vector2 input = Vector2.zero;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                input.x -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                input.x += 1f;
            }

            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
            {
                input.y -= 1f;
            }

            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
            {
                input.y += 1f;
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        private void Move(Vector2 input)
        {
            Transform reference = movementReference != null
                ? movementReference
                : Camera.main != null ? Camera.main.transform : null;

            Vector3 forward = reference != null ? reference.forward : Vector3.forward;
            Vector3 right = reference != null ? reference.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 horizontalVelocity = (forward * input.y + right * input.x) * moveSpeed;

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity -= gravity * Time.deltaTime;
            }

            Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);

            if (horizontalVelocity.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(horizontalVelocity, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime);
            }
        }
    }
}

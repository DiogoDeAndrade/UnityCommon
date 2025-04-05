using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    [RequireComponent(typeof(CharacterController))]
    public class FPSController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 5f;      // Movement speed
        [SerializeField] private float mouseSensitivity = 2f; // Mouse sensitivity
        [SerializeField] private float pitchLimit = 80f;    // Pitch limit for looking up/down
        [SerializeField] private Transform headTransform;   // Reference to the head for pitch control
        [SerializeField]
        private AudioClip stepSound;
        [SerializeField, ShowIf(nameof(hasStepSound))]
        private float stepDistance = 1.0f;
        [SerializeField, ShowIf(nameof(hasStepSound)), MinMaxSlider(0.0f, 1.0f)]
        private Vector2 stepVolume = Vector2.one;
        [SerializeField, ShowIf(nameof(hasStepSound)), MinMaxSlider(0.5f, 2.0f)]
        private Vector2 stepPitch = Vector2.one;

        bool hasStepSound => stepSound != null;

        private CharacterController characterController;
        private Vector3 velocity;
        private float yaw;   // Horizontal rotation (around Y axis)
        private float pitch; // Vertical rotation (around X axis)
        private float currentStepDistance;

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            if (headTransform == null)
            {
                Debug.LogError("Head Transform is not assigned!");
            }

            // Lock and hide the cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void FixedUpdate()
        {
            HandleMovement();

            // Apply gravity
            if (characterController.isGrounded)
            {
                velocity.y = 0f;
            }
            else
            {
                velocity.y += Physics.gravity.y * Time.fixedDeltaTime;
            }

            // Move the player
            var stepSize = velocity * Time.fixedDeltaTime;
            characterController.Move(stepSize);

            currentStepDistance += stepSize.x0z().magnitude;
        }

        private void Update()
        {
            HandleMouseLook();

            if ((stepSound != null) && (currentStepDistance > stepDistance))
            {
                currentStepDistance -= stepDistance;
                SoundManager.PlaySound(SoundType.PrimaryFX, stepSound, stepVolume.Random(), stepPitch.Random());
            }
        }

        private void HandleMouseLook()
        {
            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Horizontal rotation (yaw) is applied to the body (transform)
            yaw += mouseX;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Vertical rotation (pitch) is applied to the head (headTransform)
            pitch -= mouseY;
            pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);
            headTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            // Get movement input (WASD)
            float moveX = Input.GetAxis("Horizontal"); // A/D movement (strafe)
            float moveZ = Input.GetAxis("Vertical");   // W/S movement (forward/backward)

            // Create movement vector
            Vector3 moveDirection = transform.right * moveX + transform.forward * moveZ;

            // Normalize direction to avoid faster diagonal movement and multiply by speed
            velocity.x = moveDirection.x * moveSpeed;
            velocity.z = moveDirection.z * moveSpeed;
        }
    }
}
using UnityEngine;
using NaughtyAttributes;
using UnityEngine.InputSystem;

namespace UC
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovementDirectionalXZ : MonoBehaviour
    {
        public enum MovementControl { Velocity = 0, Acceleration = 1 };
        public enum TurnBehaviour { None = 0, InputRotatesObject = 1, VelocityRotatesObject = 2 };

        [SerializeField]
        private float speed = 5.0f;
        [SerializeField]
        private MovementControl movementControl = MovementControl.Velocity;
        [SerializeField]
        private float acceleration = 50.0f;
        [SerializeField]
        private float deceleration = 50.0f;
        [SerializeField, HideIf("needNewInputSystem")]
        private PlayerInput playerInput;
        [SerializeField, InputPlayer(nameof(playerInput))]
        private InputControl moveInput;
        [SerializeField, InputPlayer(nameof(playerInput)), Tooltip("Turns the object on the spot while there's no move input (camera-relative), e.g. a right stick. Only read when it's an input action")]
        private InputControl aimInput;
        [SerializeField]
        private Hypertag cameraTag;
        [SerializeField]
        private TurnBehaviour turnBehaviour = TurnBehaviour.None;
        [SerializeField]
        private float turnSpeed = 720.0f;

        public bool isMoving { get; private set; }
        // The aim input turns the object while it moves too (twin-stick: walk one way, face another), e.g. with a bow
        public bool aimWhileMoving { get; set; }
        // The aim input is held (past the dead zone)
        public bool isAiming => GetAimInput().magnitude >= inputEpsilonZero;
        public Vector3 moveDirection { get; private set; }

        private Transform cameraTransform;
        private Vector3 planarVelocity;

        const float inputEpsilonZero = 0.1f;
        const float velocityEpsilonZero = 0.05f;

        public float GetSpeed() => speed;
        public void SetSpeed(float speed) { this.speed = speed; }

        public void SetTurnSpeed(float v) { turnSpeed = v; }
        public float GetTurnSpeed() => turnSpeed;

        public bool needNewInputSystem => (moveInput.type == InputControl.InputType.NewInput);

        protected Rigidbody rb;

        protected void Start()
        {
            rb = GetComponent<Rigidbody>();

            moveInput.playerInput = playerInput;
            if (aimInput != null) aimInput.playerInput = playerInput;

            FetchCamera();
        }

        void FixedUpdate()
        {
            Vector2 input = moveInput.GetAxis2();
            // Keep diagonals from being faster than straight movement (keyboard input)
            if (input.sqrMagnitude > 1.0f) input.Normalize();
            if (input.magnitude < inputEpsilonZero) input = Vector2.zero;

            (Vector3 forward, Vector3 right) = GetCameraAxis();

            moveDirection = right * input.x + forward * input.y;

            Vector3 targetVelocity = moveDirection * speed;
            Vector3 currentVelocity = rb.linearVelocity;
            planarVelocity = new Vector3(currentVelocity.x, 0.0f, currentVelocity.z);

            if (movementControl == MovementControl.Acceleration)
            {
                float accel = (moveDirection.sqrMagnitude > 0.0f) ? acceleration : deceleration;
                planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, accel * Time.fixedDeltaTime);
            }
            else
            {
                planarVelocity = targetVelocity;
            }

            // Vertical velocity is left alone, so gravity and collisions still work
            rb.linearVelocity = new Vector3(planarVelocity.x, currentVelocity.y, planarVelocity.z);

            isMoving = planarVelocity.magnitude > velocityEpsilonZero;

            Vector3 facing = Vector3.zero;
            switch (turnBehaviour)
            {
                case TurnBehaviour.None:
                    break;
                case TurnBehaviour.InputRotatesObject:
                    if (moveDirection.sqrMagnitude > 0.0f) facing = moveDirection;
                    break;
                case TurnBehaviour.VelocityRotatesObject:
                    if (isMoving) facing = planarVelocity;
                    break;
                default:
                    break;
            }

            // Standing still (or always, with aimWhileMoving): the aim input turns the object
            if ((moveDirection.sqrMagnitude == 0.0f) || (aimWhileMoving))
            {
                Vector2 aim = GetAimInput();
                if (aim.magnitude >= inputEpsilonZero) facing = right * aim.x + forward * aim.y;
            }

            if (facing.sqrMagnitude > 0.0f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
                if (turnSpeed > 0.0f)
                {
                    targetRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
                }
                rb.MoveRotation(targetRotation);
            }
        }

        // Only input actions are read: the other input types can't give a 2D axis (and an unset one would throw)
        Vector2 GetAimInput()
        {
            if ((aimInput == null) || (aimInput.type != InputControl.InputType.NewInput)) return Vector2.zero;

            return aimInput.GetAxis2();
        }

        void FetchCamera()
        {
            if (cameraTag != null)
            {
                var cam = Hypertag.FindFirstObjectWithHypertag<Camera>(cameraTag);
                if (cam) cameraTransform = cam.transform;
            }
            if ((cameraTransform == null) && (Camera.main != null))
            {
                cameraTransform = Camera.main.transform;
            }
        }

        (Vector3, Vector3) GetCameraAxis()
        {
            if (cameraTransform == null)
            {
                FetchCamera();
                // No camera, just use world axis
                if (cameraTransform == null) return (Vector3.forward, Vector3.right);
            }

            Vector3 forward = cameraTransform.forward;
            forward.y = 0.0f;
            // Camera is looking straight down, so use the top of the screen as forward
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = cameraTransform.up;
                forward.y = 0.0f;
            }
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);

            return (forward, right);
        }

        public void SetActive(bool active)
        {
            enabled = active;
            if (!active)
            {
                rb.linearVelocity = new Vector3(0.0f, rb.linearVelocity.y, 0.0f);
                planarVelocity = Vector3.zero;
                isMoving = false;
            }
        }
    }
}

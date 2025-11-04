using UnityEngine;
using NaughtyAttributes;
using UnityEngine.InputSystem;

namespace UC
{

    [RequireComponent(typeof(Rigidbody2D))]
    public class MovementPlatformer : MonoBehaviour
    {
        public enum FlipBehaviour
        {
            None = 0,
            VelocityFlipsSprite = 1, VelocityInvertsScale = 2,
            InputFlipsSprite = 3, InputInvertsScale = 4,
            VelocityRotatesSprite = 5, InputRotatesSprite = 6
        };
        public enum JumpBehaviour { None = 0, Fixed = 1, Variable = 2 };
        public enum GlideBehaviour { None = 0, Enabled = 1, Timer = 2 };
        public enum ClimbBehaviour { None = 0, Enabled = 1 };

        [SerializeField]
        private Vector2 speed = new Vector2(100, 100);
        [SerializeField, HideIf("needNewInputSystem")]
        private PlayerInput playerInput;
        [SerializeField, InputPlayer(nameof(playerInput))]
        private InputControl horizontalInput;
        [SerializeField]
        private float gravityScale = 1.0f;
        [SerializeField]
        private bool useTerminalVelocity = false;
        [SerializeField]
        private float terminalVelocity = 100.0f;
        [SerializeField]
        private float coyoteTime = 0.0f;
        [SerializeField]
        private JumpBehaviour jumpBehaviour = JumpBehaviour.None;
        [SerializeField]
        private int maxJumpCount = 1;
        [SerializeField]
        private float jumpBufferingTime = 0.1f;
        [SerializeField]
        private float jumpHoldMaxTime = 0.1f;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        private InputControl jumpInput;
        [SerializeField]
        private bool enableAirControl = true;
        [SerializeField]
        private Collider2D airCollider;
        [SerializeField]
        private Collider2D groundCollider;
        [SerializeField]
        private GlideBehaviour glideBehaviour = GlideBehaviour.None;
        [SerializeField]
        private float glideMaxTime = float.MaxValue;
        [SerializeField]
        private float maxGlideSpeed = 50.0f;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        private InputControl glideInput;
        [SerializeField]
        private Collider2D groundCheckCollider;
        [SerializeField]
        private LayerMask groundLayerMask;
        [SerializeField]
        private ClimbBehaviour climbBehaviour;
        [SerializeField]
        private Collider2D climbCheckCollider;
        [SerializeField]
        private LayerMask climbMask;
        [SerializeField]
        private float climbSpeed = 200;
        [SerializeField]
        private float climbCooldown = 0.0f;
        [SerializeField, InputPlayer(nameof(playerInput))]
        private InputControl climbInput;
        [SerializeField]
        private bool canJumpFromClimb = false;
        [SerializeField]
        private FlipBehaviour flipBehaviour = FlipBehaviour.None;
        [SerializeField]
        private bool useAnimator = false;
        [SerializeField]
        private Animator animator;
        [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Float)]
        private string horizontalVelocityParameter;
        [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Float)]
        private string absoluteHorizontalVelocityParameter;
        [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Float)]
        private string verticalVelocityParameter;
        [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Float)]
        private string absoluteVerticalVelocityParameter;
        [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Bool)]
        private string isGroundedParameter;
        [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Bool)]
        private string isGlidingParameter;
        [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Bool)]
        private string isClimbingParameter;

        public bool isGrounded { get; private set; }
        public bool isClimbing { get; private set; }
        private SpriteRenderer spriteRenderer;
        private int currentJumpCount;
        private bool prevJumpKey = false;
        private float jumpBufferingTimer = 0.0f;
        private float jumpTime;
        private float coyoteTimer;
        private bool actualIsGrounded;
        private float glideTimer = 0.0f;
        private bool canClimb = false;
        public bool isGliding { get; private set; }

        const float inputEpsilonZero = 0.4f;
        const float velocityEpsilonZero = 5.0f;

        float lastClimbTime = 0.0f;

        Quaternion originalRotation;

        public Vector2 GetSpeed() => speed;
        public void SetSpeed(Vector2 speed) { this.speed = speed; }

        public void SetGravityScale(float v) { gravityScale = v; }
        public float GetGravityScale() => gravityScale;

        public void SetMaxJumpCount(int v) { maxJumpCount = v; }

        public void SetJumpHoldTime(float v) { jumpHoldMaxTime = v; }
        public float GetJumpHoldTime() => jumpHoldMaxTime;
        public void SetGlideMaxTime(float v) { glideMaxTime = v; }
        public float GetGlideMaxTime() => glideMaxTime;

        public bool needNewInputSystem => (horizontalInput.type == InputControl.InputType.NewInput);

        protected Rigidbody2D rb;

        protected void Start()
        {
            rb = GetComponent<Rigidbody2D>();

            if (rb)
            {
                rb.gravityScale = 0.0f;
            }
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            horizontalInput.playerInput = playerInput;
            climbInput.playerInput = playerInput;
            jumpInput.playerInput = playerInput;
            glideInput.playerInput = playerInput;

            originalRotation = transform.rotation;
        }

        void FixedUpdate()
        {
            UpdateGroundState();

            // Jump buffering
            if ((jumpBehaviour != JumpBehaviour.None) && (jumpBufferingTimer > 0))
            {
                jumpBufferingTimer -= Time.fixedDeltaTime;
                if (isGrounded)
                {
                    Jump();
                }
            }

            // Fixed height jump
            if (jumpBehaviour == JumpBehaviour.Fixed)
            {
                bool isJumpPressed = GetJumpPressed();
                if ((isJumpPressed) && (!prevJumpKey))
                {
                    jumpBufferingTimer = jumpBufferingTime;

                    if ((isJumpPressed) && (!prevJumpKey))
                    {
                        if ((isGrounded) && (currentJumpCount == maxJumpCount))
                        {
                            Jump();
                        }
                        else if (currentJumpCount > 0)
                        {
                            Jump();
                        }
                    }
                }
                prevJumpKey = isJumpPressed;
            }
            else
            {
                bool isJumpPressed = GetJumpPressed();
                if (isJumpPressed)
                {
                    if (!prevJumpKey)
                    {
                        jumpBufferingTimer = jumpBufferingTime;

                        if ((isGrounded) && (currentJumpCount == maxJumpCount))
                        {
                            Jump();
                        }
                        else if ((currentJumpCount > 0) && (currentJumpCount != maxJumpCount))
                        {
                            Jump();
                        }
                        else if ((climbBehaviour == ClimbBehaviour.Enabled) && (canJumpFromClimb) && (canClimb))
                        {
                            Jump();
                        }
                    }
                    else if ((Time.time - jumpTime) < jumpHoldMaxTime)
                    {
                        rb.linearVelocity = new Vector2(rb.linearVelocity.x, speed.y);
                    }
                }
                else
                {
                    // Jump button was released, so it doesn't count anymore as being pressed
                    jumpTime = -float.MaxValue;
                }
                prevJumpKey = isJumpPressed;
            }

            bool limitFallSpeed = false;
            float maxFallSpeed = float.MaxValue;

            if (useTerminalVelocity)
            {
                limitFallSpeed = true;
                maxFallSpeed = terminalVelocity;
            }

            isGliding = false;
            if (glideBehaviour != GlideBehaviour.None)
            {
                if ((GetGlidePressed()) && ((glideTimer >= 0.0f) || (glideBehaviour == GlideBehaviour.Enabled)))
                {
                    glideTimer -= Time.fixedDeltaTime;
                    limitFallSpeed = true;
                    maxFallSpeed = maxGlideSpeed;
                    isGliding = true;
                }
                else
                {
                    isGliding = false;
                }
            }
            else isGliding = false;

            if (limitFallSpeed)
            {
                var currentVelocity = rb.linearVelocity;
                if (currentVelocity.y < -maxFallSpeed)
                {
                    currentVelocity.y = -maxFallSpeed;
                    rb.linearVelocity = currentVelocity;
                }
            }
        }

        void Jump()
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, speed.y);
            jumpBufferingTimer = 0.0f;
            coyoteTimer = 0;
            jumpTime = Time.time;
            currentJumpCount--;
        }

        public void Bounce(float multiplier)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, speed.y * multiplier);
            jumpBufferingTimer = 0.0f;
            coyoteTimer = 0;
            jumpTime = Time.time;
            currentJumpCount--;
        }

        bool GetJumpPressed()
        {
            return jumpInput.IsPressed();
        }

        bool GetGlidePressed()
        {
            return glideInput.IsPressed();
        }

        void Update()
        {
            if (coyoteTimer > 0)
            {
                coyoteTimer -= Time.deltaTime;
            }

            float deltaX = 0.0f;

            UpdateGroundState();

            if ((enableAirControl) || (isGrounded))
            {
                deltaX = horizontalInput.GetAxis();

                rb.linearVelocity = new Vector2(deltaX * speed.x, rb.linearVelocity.y);
            }

            // Need to check with actual is grounded or else coyote time will make the jump count reset immediately after flying off
            if (actualIsGrounded)
            {
                rb.gravityScale = 0.0f;
                currentJumpCount = maxJumpCount;
                if ((airCollider != groundCollider) && (airCollider) && (groundCollider))
                {
                    airCollider.enabled = false;
                    groundCollider.enabled = true;
                }
                glideTimer = glideMaxTime;
            }
            else
            {
                rb.gravityScale = gravityScale;
                if ((airCollider != groundCollider) && (airCollider) && (groundCollider))
                {
                    airCollider.enabled = true;
                    groundCollider.enabled = false;
                }
            }

            var currentVelocity = rb.linearVelocity;

            if (climbBehaviour == ClimbBehaviour.Enabled)
            {
                float deltaY = climbInput.GetAxis();

                if (isClimbing)
                {
                    UpdateClimbState();
                    if (!canClimb)
                    {
                        rb.gravityScale = gravityScale;
                        isClimbing = false;
                    }
                    else
                    {
                        rb.gravityScale = 0.0f;

                        currentVelocity.y = deltaY * climbSpeed;
                        rb.linearVelocity = currentVelocity;

                        if ((deltaY < 0.0f) && (isGrounded))
                        {
                            isClimbing = false;
                        }
                    }
                    lastClimbTime = Time.time;
                }
                else
                {
                    if ((Mathf.Abs(deltaY) > 0.25f) && ((Time.time - lastClimbTime) > climbCooldown))
                    {
                        UpdateClimbState();
                        if (canClimb)
                        {
                            isClimbing = true;
                        }
                    }
                }
            }

            if ((useAnimator) && (animator))
            {
                if (horizontalVelocityParameter != "") animator.SetFloat(horizontalVelocityParameter, currentVelocity.x);
                if (absoluteHorizontalVelocityParameter != "") animator.SetFloat(absoluteHorizontalVelocityParameter, Mathf.Abs(currentVelocity.x));
                if (verticalVelocityParameter != "") animator.SetFloat(verticalVelocityParameter, currentVelocity.y);
                if (absoluteVerticalVelocityParameter != "") animator.SetFloat(absoluteVerticalVelocityParameter, Mathf.Abs(currentVelocity.y));
                if (isGroundedParameter != "") animator.SetBool(isGroundedParameter, actualIsGrounded);
                if (isGlidingParameter != "") animator.SetBool(isGlidingParameter, isGliding);
                if (isClimbingParameter != "") animator.SetBool(isClimbingParameter, isClimbing);
            }

            switch (flipBehaviour)
            {
                case FlipBehaviour.None:
                    break;
                case FlipBehaviour.VelocityFlipsSprite:
                    if (currentVelocity.x > velocityEpsilonZero) spriteRenderer.flipX = false;
                    else if (currentVelocity.x < -velocityEpsilonZero) spriteRenderer.flipX = true;
                    break;
                case FlipBehaviour.VelocityInvertsScale:
                    if ((currentVelocity.x > velocityEpsilonZero) && (transform.localScale.x < 0.0f)) transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
                    else if ((currentVelocity.x < -velocityEpsilonZero) && (transform.localScale.x > 0.0f)) transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
                    break;
                case FlipBehaviour.VelocityRotatesSprite:
                    if ((currentVelocity.x > velocityEpsilonZero) && (transform.right.x < 0.0f))
                    {
                        transform.rotation = originalRotation;
                    }
                    else if ((currentVelocity.x < -velocityEpsilonZero) && (transform.right.x > 0.0f))
                    {
                        transform.rotation = originalRotation * Quaternion.Euler(0, 180, 0);
                    }
                    break;
                case FlipBehaviour.InputFlipsSprite:
                    if (deltaX > inputEpsilonZero) spriteRenderer.flipX = false;
                    else if (deltaX < -inputEpsilonZero) spriteRenderer.flipX = true;
                    break;
                case FlipBehaviour.InputInvertsScale:
                    if ((deltaX > inputEpsilonZero) && (transform.localScale.x < 0.0f)) transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
                    else if ((deltaX < -inputEpsilonZero) && (transform.localScale.x > 0.0f)) transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
                    break;
                case FlipBehaviour.InputRotatesSprite:
                    if ((deltaX > inputEpsilonZero) && (transform.right.x < 0.0f)) transform.rotation = originalRotation;
                    else if ((deltaX < -inputEpsilonZero) && (transform.right.x > 0.0f)) transform.rotation = originalRotation * Quaternion.Euler(0, 180, 0);
                    break;
                default:
                    break;
            }
        }

        void UpdateGroundState()
        {
            if (groundCheckCollider)
            {
                ContactFilter2D contactFilter = new ContactFilter2D();
                contactFilter.useLayerMask = true;
                contactFilter.layerMask = groundLayerMask;

                Collider2D[] results = new Collider2D[128];

                int n = Physics2D.OverlapCollider(groundCheckCollider, contactFilter, results);
                if (n > 0)
                {
                    actualIsGrounded = true;
                    isGrounded = true;
                    coyoteTimer = coyoteTime;
                    return;
                }
                else
                {
                    actualIsGrounded = false;
                    if (rb.linearVelocity.y > 0)
                    {
                        coyoteTimer = 0;
                    }
                }
            }

            actualIsGrounded = false;

            if (coyoteTimer > 0)
            {
                isGrounded = true;
                return;
            }

            isGrounded = false;
        }
        void UpdateClimbState()
        {
            if (climbCheckCollider)
            {
                ContactFilter2D contactFilter = new ContactFilter2D();
                contactFilter.useLayerMask = true;
                contactFilter.layerMask = climbMask;
                contactFilter.useTriggers = true;

                Collider2D[] results = new Collider2D[128];

                int n = Physics2D.OverlapCollider(climbCheckCollider, contactFilter, results);
                if (n > 0)
                {
                    canClimb = true;
                    return;
                }
            }

            canClimb = false;
        }

        public void SetActive(bool active)
        {
            enabled = active;
            if (!active)
            {
                rb.linearVelocity = new Vector2(0.0f, rb.linearVelocity.y);
            }
        }

        public void EnableColliders(bool v)
        {
            if (airCollider) airCollider.enabled = v;
            if (groundCollider) groundCollider.enabled = v;
        }
    }
}
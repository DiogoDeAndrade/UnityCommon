using System;
using UnityEngine;
using NaughtyAttributes;

namespace UC.Legacy
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Parameters")]
        public float moveSpeed = 64;
        [Range(0.0f, 1.0f)]
        public float drag = 0.9f;
        public LayerMask groundMask;
        public float jumpSpeed = 128;
        public float jumpSustainMaxTime = 0.0f;
        public float gravityJumpMultiplier = 4.0f;
        public float coyoteTime = 0.1f;
        public bool knockbackEnable = true;
        public float interactionRadius = 10.0f;
        public LayerMask interactionMask;
        [Header("References")]
        public GameObject groundPoint;
        public Transform followTarget;
        [Header("Controls")]
        public bool enableControls = true;
        public string xAxis = "Horizontal";
        public string jumpButton = "Jump";
        public string interact = "Interact";
        [Header("Sounds")]
        public AudioSource stepSound;
        public AudioSource jumpSound;
        public AudioSource deathSound;
        public AudioSource hitSound;
        [Header("Camera")]
        public Transform followPos;
        public bool shakeOnHit = false;
        [ShowIf("shakeOnHit")]
        public float hitShakeStrength = 20.0f;
        [ShowIf("shakeOnHit")]
        public float hitShakeTime = 0.1f;
        public bool shakeOnDead = false;
        [ShowIf("shakeOnDead")]
        public float deadShakeStrength = 40.0f;
        [ShowIf("shakeOnDead")]
        public float deadShakeTime = 0.1f;

        Vector2 movementDir;
        Vector2 currentVelocity;
        Rigidbody2D rb;
        TimeScaler2d timeScaler;
        Animator anim;
        float jumpTime;
        bool jumpPress;
        Collider2D coyoteCollider;
        ContactFilter2D groundContactFilter;
        float timeOfFall;
        bool movementEnable = true;
        HealthSystem healthSystem;

        public bool isInvulnerable
        {
            get
            {
                return healthSystem.isInvulnerable;
            }
        }

        public bool isDead
        {
            get
            {
                return healthSystem.isDead;
            }
        }

        Vector2 groundPointPosition
        {
            get
            {
                return (groundPoint) ? (groundPoint.transform.position) : (transform.position);
            }
        }

        public bool isGrounded
        {
            get
            {
                if (coyoteCollider)
                {
                    if (coyoteCollider.enabled)
                    {
                        Collider2D[] colliders = new Collider2D[32];
                        return (Physics2D.OverlapCollider(coyoteCollider, groundContactFilter, colliders) > 0);
                    }
                }

                Vector2 gp = groundPointPosition;
                bool b = Physics2D.OverlapPoint(gp, groundMask);

                return b;
            }
        }

        float timestamp
        {
            get
            {
                if (timeScaler) return Time.time;
                else return timeScaler.time;
            }
        }

        float gravity
        {
            get
            {
                if (timeScaler) return timeScaler.originalGravityScale;

                return rb.gravityScale;
            }
            set
            {
                if (timeScaler)
                    timeScaler.originalGravityScale = value;
                else
                    rb.gravityScale = value;
            }
        }

        Vector2 velocity
        {
            get
            {
                if (timeScaler) return timeScaler.originalVelocity;

                return rb.linearVelocity;
            }
            set
            {
                if (timeScaler)
                    timeScaler.originalVelocity = value;
                else
                    rb.linearVelocity = value;
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            timeScaler = GetComponent<TimeScaler2d>();
            anim = GetComponent<Animator>();
            healthSystem = GetComponent<HealthSystem>();
            healthSystem.onHit += OnHit;
            healthSystem.onDead += OnDead;

            if (groundPoint)
                coyoteCollider = groundPoint.GetComponent<Collider2D>();
            else
                coyoteCollider = null;

            groundContactFilter = new ContactFilter2D();
            groundContactFilter.SetLayerMask(groundMask);

            if (followTarget == null)
            {
                followTarget = transform;
            }
        }

        private void FixedUpdate()
        {
            currentVelocity = velocity;

            if (Mathf.Abs(movementDir.x) > 0.01f)
            {
                currentVelocity.x = movementDir.x * moveSpeed;
            }
            else
            {
                currentVelocity.x *= (1.0f - drag);
            }

            if (movementDir.y > 0.0f)
            {
                currentVelocity.y = jumpSpeed;

                movementDir.y = 0.0f;
            }

            velocity = currentVelocity;

            currentVelocity = velocity;

            if (jumpPress)
            {
                // Going up
                if (currentVelocity.y > 0.01f)
                {
                    if ((timestamp - jumpTime) > jumpSustainMaxTime)
                    {
                        gravity = gravityJumpMultiplier;
                    }
                }
                else
                {
                    gravity = gravityJumpMultiplier;
                }
            }
            else
            {
                gravity = gravityJumpMultiplier;
            }

            if (Mathf.Abs(currentVelocity.y) > 0.01f)
            {
                coyoteCollider.enabled = false;
            }
            else
            {
                coyoteCollider.enabled = true;

                if (Physics2D.OverlapCircle(groundPointPosition, 1.0f, groundMask))
                {
                    timeOfFall = timestamp;
                }
                else
                {
                    if ((timestamp - timeOfFall) > coyoteTime)
                    {
                        coyoteCollider.enabled = false;
                    }
                }
            }
        }

        void Update()
        {
            if (isDead) return;

            if ((movementEnable) && (enableControls))
                movementDir.x = Input.GetAxis(xAxis);
            else
                movementDir.x = 0.0f;

            bool grounded = isGrounded;

            if (grounded)
            {
                gravity = 1.0f;
            }

            if ((Input.GetButtonDown(jumpButton)) && (movementEnable) && (enableControls))
            {
                if (Mathf.Abs(currentVelocity.y) < 0.01f)
                {
                    if (grounded)
                    {
                        movementDir.y = 1.0f;
                        jumpTime = timestamp;

                        gravity = 1.0f;
                        jumpPress = true;

                        if (jumpSound)
                        {
                            jumpSound.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                            jumpSound.Play();
                        }

                    }
                }

            }
            else if (Input.GetButton(jumpButton))
            {
                jumpPress = true;
            }
            else
            {
                jumpPress = false;
            }

            // Animation/Visual update
            float absMovementX = Mathf.Abs(movementDir.x);
            anim.SetFloat("AbsSpeedX", absMovementX);
            anim.SetFloat("SpeedY", currentVelocity.y);
            if (absMovementX > 0.0f) anim.SetFloat("AnimSpeed", absMovementX);
            else anim.SetFloat("AnimSpeed", 1.0f);
            anim.SetBool("Grounded", grounded);

            Vector2 right = transform.right;

            if (Vector2.Dot(right, movementDir) < 0.0f)
            {
                if (movementDir.x > 0.01f) transform.rotation = Quaternion.identity;
                else if (movementDir.x < -0.01f) transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
            }

            // Interaction
            if ((Input.GetButtonDown(interact)) && (enableControls))
            {
                var colliders = Physics2D.OverlapCircleAll(transform.position, interactionRadius, interactionMask);

                bool interact = false;
                foreach (var collider in colliders)
                {
                    Interactable interactable = collider.GetComponentInParent<Interactable>();

                    if (interactable)
                    {
                        interactable.Interact();
                        interact = true;
                    }
                }

                if (interact) anim.SetBool("Attack", interact);
            }
        }

        public void EnableMovement(bool b)
        {
            movementEnable = b;
        }

        public Transform GetFollowTarget()
        {
            return followTarget;
        }

        private void OnDead(GameObject damageSource)
        {
            movementDir = Vector2.zero;

            anim.SetTrigger("Dead");

            if (deathSound) deathSound.Play();

            if (shakeOnDead)
            {
                CameraShake2d.Shake(deadShakeStrength, deadShakeTime);
            }
        }

        private void OnHit(HealthSystem.DamageType damageType, float damage, Vector3 damagePosition, Vector3 hitDirection, GameObject damageSource)
        {
            healthSystem.isInvulnerable = true;

            anim.SetTrigger("Hit");

            if (knockbackEnable)
            {
                var v = velocity;
                v.y = 100.0f;
                velocity = v;
            }

            if (hitSound)
            {
                hitSound.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                hitSound.Play();
            }

            if (shakeOnHit)
            {
                CameraShake2d.Shake(hitShakeStrength, hitShakeTime);
            }
        }

        public void DestroySelf()
        {
            Destroy(gameObject);
        }

        public void Celebrate()
        {
            anim.SetTrigger("Celebrate");
        }

        public void StepSound()
        {
            if (stepSound)
            {
                stepSound.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                stepSound.Play();
            }
        }

        public void EnableControls(bool b)
        {
            enableControls = b;
        }
    }
}
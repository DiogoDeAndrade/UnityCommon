using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Parameters")]
    public float        moveSpeed = 64;
    [Range(0.0f, 1.0f)]
    public float        drag = 0.9f;
    public LayerMask    groundMask;
    public float        jumpSpeed = 128;
    public float        jumpSustainMaxTime = 0.0f;
    public float        gravityJumpMultiplier = 4.0f;
    public float        coyoteTime = 0.1f;
    [Header("References")]
    public GameObject   groundPoint;
    [Header("Controls")]
    public string       xAxis = "Horizontal";
    public string       jumpButton = "Jump";

    Vector2         movementDir;
    Vector2         currentVelocity;
    Rigidbody2D     rb;
    Animator        anim;
    float           jumpTime;
    bool            jumpPress;
    Collider2D      coyoteCollider;
    ContactFilter2D groundContactFilter;
    float           timeOfFall;

    Vector2 groundPointPosition
    {
        get
        {
            return (groundPoint) ? (groundPoint.transform.position) : (transform.position);
        }
    }

    bool isGrounded
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
            bool    b = Physics2D.OverlapPoint(gp, groundMask);

            return b;
        }
    }

    float timestamp
    {
        get
        {
            return Time.time;
        }
    }

    float gravity
    {
        get
        {
            return rb.gravityScale;
        }
        set
        {
            rb.gravityScale = value;
        }
    }

    Vector2 velocity
    {
        get
        {
            return rb.velocity;
        }
        set
        {
            rb.velocity = value;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        if (groundPoint)
            coyoteCollider = groundPoint.GetComponent<Collider2D>();
        else
            coyoteCollider = null;

        groundContactFilter = new ContactFilter2D();
        groundContactFilter.layerMask = groundMask;
    }

    private void FixedUpdate()
    {
        currentVelocity = velocity;

        if (Mathf.Abs(movementDir.x) > 0.0001f)
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
            if (currentVelocity.y > 0.0001f)
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

        if (Mathf.Abs(currentVelocity.y) > 0.001f)
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
        movementDir.x = Input.GetAxis(xAxis);

        bool grounded = isGrounded;

        if (isGrounded)
        {
            gravity = 1.0f;
        }

        if (Input.GetButtonDown(jumpButton))
        {
            if (Mathf.Abs(currentVelocity.y) < 0.0001f)
            {
                if (isGrounded)
                {
                    movementDir.y = 1.0f;
                    jumpTime = timestamp;

                    gravity = 1.0f;
                    jumpPress = true;
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
        anim.SetFloat("AbsSpeedX", Mathf.Abs(movementDir.x));

        Vector2 right = transform.right;

        if (Vector2.Dot(right, movementDir) < 0.0f)
        {
            if (movementDir.x > 0.0001f) transform.rotation = Quaternion.identity;
            else if (movementDir.x < -0.0001f) transform.rotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
        }
    }
}

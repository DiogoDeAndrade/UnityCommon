using UnityEngine;
using NaughtyAttributes;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace UC
{

    [RequireComponent(typeof(GridObject))]
    public class MovementGridXY : MonoBehaviour
    {
        public enum InputType { Axis = 0, Button = 1, Key = 2 };
        public enum Axis { UpAxis = 0, RightAxis = 1 };

        [SerializeField]
        private Vector2 speed = new Vector2(100, 100);
        [SerializeField]
        protected float cooldown = 0.0f;
        [SerializeField]
        private bool useRotation = false;
        [SerializeField]
        private bool turnToDirection = false;
        [SerializeField]
        private Axis axisToAlign = Axis.UpAxis;
        [SerializeField]
        private float maxTurnSpeed = 360.0f;
        [SerializeField]
        private bool inputEnabled;
        [SerializeField]
        private bool turnEnabled;
        [SerializeField]
        private PlayerInput playerInput;
        [SerializeField, InputPlayer(nameof(playerInput))]
        private InputControl movementInput;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        private InputControl turnModifier;

        protected SpriteRenderer spriteRenderer;
        protected GridObject gridObject;
        protected Rigidbody2D rb;
        protected float moveCooldownTimer = 0.0f;
        protected Vector3 currentVelocity = Vector3.zero;
        protected Vector2 prevMoveVector;

        public Vector2 GetSpeed() => speed;
        public void SetSpeed(Vector2 speed) { this.speed = speed; }
        public bool needNewInputSystem => (movementInput.type == InputControl.InputType.NewInput) || (turnEnabled) && (turnModifier.type == InputControl.InputType.NewInput);

        protected void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            gridObject = GetComponent<GridObject>();
            if (inputEnabled)
            {
                movementInput.playerInput = playerInput;
                turnModifier.playerInput = playerInput;
            }

            if (rb)
            {
                rb.gravityScale = 0.0f;
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        protected void Update()
        {
            if ((cooldown > 0.0f) || (moveCooldownTimer > 0.0f))
            {
                moveCooldownTimer -= Time.deltaTime;
                if (moveCooldownTimer < 0.0f) moveCooldownTimer = 0.0f;
            }

            if (gridObject.isMoving)
            {
                if ((!useRotation) && (turnToDirection))
                {
                    if (gridObject.lastDelta.sqrMagnitude > 1e-6)
                    {
                        Vector3 upAxis = gridObject.lastDelta.normalized;

                        if (axisToAlign == Axis.RightAxis) upAxis = new Vector3(-upAxis.y, upAxis.x);

                        Quaternion targetRotation = Quaternion.LookRotation(Vector3.forward, upAxis);

                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxTurnSpeed * Time.fixedDeltaTime);
                    }
                }
            }
            else if (moveCooldownTimer <= 0.0f)
            {
                Vector2 moveVector = Vector3.zero;
                if (inputEnabled)
                {
                    moveVector = movementInput.GetAxis2();
                    if (Mathf.Abs(prevMoveVector.x) > 0.2f) moveVector.x = 0;
                    if (Mathf.Abs(prevMoveVector.y) > 0.2f) moveVector.y = 0;

                    prevMoveVector = movementInput.GetAxis2();
                }
                else
                {
                    moveVector = speed;
                }

                if ((turnEnabled) && (turnModifier.IsPressed()))
                    TurnInDirection(moveVector);
                else
                    MoveInDirection(moveVector, useRotation);
            }
        }

        protected void MoveInDirection(Vector2 moveVector, bool relativeAxis)
        {
            if (moveVector != Vector2.zero)
            {
                Vector2Int gridPos = gridObject.WorldToGrid(transform.position);

                Vector2 posInc = Vector2.zero;
                if (moveVector.x < 0) posInc.x -= 1.0f;
                if (moveVector.x > 0) posInc.x += 1.0f;
                if (moveVector.y < 0) posInc.y -= 1.0f;
                if (moveVector.y > 0) posInc.y += 1.0f;

                if (posInc.x != 0.0f)
                {
                    posInc.y = 0.0f;
                }

                if (relativeAxis)
                {
                    posInc = transform.TransformDirection(posInc);

                    if (posInc.x < -1e-3) posInc.x = -1.0f;
                    else if (posInc.x > 1e-3) posInc.x = 1.0f;
                    else posInc.x = 0.0f;

                    if (posInc.x == 0.0f)
                    {
                        if (posInc.y < -1e-3) posInc.y = -1.0f;
                        else if (posInc.y > 1e-3) posInc.y = 1.0f;
                        else posInc.y = 0.0f;
                    }
                    else posInc.y = 0.0f;
                }

                Vector2Int nextGridPos = gridPos;
                nextGridPos.x = (int)(nextGridPos.x + posInc.x);
                nextGridPos.y = (int)(nextGridPos.y + posInc.y);

                currentVelocity = posInc * GetSpeed();

                if (gridObject.MoveToGrid(nextGridPos, GetSpeed()))
                {
                    moveCooldownTimer = cooldown;
                }
            }
            else
            {
                // This code forces a direction after the movement
                if (currentVelocity.magnitude > 1e-3f)
                {
                    currentVelocity = currentVelocity.normalized * 0.5f;
                }
                else currentVelocity = Vector2.zero;
            }
        }

        protected void TurnInDirection(Vector2 moveVector)
        {
            if (moveVector != Vector2.zero)
            {
                Vector2 posInc = Vector2.zero;
                if (moveVector.x < 0) posInc.x -= 1.0f;
                if (moveVector.x > 0) posInc.x += 1.0f;
                if (moveVector.y < 0) posInc.y -= 1.0f;
                if (moveVector.y > 0) posInc.y += 1.0f;

                if (posInc.x != 0.0f)
                {
                    posInc.y = 0.0f;
                }

                gridObject.TurnTo(posInc);
            }
            else
            {
                // This code forces a direction after the movement
                if (currentVelocity.magnitude > 1e-3f)
                {
                    currentVelocity = currentVelocity.normalized * 0.5f;
                }
                else currentVelocity = Vector2.zero;
            }
        }
    }
}
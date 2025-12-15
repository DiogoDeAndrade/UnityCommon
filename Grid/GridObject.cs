using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace UC
{

    public class GridObject : MonoBehaviour
    {
        [SerializeField]
        private bool        snapOnStart = false;
        [SerializeField, FormerlySerializedAs("checkCollisionOnMove")]
        private bool        solidObject = true;
        [SerializeField, Range(-1.0f, 1.0f), ShowIf(nameof(solidObject))]
        private float       moveAnimationOnCollision = 0.0f;
        [SerializeField]
        private AudioClip   moveSnd;
        [SerializeField, ShowIf(nameof(solidObject))]
        private AudioClip   moveFailSnd;

        public delegate void OnMove(Vector2Int sourcePos, Vector2Int destPos);
        public event OnMove onMove;
        public delegate void OnMoveEnd(Vector2Int sourcePos, Vector2Int destPos, bool success);
        public event OnMoveEnd onMoveEnd;
        public delegate void OnTurnTo(Vector2Int sourcePos, Vector2Int destPos);
        public event OnTurnTo onTurnTo;

        private GridSystem _gridSystem;
        private Tilemap tilemap;
        private int facingDirection;
        private Tweener.BaseInterpolator moveInterpolator;

        public bool isMoving => (moveInterpolator != null) && (!moveInterpolator.isFinished);
        public bool isSolid
        {
            get => solidObject;
            set => solidObject = value;
        }
        public Vector2 lastDelta { get; private set; }
        public Vector2 cellSize => _gridSystem.cellSize;
        public GridSystem gridSystem => _gridSystem;

        private void Awake()
        {
            tilemap = GetComponent<Tilemap>();
        }

        private void Start()
        {
            facingDirection = 0;

            if (snapOnStart)
            {
                ClampToGrid();
            }
        }

        private void OnEnable()
        {
            _gridSystem = GetComponentInParent<GridSystem>();
            if (_gridSystem == null)
            {
                _gridSystem = FindFirstObjectByType<GridSystem>();
                transform.SetParent(_gridSystem.transform);
            }
            _gridSystem?.Register(this);
        }

        private void OnDisable()
        {
            _gridSystem = GetComponentInParent<GridSystem>();
            _gridSystem?.Unregister(this);
        }

        void ClampToGrid()
        {
            transform.position = Snap(transform.position);
        }

        public Vector2Int gridPosition => _gridSystem.WorldToGrid(transform.position);
        public Vector3 Snap(Vector3 position) => _gridSystem.Snap(position);
        public Vector2Int WorldToGrid(Vector3 worldPosition) => _gridSystem.WorldToGrid(worldPosition);
        public Vector2 GridToWorld(Vector3Int gridPosition) => _gridSystem.GridToWorld(gridPosition);
        public Vector2 GridToWorld(Vector2Int gridPosition) => _gridSystem.GridToWorld(gridPosition);

        public bool MoveToGrid(Vector2Int gridPos, Vector2 speed)
        {
            // This only moves one tile in any direction, and doesn't allow for diagonals

            var targetWorldPos = GridToWorld(gridPos.xy0());
            var originalGridPos = WorldToGrid(transform.position);

            Vector2Int deltaPos = Vector2Int.zero;
            if (gridPos.x < originalGridPos.x) deltaPos.x = -1;
            else if (gridPos.x > originalGridPos.x) deltaPos.x = 1;
            else if (gridPos.y < originalGridPos.y) deltaPos.y = -1;
            else if (gridPos.y > originalGridPos.y) deltaPos.y = 1;

            var endPosGrid = originalGridPos + deltaPos;
            Vector3 endPos = GridToWorld(endPosGrid);

            // Check if player can move to this position
            var worldDistance = (endPos - transform.position).magnitude;
            float moveTime = worldDistance / speed.magnitude;

            if (solidObject)
            {
                if (_gridSystem.CheckCollision(endPosGrid, this))
                {
                    if (moveAnimationOnCollision > 0.0f)
                    {
                        // Midway to the position
                        endPos = transform.position + (endPos - transform.position) * moveAnimationOnCollision;

                        moveInterpolator = transform.MoveToWorld(endPos, moveTime, "Move").EaseFunction(HalfwayInAndOut).Event(0.5f,
                        () =>
                        {
                            moveInterpolator = null;
                            onMoveEnd?.Invoke(originalGridPos, endPosGrid, false);
                        });

                        if (moveFailSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveFailSnd, 1.0f, UnityEngine.Random.Range(0.7f, 1.3f));
                    }

                    ComputeFacingFromVector(deltaPos);

                    onTurnTo?.Invoke(originalGridPos, endPosGrid);
                    return false;
                }
            }

            moveInterpolator = transform.MoveToWorld(endPos, moveTime, "Move").EaseFunction(Ease.OutBack).Done(
                () =>
                {
                    moveInterpolator = null;
                    onMoveEnd?.Invoke(originalGridPos, endPosGrid, true);
                });

            ComputeFacingFromVector(deltaPos);

            lastDelta = endPos - transform.position;

            onTurnTo?.Invoke(originalGridPos, endPosGrid);
            onMove?.Invoke(originalGridPos, endPosGrid);

            if (moveSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveSnd, 1.0f, UnityEngine.Random.Range(0.7f, 1.3f));

            return true;
        }

        private float HalfwayInAndOut(float t)
        {
            if (t > 0.5f) return (1.0f - t);
            return t;
        }

        public void TurnTo(Vector2 dir)
        {
            ComputeFacingFromVector(dir);
            onTurnTo?.Invoke(gridPosition, gridPosition);
        }
        public void TurnTo(Vector2Int targetPos)
        {
            Vector3 dir = targetPos.xy0() - gridPosition.xy0();
            ComputeFacingFromVector(dir);
            onTurnTo?.Invoke(gridPosition, gridPosition);
        }

        public void TeleportTo(Vector2 target)
        {
            var originalPos = WorldToGrid(transform.position);
            transform.position = Snap(target);
            moveInterpolator?.Interrupt();
            moveInterpolator = null;

            onMove?.Invoke(originalPos, WorldToGrid(target));
        }

        void ComputeFacingFromVector(Vector2 deltaPos)
        {
            facingDirection = -1;

            if (Mathf.Abs(deltaPos.x) < Mathf.Abs(deltaPos.y))
            {
                // More movement in Y than X
                if (deltaPos.y > 0.0f)
                {
                    facingDirection = 2;
                }
                else if (deltaPos.y < 0.0f)
                {
                    facingDirection = 0;
                }
            }
            else
            {
                if (deltaPos.x > 0.0f)
                {
                    facingDirection = 3;
                }
                else if (deltaPos.x < 0.0f)
                {
                    facingDirection = 1;
                }
            }
        }

        public Vector2Int GetPositionFacing()
        {
            return WorldToGrid(transform.position) + GetFacingDirection2i();
        }

        public int GetFacingDirection() => facingDirection;
        public Vector2Int GetFacingDirection2i()
        {
            if (facingDirection == 0) return new Vector2Int(0, -1);
            if (facingDirection == 1) return new Vector2Int(-1, 0);
            if (facingDirection == 2) return new Vector2Int(0, 1);
            if (facingDirection == 3) return new Vector2Int(1, 0);

            return Vector2Int.zero;
        }

        public bool IsFacingPosition(Vector2Int gridPos, float halfFovDegrees = 45f)
        {
            return IsFacingPosition(gridSystem.GridToWorld(gridPos), halfFovDegrees);
        }

        public bool IsFacingPosition(Vector3 targetWorldPosition, float halfFovDegrees = 45f)
        {
            Vector3 toTarget = targetWorldPosition - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < Mathf.Epsilon)
                return true; 

            Vector2 facing = GetFacingDirection2i();
            if (facing == Vector2Int.zero)
                return false;

            Vector3 facingDir = new Vector3(facing.x, 0f, facing.y).normalized;
            Vector3 targetDir = toTarget.normalized;

            float dot = Vector3.Dot(facingDir, targetDir);

            float threshold = Mathf.Cos(halfFovDegrees * Mathf.Deg2Rad);

            return dot >= threshold;
        }


        public void GatherActions(GridObject subject, Vector2Int position, List<GridActionContainer.NamedAction> actions)
        {
            var objActions = GetComponents<GridActionContainer>();

            // Not a tilemap, just a single grid cell
            bool isOnTile = (tilemap) && (tilemap.GetTile(position.xy0()) != null);

            if (((_gridSystem.WorldToGrid(transform.position) == position) && (tilemap == null)) || (isOnTile))
            {
                foreach (var action in objActions)
                {
                    action.GatherActions(subject, position, actions);
                }
            }
        }
    }
}
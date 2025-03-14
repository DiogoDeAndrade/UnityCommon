using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridObject : MonoBehaviour
{
    [SerializeField] 
    private bool snapOnStart = false;
    [SerializeField] 
    private bool checkCollisionOnMove = true;
    [SerializeField, Range(-1.0f, 1.0f), ShowIf(nameof(checkCollisionOnMove))]
    private float moveAnimationOnCollision = 0.0f;

    public delegate void OnMove(Vector2Int sourcePos, Vector2Int destPos);
    public event OnMove onMove;
    public delegate void OnMoveEnd(Vector2Int sourcePos, Vector2Int destPos);
    public event OnMoveEnd onMoveEnd;
    public delegate void OnTurnTo(Vector2Int sourcePos, Vector2Int destPos);
    public event OnTurnTo onTurnTo;    

    private GridSystem                  gridSystem;
    private Tilemap                     tilemap;
    private int                         facingDirection;    
    private Tweener.BaseInterpolator    moveInterpolator;

    public bool isMoving => (moveInterpolator != null) && (!moveInterpolator.isFinished);
    public Vector2 lastDelta { get; private set; }
    public Vector2 cellSize => gridSystem.cellSize;

    private void Awake()
    {
        tilemap = GetComponent<Tilemap>();
    }

    private void Start()
    {
        if (snapOnStart)
        {
            ClampToGrid();
        }

        facingDirection = 0;
    }

    private void OnEnable()
    {
        gridSystem = GetComponentInParent<GridSystem>();
        gridSystem?.Register(this);
    }

    private void OnDisable()
    {
        gridSystem = GetComponentInParent<GridSystem>();
        gridSystem?.Unregister(this);
    }

    void ClampToGrid()
    {
        transform.position = Snap(transform.position);
    }

    public Vector3 Snap(Vector3 position) => gridSystem.Snap(position);
    public  Vector2Int WorldToGrid(Vector3 worldPosition) => gridSystem.WorldToGrid(worldPosition);
    public Vector2 GridToWorld(Vector3Int gridPosition) => gridSystem.GridToWorld(gridPosition);
    public Vector2 GridToWorld(Vector2Int gridPosition) => gridSystem.GridToWorld(gridPosition);

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

        if (checkCollisionOnMove)
        {
            if (gridSystem.CheckCollision(endPosGrid, this))
            {
                if (moveAnimationOnCollision > 0.0f)
                {
                    // Midway to the position
                    endPos = transform.position + (endPos - transform.position) * moveAnimationOnCollision;

                    moveInterpolator = transform.MoveToWorld(endPos, moveTime, "Move").EaseFunction(HalfwayInAndOut).Done(
                    () =>
                    {
                        moveInterpolator = null;
                        onMoveEnd?.Invoke(originalGridPos, endPosGrid);
                    });
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
                onMoveEnd?.Invoke(originalGridPos, endPosGrid);
            });

        ComputeFacingFromVector(deltaPos);

        lastDelta = endPos - transform.position;

        onTurnTo?.Invoke(originalGridPos, endPosGrid);
        onMove?.Invoke(originalGridPos, endPosGrid);

        return true;
    }

    private float HalfwayInAndOut(float t)
    {
        if (t > 0.5f) return (1.0f - t);
        return t;
    }

    public void TeleportTo(Vector2 target)
    {
        var originalPos = WorldToGrid(transform.position);
        transform.position = Snap(target);
        moveInterpolator.Interrupt();
        moveInterpolator = null;

        onMove(originalPos, WorldToGrid(target));
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

    public void GatherActions(GridObject subject, Vector2Int position, List<GridAction> actions)
    {
        var objActions = GetComponents<GridAction>();

        // Not a tilemap, just a single grid cell
        bool isOnTile = (tilemap) && (tilemap.GetTile(position.xy0()) != null);

        if ((gridSystem.WorldToGrid(transform.position) == position) || (isOnTile))
        {                
            foreach (var action in objActions)
            {
                if (action.CanRunAction(subject, position))
                {
                    actions.Add(action);
                }
            }
        }
    }
}

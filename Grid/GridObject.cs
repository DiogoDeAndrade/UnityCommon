using NaughtyAttributes;
using System;
using UnityEditor.ShaderKeywordFilter;
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
    public delegate void OnTurnTo(Vector2Int sourcePos, Vector2Int destPos);
    public event OnTurnTo onTurnTo;

    private GridSystem  gridSystem;
    
    private Tweener.BaseInterpolator    moveInterpolator;

    public bool isMoving => (moveInterpolator != null) && (!moveInterpolator.isFinished);
    public Vector2 lastDelta { get; private set; }

    private void Start()
    {
        if (snapOnStart)
        {
            ClampToGrid();
        }
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
                    });
                }

                onTurnTo?.Invoke(originalGridPos, endPosGrid);
                return false;
            }
        }

        moveInterpolator = transform.MoveToWorld(endPos, moveTime, "Move").EaseFunction(Ease.OutBack).Done(
            () =>
            {
                moveInterpolator = null;
            });

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
}

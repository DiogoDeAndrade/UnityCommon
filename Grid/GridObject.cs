using System;
using UnityEngine;

public class GridObject : MonoBehaviour
{
    public delegate void OnMove(Vector2Int sourcePos, Vector2Int destPos);
    public event OnMove onMove;

    private GridSystem  gridSystem;
    
    private Tweener.BaseInterpolator    moveInterpolator;

    public bool isMoving => (moveInterpolator != null) && (!moveInterpolator.isFinished);
    public Vector2 lastDelta { get; private set; }

    private void Start()
    {
        ClampToGrid();
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

        // Check if object can move to this position
        var targetWorldPos = GridToWorld(gridPos.xy0());
        var originalGridPos = WorldToGrid(transform.position);

        Vector2Int deltaPos = Vector2Int.zero;
        if (gridPos.x < originalGridPos.x) deltaPos.x = -1;
        else if (gridPos.x > originalGridPos.x) deltaPos.x = 1;
        else if (gridPos.y < originalGridPos.y) deltaPos.y = -1;
        else if (gridPos.y > originalGridPos.y) deltaPos.y = 1;

        var endPosGrid = originalGridPos + deltaPos;
        Vector3 endPos = GridToWorld(endPosGrid);

        var worldDistance = (endPos - transform.position).magnitude;
        float moveTime = worldDistance / speed.magnitude;

        moveInterpolator = transform.MoveToWorld(endPos, moveTime, "Move").EaseFunction(Ease.OutBack).Done(
            () =>
            {
                moveInterpolator = null;
            });

        onMove(originalGridPos, endPosGrid);

        return true;
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

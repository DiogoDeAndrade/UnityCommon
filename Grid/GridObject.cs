using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class GridObject : MonoBehaviour
{
    private Grid        grid;
    private Vector2     originalPos;
    private Vector3     gridOffset;
    
    private Tweener.BaseInterpolator    moveInterpolator;

    public bool isMoving => (moveInterpolator != null) && (!moveInterpolator.isFinished);
    public Vector2 lastDelta { get; private set; }

    private void Awake()
    {
        grid = GetComponentInParent<Grid>();
        if (grid == null)
        {
            grid = FindFirstObjectByType<Grid>();
        }
        gridOffset = new Vector3(grid.cellSize.x * 0.5f, grid.cellSize.y * 0.5f, 0.0f);

        ClampToGrid();
    }

    void ClampToGrid()
    {
        transform.position = Snap(transform.position);
    }

    void Update()
    {
    }

    public Vector2Int WorldToGrid(Vector3 position)
    {
        return grid.WorldToCell(position).xy();
    }

    public Vector3 GridToWorld(Vector3Int gridPos)
    {
        return grid.CellToWorld(gridPos) + gridOffset;
    }

    public Vector3 GridToWorld(Vector2Int gridPos)
    {
        return grid.CellToWorld(gridPos.xy0()) + gridOffset;
    }

    public Vector2 Snap(Vector3 position)
    {
        // Set to grid
        var gridPos = grid.WorldToCell(position);

        return GridToWorld(gridPos);
    }

    public bool MoveToGrid(Vector2Int gridPos, Vector2 speed)
    {
        // Check if object can move to this position
        var targetWorldPos = GridToWorld(gridPos.xy0());
        var originalGridPos = WorldToGrid(transform.position);

        Vector2 deltaPos = targetWorldPos - transform.position;
        deltaPos.x = Mathf.Floor(deltaPos.x / grid.cellSize.x);
        deltaPos.y = Mathf.Floor(deltaPos.y / grid.cellSize.y);

        if (deltaPos.x != 0)
        {
            deltaPos.x = Mathf.Clamp(deltaPos.x , - 1.0f, 1.0f);
            deltaPos.y = 0.0f;            
        }
        else if (deltaPos.y != 0)
        {
            deltaPos.y = Mathf.Clamp(deltaPos.y, -1.0f, 1.0f);
        }

        float moveTime = deltaPos.magnitude / speed.magnitude;

        Vector3 endPos = GridToWorld(originalGridPos + deltaPos.toInt());

        moveInterpolator = transform.MoveToWorld(endPos, moveTime, "Move").EaseFunction(Ease.OutBack).Done(
            () =>
            {
                moveInterpolator = null;
            });

        return true;
    }

    internal void TeleportTo(Vector2 target)
    {
        originalPos = transform.position;
        transform.position = Snap(target);
        moveInterpolator.Interrupt();
        moveInterpolator = null;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Grid))]
public class GridSystem : MonoBehaviour
{
    private Grid                grid;
    private List<GridObject>    gridObjects = new();
    private List<GridCollider>  gridColliders = new();
    private Vector3             gridOffset;

    public Vector2 cellSize => grid.cellSize;

    public void Awake()
    {
        grid = GetComponent<Grid>();
        gridOffset = new Vector3(grid.cellSize.x * 0.5f, grid.cellSize.y * 0.5f, 0.0f);
    }

    public void Register(GridObject gridObject)
    {
        if (gridObjects.Contains(gridObject)) return;

        gridObjects.Add(gridObject);
    }

    public void Unregister(GridObject gridObject)
    {
        if (!gridObjects.Contains(gridObject)) return;

        gridObjects.Remove(gridObject);
    }
    public void Register(GridCollider gridCollider)
    {
        if (gridColliders.Contains(gridCollider)) return;

        gridColliders.Add(gridCollider);
    }

    public void Unregister(GridCollider gridCollider)
    {
        if (!gridColliders.Contains(gridCollider)) return;

        gridColliders.Remove(gridCollider);
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

    public bool CheckCollision(Vector2Int endPosGrid, GridObject gridObject)
    {
        foreach (var collider in gridColliders)
        {
            // Check if this collider is on the same object as the given object
            if (collider.transform.IsChildOf(gridObject.transform)) continue;

            if (collider.IsIntersecting(endPosGrid))
            {
                return true;
            }
        }

        return false;
    }
}

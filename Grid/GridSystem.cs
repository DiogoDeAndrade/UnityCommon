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

    public List<GridAction> GetActions(GridObject subject, Vector2Int position)
    {
        var ret = new List<GridAction>();

        // Get actions on subject
        var actionsOnSubject = subject.GetComponents<GridAction>();
        foreach (var action in actionsOnSubject)
        {
            action.GatherActions(subject, position, ret);
        }

        foreach (var obj in gridObjects)
        {
            obj.GatherActions(subject, position, ret);
        }

        return ret;
    }

    public GridObject FindVonNeumann(int radius, Vector3 worldPosition, Func<GridObject, bool> predicate)
    {
        Vector2Int center = WorldToGrid(worldPosition);
        return FindVonNeumann(radius, center, predicate);
    }

    public GridObject FindVonNeumann(int radius, Vector2Int center, Func<GridObject, bool> predicate)
    {
        for (int r = 0; r <= radius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                int dy = r - Mathf.Abs(dx);

                // Check both dy and -dy, but avoid duplicate when dy is zero
                Vector2Int[] positions = dy == 0
                    ? new[] { center + new Vector2Int(dx, 0) }
                    : new[] { center + new Vector2Int(dx, dy), center + new Vector2Int(dx, -dy) };

                foreach (var pos in positions)
                {
                    foreach (var obj in gridObjects)
                    {
                        if (WorldToGrid(obj.transform.position) == pos && predicate(obj))
                        {
                            return obj;
                        }
                    }
                }
            }
        }

        return null;
    }
}

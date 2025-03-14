using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridAction_TileArea : GridAction
{
    [SerializeField] private GridAction[]   actions;
    [SerializeField] private Color          debugColor = Color.yellow;

    Tilemap tilemap;

    void Awake()
    {
        tilemap = GetComponent<Tilemap>();
    }

    public override void GatherActions(GridObject subject, Vector2Int position, List<GridAction> retActions)
    {
        foreach (var action in actions)
        {
            var collider = action.GetComponent<Collider2D>();

            var worldBounds = new BoundsInt();
            worldBounds.min = tilemap.WorldToCell(collider.bounds.min);
            worldBounds.max = tilemap.WorldToCell(collider.bounds.max) + Vector3Int.one;

            if (worldBounds.Contains(position.xy0()))
            {
                retActions.Add(action);
            }
        }
    }

    public override bool RunAction(GridObject subject, Vector2Int position)
    {
        throw new System.NotImplementedException();
    }

    public void OnDrawGizmosSelected()
    {
        var grid = GetComponentInParent<Grid>();
        if (grid == null) return;
        if (actions == null) return;

        foreach (var action in actions)
        {
            var collider = action.GetComponent<Collider2D>();
            if (collider == null) continue;
            
            Gizmos.color = debugColor;
            var worldBounds = new Bounds();
            worldBounds.min = grid.CellToWorld(grid.WorldToCell(collider.bounds.min));
            worldBounds.max = grid.CellToWorld(grid.WorldToCell(collider.bounds.max) + Vector3Int.one);
            DebugHelpers.DrawBox(worldBounds);
        }
    }
}

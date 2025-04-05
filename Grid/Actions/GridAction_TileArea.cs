using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UC
{

    public class GridAction_TileArea : GridActionContainer
    {
        [SerializeField] private GridActionContainer[] actions;
        [SerializeField] private Color debugColor = Color.yellow;

        Tilemap tilemap;

        void Awake()
        {
            tilemap = GetComponent<Tilemap>();
        }

        public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
        {
            foreach (var action in actions)
            {
                var collider = action.GetComponent<Collider2D>();

                var worldBounds = new BoundsInt();
                worldBounds.min = tilemap.WorldToCell(collider.bounds.min);
                worldBounds.max = tilemap.WorldToCell(collider.bounds.max) + Vector3Int.one;

                if (worldBounds.Contains(position.xy0()))
                {
                    action.ActualGatherActions(subject, position, retActions);
                }
            }
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
}
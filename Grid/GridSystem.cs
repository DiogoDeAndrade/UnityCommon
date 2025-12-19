using System;
using System.Collections.Generic;
using System.Linq;
using UC.RPG;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UC
{

    [RequireComponent(typeof(Grid))]
    public class GridSystem : MonoBehaviour
    {
        private Grid                    grid;
        private List<GridObject>        gridObjects = new();
        private List<GridCollider>      gridColliders = new();
        private Vector3                 gridOffset;
        private List<Tilemap>           tilemaps;

        public Vector2 cellSize => grid.cellSize;

        public void Awake()
        {
            grid = GetComponent<Grid>();
            gridOffset = new Vector3(grid.cellSize.x * 0.5f, grid.cellSize.y * 0.5f, 0.0f);
            tilemaps = GetComponentsInChildren<Tilemap>().ToList();
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

        public Rect GetGridCellRectangle(Vector3 position)
        {
            var snappedPos = Snap(position);

            return new Rect(snappedPos.x - cellSize.x * 0.5f, snappedPos.y - cellSize.y * 0.5f, cellSize.x, cellSize.y);
        }

        public bool CheckCollision(Vector2Int endPosGrid, GridObject gridObject)
        {
            foreach (var collider in gridColliders)
            {
                // Check if this collider is on the same object as the given object
                if (collider.transform.IsChildOf(gridObject.transform)) continue;
                if ((collider.gridObject) && (!collider.gridObject.isSolid)) continue;

                if (collider.IsIntersecting(endPosGrid))
                {
                    return true;
                }
            }

            return false;
        }

        public List<GridActionContainer.NamedAction> GetActions(GridObject subject, Vector2Int position)
        {
            var ret = new List<GridActionContainer.NamedAction>();

            // Get actions on subject
            var actionsOnSubject = subject.GetComponents<GridActionContainer>();
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

        public bool FindMoore(int radius, Vector3 worldPosition, Func<GridObject, Vector2Int, bool> predicate, out GridObject ret, out Vector2Int retPos)
        {
            Vector2Int center = WorldToGrid(worldPosition);
            return FindMoore(radius, center, predicate, out ret, out retPos);
        }

        public bool FindMoore(int radius, Vector2Int center, Func<GridObject, Vector2Int, bool> predicate, out GridObject ret, out Vector2Int retPos)
        {
            ret = null;
            retPos = Vector2Int.zero;

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
                        bool foundObj = false;
                        foreach (var obj in gridObjects)
                        {
                            if (WorldToGrid(obj.transform.position) == pos)
                            {
                                foundObj = true;
                                if (predicate(obj, pos))
                                {
                                    ret = obj;
                                    retPos = pos;
                                    return true;
                                }
                            }
                        }
                        if (!foundObj)
                        {
                            if (predicate(null, pos))
                            {
                                retPos = pos;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public bool FindVonNeumann(int radius, Vector3 worldPosition, Func<GridObject, Vector2Int, bool> predicate, out GridObject ret, out Vector2Int retPos)
        {
            Vector2Int center = WorldToGrid(worldPosition);
            return FindVonNeumann(radius, center, predicate, out ret, out retPos);
        }

        public bool FindVonNeumann(int radius, Vector2Int center, Func<GridObject, Vector2Int, bool> predicate, out GridObject ret, out Vector2Int retPos)
        {
            ret = null;
            retPos = Vector2Int.zero;

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
                        bool foundObj = false;
                        foreach (var obj in gridObjects)
                        {
                            if (WorldToGrid(obj.transform.position) == pos)
                            {
                                foundObj = true;
                                if (predicate(obj, pos))
                                {
                                    ret = obj;
                                    retPos = pos;
                                    return true;
                                }
                            }
                        }
                        if (!foundObj)
                        {
                            if (predicate(null, pos))
                            {
                                retPos = pos;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public bool FindInRadius(float radius, Vector3 worldPosition, Func<GridObject, Vector2Int, bool> predicate, out GridObject ret, out Vector2Int retPos)
        {
            Vector2Int center = WorldToGrid(worldPosition);
            return FindInRadius(radius, center, predicate, out ret, out retPos);
        }

        public bool FindInRadius(float radius, Vector2Int center, Func<GridObject, Vector2Int, bool> predicate, out GridObject ret, out Vector2Int retPos)
        {
            ret = null;
            retPos = Vector2Int.zero;
            int ceilRadius = Mathf.CeilToInt(radius);

            for (int dy = -ceilRadius; dy <= ceilRadius; dy++)
            {
                for (int dx = -ceilRadius; dx <= ceilRadius; dx++)
                {
                    Vector2Int offset = new Vector2Int(dx, dy);
                    if (offset.sqrMagnitude > radius * radius) continue;

                    Vector2Int pos = center + offset;

                    bool foundObj = false;
                    foreach (var obj in gridObjects)
                    {
                        if (WorldToGrid(obj.transform.position) == pos)
                        {
                            foundObj = true;
                            if (predicate(obj, pos))
                            {
                                ret = obj;
                                retPos = pos;
                                return true;
                            }
                        }
                    }
                    if (!foundObj)
                    {
                        if (predicate(null, pos))
                        {
                            retPos = pos;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public List<GridObject> GetGridObjectsAt(Vector2Int gridPos)
        {
            List<GridObject> result = new List<GridObject>();

            foreach (var obj in gridObjects)
            {
                if (WorldToGrid(obj.transform.position) == gridPos)
                {
                    result.Add(obj);
                }
            }

            return result;
        }

        public List<GridObject> GetGridObjectsAt(Vector2 worldPos, bool forInteraction = false)
        {
            List<GridObject> result = new List<GridObject>();

            foreach (var obj in gridColliders)
            {
                if (obj.IsIntersecting(worldPos, forInteraction))
                {
                    result.Add(obj.gridObject);
                }
            }

            return result;
        }

        public GridObject GetFirstGridObjectAt(Vector2Int gridPos)
        {
            foreach (var obj in gridObjects)
            {
                if (WorldToGrid(obj.transform.position) == gridPos)
                {
                    return obj;
                }
            }

            return null;
        }

        public GridObject GetFirstGridObjectAt(Vector2 worldPos, bool forInteraction)
        {
            foreach (var obj in gridColliders)
            {
                if (obj.IsIntersecting(worldPos, forInteraction))
                {
                    return obj.gridObject;
                }
            }

            return null;
        }

        public List<GridObject> GetGridObjectsAt(Vector3 worldPos, bool tileBasedPosition)
        {
            if (tileBasedPosition)
                return GetGridObjectsAt(WorldToGrid(worldPos));
            else
                return GetGridObjectsAt(worldPos);
        }

        public GridObject GetFirstGridObjectAt(Vector3 worldPos, bool tileBasedPosition, bool forInteraction)
        {
            if (tileBasedPosition)
                return GetFirstGridObjectAt(WorldToGrid(worldPos));
            else
                return GetFirstGridObjectAt(worldPos, forInteraction);
        }

        public List<TileBase> GetTiles(Vector2Int pos)
        {
            List<TileBase> ret = new();

            foreach (var tilemap in tilemaps)
            {
                var tile = tilemap.GetTile(pos.xy0());
                if (tile) ret.Add(tile);
            }

            return ret;
        }
    }
}
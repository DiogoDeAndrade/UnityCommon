using System;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UC
{

    public class GridCollider : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        GridSystem              gridSystem;
        TilemapCollider2D       tilemapCollider;
        CompositeCollider2D     compositeCollider;
        GridObject              _gridObject;

        bool isTilemapCollider = false;
        Vector3 tolerance = new Vector3(1.0f, 1.0f, 0.0f);

        public GridObject gridObject => _gridObject;

        void OnEnable()
        {
            _gridObject = GetComponent<GridObject>();
            gridSystem = GetComponentInParent<GridSystem>();
            gridSystem?.Register(this);
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

            if (GetComponent<Tilemap>())
            {
                isTilemapCollider = true;
                tilemapCollider = GetComponent<TilemapCollider2D>();
                compositeCollider = GetComponent<CompositeCollider2D>();
            }
        }

        void OnDisable()
        {
            gridSystem = GetComponentInParent<GridSystem>();
            gridSystem?.Unregister(this);
        }

        virtual public bool IsIntersecting(Vector2Int endPosGrid)
        {
            if (spriteRenderer)
            {
                var minGrid = gridSystem.WorldToGrid(spriteRenderer.bounds.min + tolerance);
                var maxGrid = gridSystem.WorldToGrid(spriteRenderer.bounds.max - tolerance);

                if ((endPosGrid.x >= minGrid.x) && (endPosGrid.x <= maxGrid.x) &&
                    (endPosGrid.y >= minGrid.y) && (endPosGrid.y <= maxGrid.y))
                {
                    return true;
                }
            }
            if (isTilemapCollider)
            {
                if (compositeCollider)
                {
                    var worldPoint = gridSystem.GridToWorld(endPosGrid);
                    return compositeCollider.OverlapPoint(worldPoint);
                }
                else if (tilemapCollider)
                {
                    var worldPoint = gridSystem.GridToWorld(endPosGrid);
                    return tilemapCollider.OverlapPoint(worldPoint);
                }
            }

            return false;
        }

        virtual public bool IsIntersecting(Vector2 worldPoint)
        {
            if (spriteRenderer)
            {
                var bounds = spriteRenderer.bounds;
                bounds.Expand(tolerance);

                if ((bounds.min.x < worldPoint.x) && (bounds.max.x > worldPoint.x) &&
                    (bounds.min.y < worldPoint.y) && (bounds.max.y > worldPoint.y))
                {
                    return true;
                }
            }
            if (isTilemapCollider)
            {
                if (compositeCollider)
                {
                    return compositeCollider.OverlapPoint(worldPoint);
                }
                else if (tilemapCollider)
                {
                    return tilemapCollider.OverlapPoint(worldPoint);
                }
            }

            return false;
        }
    }
}
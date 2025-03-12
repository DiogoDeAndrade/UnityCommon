using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(GridObject))]
public class GridCollider : MonoBehaviour
{
    GridSystem          gridSystem;
    SpriteRenderer      spriteRenderer;
    TilemapCollider2D   tilemapCollider;
    CompositeCollider2D compositeCollider;

    bool            isTilemapCollider = false;
    Vector3         tolerance = new Vector3(1.0f, 1.0f, 0.0f);

    void OnEnable()
    {
        gridSystem = GetComponentInParent<GridSystem>();
        gridSystem?.Register(this);
        spriteRenderer = GetComponent<SpriteRenderer>();

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
}

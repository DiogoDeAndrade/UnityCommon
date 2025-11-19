using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace UC
{

    public class GridCollider : MonoBehaviour
    {
        public enum Type { Tilemap, Sprite, Box };

        [SerializeField, ShowIf(nameof(isNotTilemap))] 
        private Type           colliderType = Type.Sprite;
        [SerializeField, ShowIf(nameof(isSpriteCollider))] 
        private SpriteRenderer spriteRenderer;
        [SerializeField, ShowIf(nameof(isBoxCollider))]
        private Rect           rectangle = new Rect(0.0f, 0.0f, 1.0f, 1.0f);
        [SerializeField]
        private bool           separateForInteraction = false;
        [SerializeField, ShowIf(nameof(separateForInteraction))]
        private Type           colliderTypeForInteraction = Type.Sprite;

        GridSystem              gridSystem;
        TilemapCollider2D       tilemapCollider;
        CompositeCollider2D     compositeCollider;
        GridObject              _gridObject;

        bool isTilemapCollider = false;
        Vector3 tolerance = new Vector3(1.0f, 1.0f, 0.0f);

        private bool isNotTilemap => !isTilemapCollider;
        private bool isSpriteCollider => (isNotTilemap && (colliderType == Type.Sprite)) || ((separateForInteraction) && (colliderTypeForInteraction == Type.Sprite));
        private bool isBoxCollider => isNotTilemap && (colliderType == Type.Box) || ((separateForInteraction) && (colliderTypeForInteraction == Type.Box));

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
            if ((colliderType == Type.Sprite) && (spriteRenderer))
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
            if (isBoxCollider)
            {
                var minGrid = gridSystem.WorldToGrid(transform.position + rectangle.min.xy0());
                var maxGrid = gridSystem.WorldToGrid(transform.position + rectangle.max.xy0());

                if ((endPosGrid.x >= minGrid.x) && (endPosGrid.x <= maxGrid.x) &&
                    (endPosGrid.y >= minGrid.y) && (endPosGrid.y <= maxGrid.y))
                {
                    return true;
                }
            }

            return false;
        }

        virtual public bool IsIntersecting(Vector2 worldPoint, bool forInteraction)
        {
            var ct = colliderType;
            if ((forInteraction) && (separateForInteraction))
            {
                ct = colliderTypeForInteraction;
            }

            if ((ct == Type.Sprite) && (spriteRenderer))
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
            if (ct == Type.Box)
            {
                var localPos = worldPoint - transform.position.xy();

                return rectangle.Contains(localPos);
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if ((colliderType == Type.Sprite) && (spriteRenderer))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(spriteRenderer.bounds.center, spriteRenderer.bounds.size);
            }
            if (isBoxCollider)
            {
                Gizmos.color = Color.green;
                var center = transform.position + rectangle.center.xy0();
                var size = new Vector3(rectangle.width, rectangle.height, 0.0f);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
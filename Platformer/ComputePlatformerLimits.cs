using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ComputePlatformerLimits : MonoBehaviour
{
    [SerializeField] Renderer[] renderers;

    private void OnEnable()
    {
        ComputeExtents();
    }

    [Button("Compute Extents")]
    void ComputeExtents()
    { 
        if ((renderers != null) && (renderers.Length > 0))
        {
            var worldBounds = renderers[0].bounds;
            for (int i = 0; i < renderers.Length; i++)
            {
                var tilemapRenderer = renderers[i] as TilemapRenderer;
                if (tilemapRenderer != null)
                {
                    var tilemap = tilemapRenderer.GetComponent<Tilemap>();
                    if (tilemap != null)
                    {
                        tilemap.CompressBounds();
                        tilemap.RefreshAllTiles();
                    }
                }
                var bounds = renderers[i].bounds;
                if (i == 0)
                    worldBounds = bounds;
                else
                    worldBounds.Encapsulate(bounds);
            }

            var collider = GetComponent<BoxCollider2D>();
            collider.offset = worldBounds.center - transform.position;
            collider.size = worldBounds.size;
        }

        Destroy(this);
    }
}

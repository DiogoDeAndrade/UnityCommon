using UnityEngine;

public class ComputePlatformerLImits : MonoBehaviour
{
    [SerializeField] Renderer[] renderers;


    void Start()
    {
        if ((renderers != null) && (renderers.Length > 0))
        {
            var worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                var bounds = renderers[i].bounds;
                worldBounds.Encapsulate(bounds);
            }

            var collider = GetComponent<BoxCollider2D>();
            collider.offset = worldBounds.center - transform.position;
            collider.size = worldBounds.size;
        }

        Destroy(this);
    }
}

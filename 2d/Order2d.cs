using UnityEngine;

namespace UC
{
    public class Order2d : MonoBehaviour
    {
        [SerializeField]
        private float       offsetZ = 0.0f;

        SpriteRenderer          spriteRenderer;
        TrailRenderer           trailRenderer;
        LineRenderer            lineRenderer;
        ParticleSystemRenderer  particleRenderer;

        protected void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            trailRenderer = GetComponent<TrailRenderer>();
            lineRenderer = GetComponent<LineRenderer>();
            particleRenderer = GetComponent<ParticleSystemRenderer>();
        }

        void LateUpdate()
        {
            UpdateOrder();
        }

        void UpdateOrder()
        { 
            if (Order2dConfig.orderMode == OrderMode.Z)
            {
                var pos = transform.position;
                pos.z = Order2dConfig.GetZ(pos, offsetZ);
                transform.position = pos;
            }
            else if ((Order2dConfig.orderMode == OrderMode.Order) && (spriteRenderer))
            {
                int order = (int)Mathf.Clamp(Order2dConfig.orderScaleY * transform.position.y, Order2dConfig.orderMin, Order2dConfig.orderMax);
                if (spriteRenderer)
                    spriteRenderer.sortingOrder = order;
                if (trailRenderer)
                    trailRenderer.sortingOrder = order;
                if (lineRenderer)
                    lineRenderer.sortingOrder = order;
                if (particleRenderer)
                    particleRenderer.sortingOrder = order;                    
            }
        }
    }
}

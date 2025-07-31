using UnityEngine;

namespace UC
{
    public class Order2d : MonoBehaviour
    {
        [SerializeField]
        private float       offsetZ = 0.0f;

        SpriteRenderer  spriteRenderer;

        protected void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
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
                pos.z = Mathf.Clamp(Order2dConfig.orderScaleY * pos.y + offsetZ, Order2dConfig.orderMinZ, Order2dConfig.orderMaxZ);
                transform.position = pos;
            }
            else if ((Order2dConfig.orderMode == OrderMode.Order) && (spriteRenderer))
            {
                spriteRenderer.sortingOrder = (int)Mathf.Clamp(Order2dConfig.orderScaleY * transform.position.y, Order2dConfig.orderMin, Order2dConfig.orderMax);
            }
        }
    }
}

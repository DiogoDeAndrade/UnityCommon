using UnityEngine;

namespace UC
{
    public class ItemWorld : MonoBehaviour
    {
        [SerializeField]
        private Item _item;
        [SerializeField]
        private int  _quantity = 1;
        [SerializeField, Tooltip("Tags of objects that can pickup this item")]
        private Hypertag[] pickupTargets;

        public Item item => _item;
        public int quantity => _quantity;

        SpriteRenderer spriteRenderer;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = _item.displaySprite;
            spriteRenderer.color = _item.displaySpriteColor;
        }

        public bool Pickup(Inventory inventory)
        {            
            return inventory.Add(_item, _quantity) > 0;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if ((pickupTargets == null) || (pickupTargets.Length == 0)) return;

            if (collision.gameObject.HasHypertags(pickupTargets))
            {
                var inventory = collision.GetComponent<Inventory>();
                if (inventory != null)
                {
                    if (Pickup(inventory))
                    {
                        Destroy(gameObject);
                    }
                }
            }
        }
    }
}

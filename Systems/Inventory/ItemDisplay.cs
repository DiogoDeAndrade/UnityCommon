using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UC
{

    public class ItemDisplay : MonoBehaviour
    {
        [SerializeField]
        private Image itemImage;
        [SerializeField]
        private TextMeshProUGUI itemCountText;

        public void Set(Item item, int count)
        {
            if (itemImage)
            {
                if ((item) && (count > 0))
                {
                    itemImage.sprite = item.displaySprite;
                    itemImage.color = item.displaySpriteColor;
                }
                else
                {
                    itemImage.sprite = null;
                    itemImage.color = new Color(1.0f, 1.0f, 1.0f, 0.0f);
                }
            }

            if (itemCountText)
            {
                if ((item == null) || (!item.isStackable) || (count < 2))
                {
                    itemCountText.enabled = false;
                }
                else
                {
                    itemCountText.enabled = true;
                    itemCountText.color = item.displayTextColor;
                    itemCountText.text = $"x{count}";
                }
            }
        }
    }
}
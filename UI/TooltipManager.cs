using UnityEngine;

namespace UC
{

    public class TooltipManager : MonoBehaviour
    {
        [SerializeField] private Tooltip tooltipPrefab;

        static TooltipManager instance;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private Tooltip _CreateTooltip()
        {
            var tooltip = Instantiate(tooltipPrefab, transform);
            return tooltip;
        }

        public static Tooltip CreateTooltip()
        {
            return instance._CreateTooltip();
        }
    }
}
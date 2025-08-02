using UnityEngine;

namespace UC
{
    [CreateAssetMenu(fileName = "Order2d_Config", menuName = "Unity Common/Order2d Config")]
    public class Order2dConfig : ScriptableObject
    {
        [SerializeField, Header("Sorting")]
        private OrderMode _orderMode = OrderMode.Z;
        [SerializeField]
        private float _orderScaleY = 0.0001f;
        [SerializeField]
        private int _orderMin = -1000;
        [SerializeField]
        private int _orderMax = 1000;
        [SerializeField]
        private float _orderMinZ = -5.0f;
        [SerializeField]
        private float _orderMaxZ = 5.0f;

        public static OrderMode orderMode => instance?._orderMode ?? OrderMode.Z;
        public static float orderScaleY => instance?._orderScaleY ?? 0.0001f;
        public static int orderMin => instance?._orderMin ?? -1000;
        public static int orderMax => instance?._orderMax ?? 1000;
        public static float orderMinZ => instance?._orderMinZ ?? -5.0f;
        public static float orderMaxZ => instance?._orderMaxZ ?? 5.0f;

        static Order2dConfig _instance = null;

        public static Order2dConfig instance
        {
            get
            {
                if (_instance) return _instance;

                Debug.Log("Order2d Config not loaded, loading...");

                var allConfigs = Resources.LoadAll<Order2dConfig>("");
                if (allConfigs.Length == 1)
                {
                    _instance = allConfigs[0];
                }

                return _instance;
            }
        }

        public static float GetZ(Vector3 position, float offsetZ)
        {
            Order2dConfig cachedInstance = instance;
            return Mathf.Clamp(cachedInstance._orderScaleY * position.y + offsetZ, cachedInstance._orderMinZ, cachedInstance._orderMaxZ);
        }
    }
}

using NaughtyAttributes;
using UnityEngine;
using UC.RPG;

namespace UC
{
    [CreateAssetMenu(fileName = "Globals", menuName = "Unity Common/Data/Globals (Base)")]
    public class GlobalsBase : ScriptableObject
    {
        [HorizontalLine(color: EColor.Red)]
        [SerializeField]
        private LayerMask   _obstacleMask;
        [SerializeField]
        private ResourceType _healthResource;

        public static LayerMask obstacleMask => instanceBase?._obstacleMask ?? ~0;
        public static ResourceType healthResource => instanceBase?._healthResource ?? null;

        protected static GlobalsBase _instanceBase = null;

        public static GlobalsBase instanceBase
        {
            get
            {
                if (_instanceBase) return _instanceBase;

                Debug.Log("Globals not loaded, loading...");

                var allConfigs = Resources.LoadAll<GlobalsBase>("");
                if (allConfigs.Length == 1)
                {
                    _instanceBase = allConfigs[0];
                }

                return _instanceBase;
            }
        }
    }
}

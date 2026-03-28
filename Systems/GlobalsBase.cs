using NaughtyAttributes;
using UnityEngine;
using UC.RPG;

namespace UC
{
    [CreateAssetMenu(fileName = "Globals", menuName = "Unity Common/Data/Globals/Base")]
    public class GlobalsBase : ScriptableObject
    {
        [HorizontalLine(color: EColor.Red)]
        [SerializeField]
        private LayerMask   _obstacleMask;
        [SerializeField]
        private LayerMask   _groundMask;
        [SerializeField]
        private ResourceType _healthResource;
        [SerializeField] 
        private Hypertag    _weaponSlot;

        public static LayerMask obstacleMask => instanceBase?._obstacleMask ?? ~0;
        public static LayerMask groundMask => instanceBase?._groundMask ?? ~0;
        public static ResourceType healthResource => instanceBase?._healthResource ?? null;
        public static Hypertag defaultWeaponSlot => instanceBase._weaponSlot;


        protected static GlobalsBase _instanceBase = null;

        public static T GetInstanceBase<T>() where T : GlobalsBase
        {
            if (_instanceBase) return (T)_instanceBase;

            Debug.Log("Globals not loaded, loading...");

            var allConfigs = Resources.LoadAll<T>("");
            if (allConfigs.Length == 0)
            {
                Debug.LogError("Globals not available - create a globals object!");
            }
            else if (allConfigs.Length == 1)
            {
                _instanceBase = allConfigs[0];
            }

            return (T)_instanceBase;
        }

        public static GlobalsBase instanceBase => GetInstanceBase<GlobalsBase>();
    }
}

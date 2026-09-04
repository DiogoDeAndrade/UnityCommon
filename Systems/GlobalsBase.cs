using NaughtyAttributes;
using UnityEngine;
using UC.RPG;
using Unity.Scripting.LifecycleManagement;

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
        [SerializeField]
        protected SoundDef  _uiMoveSnd;
        [SerializeField]
        protected SoundDef  _uiSelectSnd;
        [SerializeField]
        protected SoundDef  _uiChangeValueSnd;
        [SerializeField]
        protected TextTooltip _textTooltip;


        public static LayerMask obstacleMask => (instanceBase != null) ? (instanceBase._obstacleMask) : ~0;
        public static LayerMask groundMask => (instanceBase != null) ? (instanceBase._groundMask) : ~0;
        public static ResourceType healthResource => (instanceBase != null) ? (instanceBase._healthResource) : null;
        public static Hypertag defaultWeaponSlot => (instanceBase != null) ? (instanceBase._weaponSlot) : null;
        public static SoundDef uiMoveSnd => (instanceBase != null) ? (instanceBase._uiMoveSnd) : null;
        public static SoundDef uiSelectSnd => (instanceBase != null) ? (instanceBase._uiSelectSnd) : null;
        public static SoundDef uiChangeValueSnd => (instanceBase != null) ? (instanceBase._uiChangeValueSnd) : null;
        public static TextTooltip textTooltip => (instanceBase != null) ? (instanceBase._textTooltip) : null;


        [NoAutoStaticsCleanup]
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

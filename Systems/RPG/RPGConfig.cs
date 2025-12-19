using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UC.RPG;

namespace UC
{
    [CreateAssetMenu(fileName = "RPGConfig", menuName = "Unity Common/RPG/RPG Config")]
    public partial class RPGConfig : ScriptableObject
    {
        [Serializable]
        struct ArmourModuleFunctionElem
        {
            public DamageType           damageType;
            [SerializeReference]
            public ArmourModuleFunction armourModuleFunction;
        }

        [HorizontalLine(color: EColor.Green)]
        [SerializeField]
        private List<ArmourModuleFunctionElem> _armourFunctions;
        [SerializeField, Tooltip("This stat is used for the calculation of the K component in the armour mitigation code.")]
        private StatType _itemArmourKStat;

        List<ArmourModuleFunction> _GetArmourFunctions(DamageType damageType)
        {
            List<ArmourModuleFunction> ret = null;

            foreach (var elem in _armourFunctions)
            {
                if (elem.damageType == damageType)
                {
                    if (ret == null) ret = new();
                    ret.Add(elem.armourModuleFunction);
                }
            }
         
            return ret;
        }

        protected static RPGConfig _instanceBase = null;

        public static RPGConfig instanceBase
        {
            get
            {
                if (_instanceBase) return _instanceBase;

                Debug.Log("RPGConfig not loaded, loading...");

                var allConfigs = Resources.LoadAll<RPGConfig>("");
                if (allConfigs.Length == 1)
                {
                    _instanceBase = allConfigs[0];
                }

                return _instanceBase;
            }
        }

        public static List<ArmourModuleFunction> GetArmourFunctions(DamageType damageType)
        {
            return instanceBase?._GetArmourFunctions(damageType);
        }
        public static StatType itemArmourKStat => instanceBase?._itemArmourKStat;
    }
}

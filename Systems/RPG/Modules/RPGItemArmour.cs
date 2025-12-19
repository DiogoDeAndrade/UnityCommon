using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Item/Armour")]
    public class RPGItemArmour : SOModule
    {
        struct ArmourSubModule
        {
            public DamageType           damageType;
            public ArmourModuleFunction armourModule;
        }

        [SerializeField]
        private List<ArmourSubModule>    _armourModules;
    }
}

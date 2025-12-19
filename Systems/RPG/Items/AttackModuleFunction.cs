using System;
using UnityEngine;

namespace UC.RPG
{    
    [Serializable]
    public abstract class AttackModuleFunction
    {
        public abstract bool Attack(RPGEntity weapon, RPGEntity source, Vector2Int destPos);
        public abstract (float minDamage, float maxDamage, float hitChance) GetWeaponDataTooltip(RPGEntity weapon, RPGEntity source);
    }
}

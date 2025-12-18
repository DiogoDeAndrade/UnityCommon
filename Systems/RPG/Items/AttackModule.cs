using System;
using UC.RPG;
using UnityEngine;

namespace UC
{    
    [Serializable]
    public abstract class AttackModule
    {
        public abstract bool Attack(RPGEntity weapon, RPGEntity source, Vector2Int destPos);
        public abstract (float minDamage, float maxDamage, float hitChance) GetWeaponData(RPGEntity weapon, RPGEntity source);
    }
}

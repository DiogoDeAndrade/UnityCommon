using System;
using UnityEngine;

namespace UC.RPG
{    
    [Serializable]
    public abstract class AttackModuleFunction
    {
        public abstract bool Attack(RPGEntity weapon, RPGEntity source, Vector2Int destPos);
        public abstract Vector2 GetDamageRange(RPGEntity weapon, RPGEntity wielder);
        public abstract float GetHitChance(RPGEntity weapon, RPGEntity wielder);
    }
}

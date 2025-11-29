using System;
using UC.RPG;
using UnityEngine;

namespace UC
{    
    [Serializable]
    public abstract class AttackModule
    {
        public abstract bool Attack(Item weapon, RPGEntity source, Vector2Int destPos);
    }
}

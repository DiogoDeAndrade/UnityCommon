using UC.RPG;
using UnityEngine;

namespace UC.RPG
{ 
    public class StatInstance 
    {
        public StatType type;
        public float    value;

        public StatInstance(StatType type)
        {
            this.type = type;
            value = type.baseValue;
        }
    }
}
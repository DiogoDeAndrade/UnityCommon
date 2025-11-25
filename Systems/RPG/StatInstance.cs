using System;
using UC.RPG;
using UnityEngine;

namespace UC.RPG
{ 
    public class StatInstance 
    {
        public StatType type;
        private float    value;

        public StatInstance(StatType type)
        {
            this.type = type;
            value = type.baseValue;
        }

        public float GetValue()
        {
            return value;
        }

        public void SetValue(float v)
        {
            value = v;
        }
    }
}
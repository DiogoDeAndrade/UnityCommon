using System;
using UC.RPG;
using UnityEngine;

namespace UC.RPG
{ 
    public class StatInstance 
    {
        public  StatType        type;
        private float           value;
        private RPGEntity       owner;
        private RPGStatModule   statModule;

        public StatInstance(RPGEntity owner, RPGStatModule statModule)
        {
            type = statModule.type;
            this.statModule = statModule;
            this.owner = owner;
        }

        public float GetValue()
        {
            if (type.isUpdateAlways) Update();

            return value;
        }

        public void SetValue(float v)
        {
            value = v;
        }

        public void Update()
        {
            if (statModule)
            {
                if (statModule.calculator != null)
                {
                    SetValue(statModule.calculator.GetValue(owner));
                }
            }
        }
    }
}

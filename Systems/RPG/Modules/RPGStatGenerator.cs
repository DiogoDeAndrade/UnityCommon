using System;
using UC.Interaction;
using UC.RPG;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Stat Generator Module")]
    public class RPGStatGenerator : SOModule
    {
        [Serializable]
        public struct Generator
        {
            public StatType type;
            [SerializeReference]
            public ResourceValueFunction calculator;
        }

        [SerializeField]
        private Generator[] statGenerators;

        public bool RunGenerator(StatType type, RPGEntity character, StatInstance statInstance)
        {
            foreach (var g in statGenerators)
            {
                if ((g.type == type) && (g.calculator != null))
                {
                    statInstance.SetValue(g.calculator.GetValue(character));
                    return true;
                }
            }

            foreach (var p in scriptableObject.parents)
            {
                var g = p.GetModule<RPGStatGenerator>(true);
                if (g != null)
                {
                    if (g.RunGenerator(type, character, statInstance)) return true;
                }
            }

            return false;
        }
    }
}

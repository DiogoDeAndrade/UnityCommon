using System;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Stat Module")]
    public class RPGStatModule : SOModule
    {
        public StatType                 type;
        [SerializeReference]
        public ResourceValueFunction    calculator;

        public override string GetModuleHeaderString()
        {
            return (type != null) ? $"RPG Stat - [{type.name}]" : string.Empty;
        }
    }
}

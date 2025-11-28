using System;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Resource Module")]
    public class RPGResourceModule : SOModule
    {
        public ResourceType             type;
        [SerializeReference]
        public ResourceValueFunction    calculator;

        public override string GetModuleHeaderString()
        {
            return (type != null) ? $"RPG Resource - [{type.name}]" : string.Empty;
        }
    }
}

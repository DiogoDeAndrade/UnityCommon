using System;
using UC.Interaction;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Event/Base")]
    public abstract class RPGEvent : SOModule
    {
        public virtual void Init(RPGEntity entity) 
        { 
        }
    }
}

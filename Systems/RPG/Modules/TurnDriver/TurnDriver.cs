using System;
using UnityEngine;

namespace UC.RPG
{
    [Serializable]
    public abstract class TurnDriver : SOModule
    {
        // If we have condition based systems, should this be evaluated at all, or ignored?
        public virtual bool IsEnabled(UnityRPGEntity entity) => true;
        // What's the priority, bigger is better
        public virtual float GetPriority(UnityRPGEntity entity) => 0.0f;
        // Take the action
        public abstract bool Execute(UnityRPGEntity entity);
    }
}

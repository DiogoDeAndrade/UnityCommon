using System;
using UnityEngine;

namespace UC.RPG
{
    [Serializable]
    public abstract class TurnDriver : SOModule
    {
        // Initialize the turn state for this turn driver
        public virtual void Init(UnityRPGEntity entity, TurnState state)
        {

        }
        // If we have condition based systems, should this TurnDriver be evaluated at all, or ignored?
        public virtual bool IsEnabled(UnityRPGEntity entity, TurnState state) => true;
        // What's the priority, bigger is better
        public virtual float GetPriority(UnityRPGEntity entity, TurnState state) => 0.0f;
        // Take the action
        public abstract bool Execute(UnityRPGEntity entity, TurnState state);
    }
}

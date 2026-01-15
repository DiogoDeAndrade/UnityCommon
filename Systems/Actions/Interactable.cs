using NaughtyAttributes;
using UnityEngine;

namespace UC.Interaction
{
    public class Interactable : ActionRunner
    {
        [SerializeField] 
        protected InteractionVerb   interactionVerb;
        [SerializeField]
        protected GameObject        _referenceObject;
        [SerializeField]
        protected int               _priority = 0;
        [SerializeField] 
        protected bool              overrideCursor;
        [SerializeField, ShowIf(nameof(overrideCursor))] 
        protected CursorDef         cursorDef;
        [SerializeReference]
        protected Condition[]       conditions;
        [SerializeReference]
        protected GameAction[]      actions;
        [SerializeField]
        protected float             cooldown = 2.0f;
        [SerializeField]
        protected bool              canRetrigger = true;

        float   lastInteractionTime = float.NegativeInfinity;
        bool    isRunning = false;

        public CursorDef cursor
        {
            get
            {
                if (overrideCursor)
                {
                    return cursorDef;
                }
                return interactionVerb.cursorDef;
            }
        }
        public InteractionVerb verb => interactionVerb;
        public GameObject referenceObject => _referenceObject ? _referenceObject : gameObject;

        public int priority => _priority;

        public virtual bool CanInteract(ActionContext context)
        {
            if (isRunning) return false;
            if ((!canRetrigger) && (lastInteractionTime >= 0)) return false;
            if ((cooldown > 0.0f) && ((Time.time - lastInteractionTime) < cooldown))
            {
                return false;
            }
            if (conditions != null)
            {
                context.targetGameObject = referenceObject;

                foreach (var condition in conditions)
                {
                    if (!condition.Evaluate(context)) return false;
                }
            }

            return true;
        }

        public bool Interact(ActionContext context, ActionRunner runnerObject)
        {
            context.runner = runnerObject ? runnerObject : this;

            GameAction.RunActions(actions, context,
                                  (context) =>
                                  {
                                      isRunning = true;
                                      return true;
                                  },
                                  (context) =>
                                  {
                                      isRunning = false;
                                      lastInteractionTime = Time.time;
                                      return true;
                                  });

            return true;
        }
    }
}

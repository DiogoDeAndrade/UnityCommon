
using System;
using System.Collections;
using UnityEngine;

namespace UC.Interaction
{
    [System.Serializable]
    public abstract class GameAction : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        protected bool initialized = false;
        [SerializeField] 
        protected bool wait = false;

        public bool shouldWait => wait;

        public virtual void OnBeforeSerialize() { }

        public virtual void OnAfterDeserialize()
        {
            if (!initialized)
            {
                initialized = true;
                SetDefaultValues();
            }
        }

        protected virtual void SetDefaultValues() { }
        public virtual bool NeedWait() { return true; }

        public abstract IEnumerator Execute(ActionContext context);

        static public bool RunActions(GameAction[] actions, ActionContext context, Predicate<ActionContext> startFunction = null, Predicate<ActionContext> endFunction = null)
        {
            context.runner?.StartCoroutine(RunActionsCR(actions, context, startFunction, endFunction));

            return context.runner != null;
        }

        static IEnumerator RunActionsCR(GameAction[] actions, ActionContext context, Predicate<ActionContext> startFunction, Predicate<ActionContext> endFunction)
        {
            bool ok = startFunction?.Invoke(context) ?? true;
            if (ok)
            {
                foreach (var a in actions)
                {
                    if (a == null)
                        continue;

                    // Run the action
                    IEnumerator routine = a.Execute(context);

                    if ((a.shouldWait) && (routine != null))
                    {
                        // Wait for the coroutine to finish
                        yield return routine;
                    }
                    else if (routine != null)
                    {
                        // Run asynchronously, but don't wait
                        context.runner.StartCoroutine(routine);
                    }
                }

                ok = endFunction?.Invoke(context) ?? true;
            }
        }
    }
}
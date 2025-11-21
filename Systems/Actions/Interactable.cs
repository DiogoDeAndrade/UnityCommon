using NaughtyAttributes;
using System.Collections;
using UnityEngine;

namespace UC.Interaction
{
    public class Interactable : MonoBehaviour
    {
        [SerializeField] 
        protected InteractionVerb   interactionVerb;
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
        public int priority => _priority;

        public virtual bool CanInteract(GameObject referenceObject)
        {
            if (isRunning) return false;
            if ((!canRetrigger) && (lastInteractionTime >= 0)) return false;
            if ((cooldown > 0.0f) && ((Time.time - lastInteractionTime) < cooldown))
            {
                return false;
            }
            if (conditions != null)
            {
                foreach (var condition in conditions)
                {
                    if (!condition.Evaluate(referenceObject)) return false;
                }
            }

            return true;
        }

        public bool Interact(GameObject referenceObject, MonoBehaviour interactionHandler)
        {
            MonoBehaviour runner = interactionHandler ? interactionHandler : this;
            runner.StartCoroutine(RunActionsCR(interactionHandler));

            return true;
        }

        IEnumerator RunActionsCR(MonoBehaviour runner)
        {
            isRunning = true;

            foreach (var a in actions)
            {
                if (a == null)
                    continue;

                // Run the action
                IEnumerator routine = a.Execute(gameObject);

                if ((a.shouldWait) && (routine != null))
                {
                    // Wait for the coroutine to finish
                    yield return routine;
                }
                else if (routine != null)
                {
                    // Run asynchronously, but don't wait
                    runner.StartCoroutine(routine);
                }
            }

            isRunning = false;
            lastInteractionTime = Time.time;
        }
    }
}

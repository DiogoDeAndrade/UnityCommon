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

        public bool Interact(GameObject actionSource, GameObject actionTarget, MonoBehaviour runnerObject)
        {
            MonoBehaviour runner = runnerObject ? runnerObject : this;
            
            runner.StartCoroutine(RunActionsCR(actionSource.GetComponent<GameActionObject>(), actionTarget.GetComponent<GameActionObject>(), runner));

            return true;
        }

        IEnumerator RunActionsCR(IGameActionObject actionSource, IGameActionObject actionTarget, MonoBehaviour runner)
        {
            isRunning = true;

            foreach (var a in actions)
            {
                if (a == null)
                    continue;

                // Run the action
                IEnumerator routine = a.Execute(actionSource, actionTarget);

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

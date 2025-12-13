using UnityEngine;

namespace UC.Interaction
{
    [System.Serializable]
    public abstract class Condition
    {
        [SerializeField] private bool negate;

        public bool Evaluate(ActionContext context)
        {
            bool b = EvaluateThis(context);
            if (negate) b = !b;
            return b;
        }
        protected abstract bool EvaluateThis(ActionContext context);

        protected bool _alreadyTriggered = false;

        public void SetTriggered()
        {
            _alreadyTriggered = true;
        }
    }
}

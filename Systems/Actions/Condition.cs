using UnityEngine;

namespace UC.Interaction
{
    [System.Serializable]
    public abstract class Condition
    {
        [SerializeField] private bool negate;

        public bool Evaluate(GameObject referenceObject)
        {
            bool b = EvaluateThis(referenceObject);
            if (negate) b = !b;
            return b;
        }
        protected abstract bool EvaluateThis(GameObject referenceObject);

        protected bool _alreadyTriggered = false;

        public void SetTriggered()
        {
            _alreadyTriggered = true;
        }
    }
}


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

        public abstract IEnumerator Execute(GameObject source, GameObject target);
    }
}
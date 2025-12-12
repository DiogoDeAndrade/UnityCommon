using System;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Event/On Resource Change")]
    public class RPGEventOnResourceChange : RPGEvent
    {
        public enum ChangeType { OnChange, OnEmpty, OnRecover };

        [SerializeField]
        private ResourceType    resourceType;
        [SerializeField]
        private ChangeType      changeType;
        [SerializeReference]
        protected Condition[]   conditions;
        [SerializeReference]
        protected GameAction[]  actions;

        public override void Init(RPGEntity entity)
        {
            var resInstance = entity.Get(resourceType);
            if (resInstance != null)
            {
                switch (changeType)
                {
                    case ChangeType.OnChange:
                        resInstance.onChange += ResInstance_onChange;
                        break;
                    case ChangeType.OnEmpty:
                        resInstance.onResourceEmpty += ResInstance_onResourceEmpty;
                        break;
                    case ChangeType.OnRecover:
                        resInstance.onResourceNotEmpty += ResInstance_onResourceNotEmpty;
                        break;
                    default:
                        break;
                }
            }
        }

        private void ResInstance_onResourceNotEmpty(ResourceInstance resource, GameObject healSource)
        {
            Debug.Log($"Resource {resource} not empty!");
        }

        private void ResInstance_onResourceEmpty(ResourceInstance resource, GameObject changeSource)
        {
            Debug.Log($"Resource {resource} empty!");
        }

        private void ResInstance_onChange(ResourceInstance resource, ChangeData data)
        {
            Debug.Log($"Resource {resource} changed!");
        }
    }
}

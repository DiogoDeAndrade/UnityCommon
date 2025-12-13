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
            base.Init(entity);

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
            RunActions(resource, healSource, null);
        }

        private void ResInstance_onResourceEmpty(ResourceInstance resource, GameObject changeSource)
        {
            RunActions(resource, changeSource, null);
        }

        private void ResInstance_onChange(ResourceInstance resource, ChangeData data)
        {
            RunActions(resource, data.source, data);
        }

        private void RunActions(ResourceInstance resource, GameObject changeSource, ChangeData changeData)
        {
            var context = new ActionContext
            {
                triggerGameObject = (resource.owner as ResourceHandler)?.gameObject,
                triggerEntity = resource.owner as RPGEntity,
                targetGameObject = (resource.owner as ResourceHandler)?.gameObject,
                targetEntity = resource.owner as RPGEntity,
                changeSource = changeSource,
                changeData = changeData,
                runner = AreaManager.instance
            };
            if (conditions != null)
            {
                foreach (var condition in conditions)
                {
                    if (condition.Evaluate(context))
                    {
                        return;
                    }
                }
            }

            GameAction.RunActions(actions, context);
        }
    }
}

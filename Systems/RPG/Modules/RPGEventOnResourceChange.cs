using System;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Event/On Resource Change")]
    public class RPGEventOnResourceChange : RPGEvent
    {
        [SerializeField]
        private ResourceType    resourceType;
        [SerializeReference]
        protected GameAction[]  actions;

        public override void Init(RPGEntity entity)
        {
            var resInstance = entity.Get(resourceType);
            if (resInstance != null)
            {
                resInstance.onChange += ResInstance_onChange;
            }
        }

        private void ResInstance_onChange(ResourceInstance resource, ChangeData data)
        {
            Debug.Log($"Resource {resource} changed!");
        }
    }
}

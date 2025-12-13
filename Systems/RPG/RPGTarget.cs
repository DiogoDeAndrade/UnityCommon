using System;
using UC.Interaction;
using UnityEngine;

namespace UC.RPG
{
    [Serializable]
    public class RPGTarget
    {
        public enum Type { UnityReference = 1, TriggerEntity = 2, TargetEntity = 3};

        [SerializeField]
        private Type            type = Type.UnityReference;
        [SerializeField]
        private UnityRPGEntity  unityEntity;

        public static RPGTarget TriggerEntity = new RPGTarget
        {
            type = Type.TriggerEntity,
            unityEntity = null
        };

        public RPGEntity GetEntity(ActionContext context)
        {
            switch (type)
            {
                case Type.UnityReference:
                    return unityEntity?.rpgEntity;
                case Type.TriggerEntity:
                    if (context == null) return null;
                    if (context.triggerEntity != null) return context.triggerEntity;
                    return context.triggerGameObject?.GetComponent<UnityRPGEntity>()?.rpgEntity;
                case Type.TargetEntity:
                    if (context == null) return null;
                    if (context.targetEntity != null) return context.targetEntity;
                    return context.targetGameObject?.GetComponent<UnityRPGEntity>()?.rpgEntity;
                default:
                    break;
            }

            throw new System.NotImplementedException();
        }

        public GameObject GetGameObject(ActionContext context)
        {
            switch (type)
            {
                case Type.UnityReference:
                    return unityEntity?.gameObject;
                case Type.TriggerEntity:
                    if (context == null) return null;
                    return context.triggerGameObject;
                case Type.TargetEntity:
                    if (context == null) return null;
                    return context.targetGameObject;
                default:
                    break;
            }

            throw new System.NotImplementedException();
        }

        public T GetComponent<T>(ActionContext context) where T : Component
        {
            switch (type)
            {
                case Type.UnityReference:
                    return unityEntity?.GetComponent<T>();
                case Type.TriggerEntity:
                    return context?.triggerGameObject?.GetComponent<T>();
                case Type.TargetEntity:
                    if (context == null) return null;
                    return context?.targetGameObject?.GetComponent<T>();
                default:
                    break;
            }

            throw new System.NotImplementedException();
        }
    }
}

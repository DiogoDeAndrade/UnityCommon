using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{

    public class ResourceHandler : MonoBehaviour
    {
        public enum ChangeType { Burst, OverTime };
        public enum OverrideMode
        {
            None = 0,
            InitialResource = 1
        }

        public delegate void OnChange(ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource);
        public event OnChange onChange;
        public delegate void OnResourceEmpty(GameObject changeSource);
        public event OnResourceEmpty onResourceEmpty;
        public delegate void OnResourceNotEmpty(GameObject healSource);
        public event OnResourceNotEmpty onResourceNotEmpty;

        [Expandable]
        public ResourceType type;
        [SerializeField]
        private OverrideMode overrideMode;
        [SerializeField]
        private float       initialValue;

        protected ResourceInstance resourceInstance;

        protected bool _fromInstance;

        bool isOverrideInitialResource => (overrideMode & OverrideMode.InitialResource) != 0;

        public float resource => instance.value;
        public float maxValue => instance.maxValue;
        public float normalizedResource => instance.value / instance.maxValue;
        public bool fromInstance => _fromInstance;

        public ResourceInstance instance
        {
            get
            {
                if (resourceInstance == null)
                {
                    resourceInstance = new(type);
                    resourceInstance.onChange += ResourceInstance_onChange;
                    resourceInstance.onResourceEmpty += ResourceInstance_onResourceEmpty;
                    resourceInstance.onResourceNotEmpty += ResourceInstance_onResourceNotEmpty;
                    _fromInstance = false;
                }
                return resourceInstance;
            }
            set
            {
                type = value.type;
                resourceInstance = value;
                _fromInstance = true;
            }
        }

        public bool isResourceEmpty => instance.isResourceEmpty;
        public bool isResourceNotEmpty => instance.isResourceNotEmpty;

        void Start()
        {
            if (!_fromInstance) ResetResource();
        }

        private void ResourceInstance_onResourceNotEmpty(GameObject healSource)
        {
            onResourceNotEmpty?.Invoke(healSource);
        }

        private void ResourceInstance_onResourceEmpty(GameObject changeSource)
        {
            onResourceEmpty?.Invoke(changeSource);
        }

        private void ResourceInstance_onChange(ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource)
        {
            onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);
        }

        void RenderCombatText(float prevValue)
        {
            float actualDelta = resourceInstance.value - prevValue;
            if (actualDelta != 0.0f)
            {
                if (type.useCombatText)
                {
                    var str = type.ctBaseText;
                    var c = type.ctPositiveColor;
                    if (actualDelta > 0)
                    {
                        str = str.Replace("{value}", $"+{actualDelta}");
                    }
                    else
                    {
                        str = str.Replace("{value}", $"{actualDelta}");
                        c = type.ctNegativeColor;
                    }

                    CombatTextManager.SpawnText(gameObject, str, c, c.ChangeAlpha(0.0f));
                }
            }
        }

        public bool Change(ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource, bool canAddOnEmpty = true)
        {
            float prevValue = instance.value;
            bool ret = instance.Change(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource, canAddOnEmpty);

            if (ret) RenderCombatText(prevValue);

            return ret;
        }

        public static List<ResourceHandler> FindAllByType(ResourceType type)
        {
            var allObjects = FindObjectsByType<ResourceHandler>(FindObjectsSortMode.None);
            var ret = new List<ResourceHandler>();
            foreach (var obj in allObjects)
            {
                if (obj.type == type) ret.Add(obj);
            }

            return ret;
        }

        public static List<ResourceHandler> FindAllInRadius(ResourceType type, Vector3 pos, float range)
        {
            List<ResourceHandler> ret = new();
            var resHandlers = FindObjectsByType<ResourceHandler>(FindObjectsSortMode.None);
            foreach (var h in resHandlers)
            {
                if ((h.type == type) && (Vector3.Distance(h.transform.position, pos) < range))
                {
                    ret.Add(h);
                }
            }

            return ret;
        }

        public void SetResource(float r)
        {
            resourceInstance.SetResource(r);
        }

        public void ResetResource(bool combatText = false)
        {
            float prevValue = resourceInstance.value;

            if (isOverrideInitialResource)
                resourceInstance.value = initialValue;
            else
                resourceInstance.value = type.defaultValue;

            if (combatText) RenderCombatText(prevValue);
        }
    }

    public static class ResourceHandlerExtensions
    {
        public static ResourceHandler FindResourceHandler(this Component component, ResourceType type)
        {
            var handlers = component.GetComponents<ResourceHandler>();
            foreach (var handler in handlers)
            {
                if (handler.type == type) return handler;
            }

            return null;
        }
    }
}
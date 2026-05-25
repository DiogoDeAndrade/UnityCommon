using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC.RPG
{

    public class ResourceHandler : MonoBehaviour, IRPGOwner
    {
        [Flags]
        public enum OverrideMode
        {
            None = 0,
            InitialValue = 1,
            MaxValue = 2
        }

        public delegate bool CanChange(ResourceInstance resource, ChangeData data);
        public event CanChange          canChange;
        public delegate void OnChange(ResourceInstance resourceInstance, ChangeData changeData);
        public event OnChange           onChange;
        public delegate void OnResourceEmpty(ResourceInstance resourceInstance, GameObject changeSource);
        public event OnResourceEmpty    onResourceEmpty;
        public delegate void OnResourceNotEmpty(ResourceInstance resourceInstance, GameObject healSource);
        public event OnResourceNotEmpty onResourceNotEmpty;

        [Expandable]
        public ResourceType type;
        [SerializeField]
        private OverrideMode overrideMode;
        [SerializeField]
        private float       initialValue;
        [SerializeField]
        private float       overrideMaxValue;

        protected ResourceInstance resourceInstance;

        protected bool _fromInstance;

        bool isOverrideInitialValue => (overrideMode & OverrideMode.InitialValue) != 0;
        bool isOverrideMaxValue => (overrideMode & OverrideMode.MaxValue) != 0;

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
                    resourceInstance = new(type, this);
                    resourceInstance.onChange += ResourceInstance_onChange;
                    resourceInstance.onResourceEmpty += ResourceInstance_onResourceEmpty;
                    resourceInstance.onResourceNotEmpty += ResourceInstance_onResourceNotEmpty;
                    resourceInstance.canChange += ResourceInstance_canChange;
                    _fromInstance = false;
                }
                return resourceInstance;
            }
            set
            {
                if (resourceInstance != null)
                {
                    resourceInstance.onChange -= ResourceInstance_onChange;
                    resourceInstance.onResourceEmpty -= ResourceInstance_onResourceEmpty;
                    resourceInstance.onResourceNotEmpty -= ResourceInstance_onResourceNotEmpty;
                    resourceInstance.canChange -= ResourceInstance_canChange;
                }   

                type = value.type;
                resourceInstance = value;
                resourceInstance.onChange += ResourceInstance_onChange;
                resourceInstance.onResourceEmpty += ResourceInstance_onResourceEmpty;
                resourceInstance.onResourceNotEmpty += ResourceInstance_onResourceNotEmpty;
                resourceInstance.canChange += ResourceInstance_canChange;
                _fromInstance = true;
            }
        }

        public bool isResourceEmpty => instance.isResourceEmpty;
        public bool isResourceNotEmpty => instance.isResourceNotEmpty;

        void Start()
        {
            if (!_fromInstance) ResetResource();
        }

        private void ResourceInstance_onResourceNotEmpty(ResourceInstance resource, GameObject healSource)
        {
            onResourceNotEmpty?.Invoke(resource, healSource);
        }

        private void ResourceInstance_onResourceEmpty(ResourceInstance resource, GameObject changeSource)
        {
            onResourceEmpty?.Invoke(resource, changeSource);
        }

        private void ResourceInstance_onChange(ResourceInstance resource, ChangeData changeData)
        {
            onChange?.Invoke(resource, changeData);
        }

        private bool ResourceInstance_canChange(ResourceInstance resource, ChangeData changeData)
        {
            if (canChange != null)
            {
                foreach (CanChange filter in canChange.GetInvocationList())
                {
                    if (!filter(resource, changeData))
                        return false;
                }
            }
            return true;
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

                    CombatTextManager.SpawnText(gameObject, str, new CombatTextDef(c));
                }
            }
        }

        public bool Change(ChangeData changeData, bool canAddOnEmpty = true)
        {
            float prevValue = instance.value;
            bool ret = instance.Change(changeData, canAddOnEmpty);

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
            instance.SetResource(r);
        }

        public void ResetResource(bool combatText = false)
        {
            float prevValue = instance.value;

            if (isOverrideMaxValue)
                resourceInstance.maxValue = overrideMaxValue;

            if (isOverrideInitialValue)
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
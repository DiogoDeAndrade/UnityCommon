using NaughtyAttributes;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class ResourceHandler : MonoBehaviour
    {
        public enum ChangeType { Burst, OverTime };

        public delegate void OnChange(ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource);
        public event OnChange onChange;
        public delegate void OnResourceEmpty(GameObject changeSource);
        public event OnResourceEmpty onResourceEmpty;
        public delegate void OnResourceNotEmpty(GameObject healSource);
        public event OnResourceNotEmpty onResourceNotEmpty;

        [Expandable]
        public ResourceType type;

        protected float _resource = 100.0f;
        protected bool _resourceEmpty;

        public float resource
        {
            get { return _resource; }
        }

        public float normalizedResource
        {
            get { return _resource / type.maxValue; }
        }

        public bool isResourceEmpty => _resourceEmpty;
        public bool isResourceNotEmpty => !_resourceEmpty;

        void Awake()
        {
        }

        void Start()
        {
            ResetResource();
            _resourceEmpty = false;
        }

        void RenderCombatText(float prevValue)
        {
            float actualDelta = _resource - prevValue;
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
            float prevValue = _resource;
            bool ret = true;

            if (deltaValue < 0)
            {
                if (_resourceEmpty) ret = false;
                else
                {
                    _resource = Mathf.Clamp(_resource + deltaValue, 0.0f, type.maxValue);
                    if (_resource <= 0.0f)
                    {
                        _resource = 0.0f;
                        _resourceEmpty = true;

                        onResourceEmpty?.Invoke(changeSource);
                    }
                    else
                    {
                        onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);
                    }
                }
            }
            else if (deltaValue > 0)
            {
                if (canAddOnEmpty)
                {
                    if (_resource < type.maxValue)
                    {
                        _resource = Mathf.Clamp(_resource + deltaValue, 0.0f, type.maxValue);

                        onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);

                        if ((_resource > 0.0f) && (_resourceEmpty))
                        {
                            onResourceNotEmpty?.Invoke(changeSource);
                            _resourceEmpty = false;
                        }
                    }
                }
                else if (_resourceEmpty) ret = false;
                else
                {
                    if (_resource < type.maxValue)
                    {
                        _resource = Mathf.Clamp(_resource + deltaValue, 0.0f, type.maxValue);

                        onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);
                    }
                    else ret = false;
                }
            }

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
            _resource = r;
            _resourceEmpty = (_resource <= 0.0f);
        }

        public void ResetResource(bool combatText = false)
        {
            float prevValue = _resource;

            _resource = type.defaultValue;
            _resourceEmpty = false;

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
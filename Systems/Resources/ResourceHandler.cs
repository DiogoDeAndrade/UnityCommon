using System.Collections.Generic;
using UnityEngine;

public class ResourceHandler : MonoBehaviour
{
    public enum ChangeType { Burst, OverTime };

    public delegate void OnChange(ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource);
    public event OnChange  onChange;
    public delegate void OnResourceEmpty(GameObject changeSource);
    public event OnResourceEmpty onResourceEmpty;
    public delegate void OnResourceNotEmpty(GameObject healSource);
    public event OnResourceNotEmpty onResourceNotEmpty;

    public ResourceType type;

    protected float     _resource = 100.0f;
    protected bool      _resourceEmpty;

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

    public bool Change(ChangeType changeType, float deltaValue, Vector3 changeSrcPosition, Vector3 changeSrcDirection, GameObject changeSource, bool canAddOnEmpty = true)
    {
        if (deltaValue < 0)
        {
            if (_resourceEmpty) return false;

            _resource += deltaValue;
            if (_resource <= 0.0f)
            {
                _resource = 0.0f;
                _resourceEmpty = true;

                onResourceEmpty?.Invoke(changeSource);
            }
            else
            {
                onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);

                if (type.useCombatText)
                {
                    var str = type.ctBaseText;
                    str = str.Replace("{value}", $"{deltaValue}");
                    CombatTextManager.SpawnText(gameObject, str, type.ctNegativeColor, type.ctNegativeColor.ChangeAlpha(0.0f));
                }
            }

            return true;
        }
        else if (deltaValue > 0)
        {
            if (canAddOnEmpty)
            {
                if (_resource < type.maxValue)
                {
                    onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);

                    if (type.useCombatText)
                    {
                        var str = type.ctBaseText;
                        str = str.Replace("{value}", $"+{deltaValue}");
                        CombatTextManager.SpawnText(gameObject, str, type.ctPositiveColor, type.ctPositiveColor.ChangeAlpha(0.0f));
                    }

                    _resource += deltaValue;
                    if ((_resource > 0.0f) && (_resourceEmpty))
                    {
                        onResourceNotEmpty?.Invoke(changeSource);
                        _resourceEmpty = false;
                    }
                    return true;
                }
            }
            else if (_resourceEmpty) return false;
            if (_resource < type.maxValue)
            {
                onChange?.Invoke(changeType, deltaValue, changeSrcPosition, changeSrcDirection, changeSource);

                if (type.useCombatText)
                {
                    var str = type.ctBaseText;
                    str = str.Replace("{value}", $"+{deltaValue}");
                    CombatTextManager.SpawnText(gameObject, str, type.ctPositiveColor, type.ctPositiveColor.ChangeAlpha(0.0f));
                }

                _resource += deltaValue;
                return true;
            }
            return false;
        }

        return true;
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

    public void ResetResource()
    {
        _resource = type.defaultValue;
        _resourceEmpty = false;
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

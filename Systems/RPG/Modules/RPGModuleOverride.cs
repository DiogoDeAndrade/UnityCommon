using System;
using System.Collections.Generic;
using UC;
using UnityEngine;

public class RPGModuleOverride : MonoBehaviour
{
    [SerializeReference]
    private List<SOModule> _modules = new();

    public T GetModule<T>() where T : SOModule
    {
        // Local search first
        for (int i = 0; i < _modules.Count; i++)
        {
            if ((_modules[i] is T tModule) && (_modules[i].enabled))
            {
                return tModule;
            }
        }

        return null;
    }

    public List<T> GetModules<T>(List<T> result = null) where T : SOModule
    {
        result ??= new List<T>();

        for (int i = 0; i < _modules.Count; i++)
        {
            if ((_modules[i] is T tModule) && (_modules[i].enabled))
            {
                result.Add(tModule);
            }
        }

        return result;
    }

    public T AddModule<T>() where T : SOModule
    {
        return (T)AddModule(typeof(T));
    }

    public SOModule AddModule(Type moduleType)
    {
        if ((moduleType == null) || (!typeof(SOModule).IsAssignableFrom(moduleType)))
        {
            Debug.LogError($"Invalid module type: {moduleType}");
            return null;
        }

        var module = Activator.CreateInstance(moduleType) as SOModule;
        if (module == null)
        {
            Debug.LogError($"Could not create SOModule of type {moduleType}");
            return null;
        }

        module.SetOwner(this);

        _modules ??= new List<SOModule>();
        _modules.Add(module);

#if UNITY_EDITOR
        var path = UnityEditor.AssetDatabase.GetAssetPath(this);
        if (!string.IsNullOrEmpty(path))
        {
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        return module;
    }

    public void RemoveModule(SOModule module)
    {
        if ((module == null) || (_modules == null))
            return;

        if (_modules.Remove(module))
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Remove Module");
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    public bool HasModule(Type moduleType)
    {
        if ((moduleType == null) || (!typeof(SOModule).IsAssignableFrom(moduleType)))
        {
            return false;
        }

        // Re-use existing API and only consider enabled modules
        var allModules = GetModules<SOModule>(null);
        for (int i = 0; i < allModules.Count; i++)
        {
            if (moduleType.IsInstanceOfType(allModules[i]))
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void OnValidate()
    {
        if (_modules == null) return;

        for (int i = _modules.Count - 1; i >= 0; i--)
        {
            if (_modules[i] == null)
            {
                _modules.RemoveAt(i);
            }
            else
            {
                _modules[i].SetOwner(this);
            }
        }
    }
}

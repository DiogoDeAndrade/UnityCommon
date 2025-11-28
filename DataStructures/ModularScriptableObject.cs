using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC 
{
    [Serializable]
    public abstract class SOModule
    {
        [SerializeField, HideInInspector]
        protected bool _enabled = true; 
        [SerializeField, HideInInspector]
        protected bool _open = true; 
        [SerializeField, HideInInspector]
        protected ModularScriptableObject _scriptableObject;

        public ModularScriptableObject scriptableObject => _scriptableObject;
        public bool enabled => _enabled;

        internal void SetOwner(ModularScriptableObject owner)
        {
            _scriptableObject = owner;
        }

        public virtual string GetModuleHeaderString() => string.Empty;
    }

    [CreateAssetMenu(fileName = "ModularScriptableObject", menuName = "Unity Common/Modular SO")]
    public class ModularScriptableObject : ScriptableObject
    {
        [SerializeField]
        private List<ModularScriptableObject>   _parents = new();

        [SerializeReference]
        private List<SOModule>                  _modules = new();

        public IReadOnlyList<ModularScriptableObject>   parents => _parents;
        public IReadOnlyList<SOModule>                  modules => _modules;

        public T GetModule<T>(bool includeParents = false) where T : SOModule
        {
            // Local search first
            for (int i = 0; i < _modules.Count; i++)
            {
                if ((_modules[i] is T tModule) && (_modules[i].enabled))
                {
                    return tModule;
                }
            }

            if (!includeParents)
            {
                return null;
            }

            // Recursive parent search with cycle protection
            HashSet<ModularScriptableObject> visited = new();
            visited.Add(this);
            return GetModuleRecursive<T>(visited);
        }

        private T GetModuleRecursive<T>(HashSet<ModularScriptableObject> visited) where T : SOModule
        {
            foreach (var parent in _parents)
            {
                if ((parent == null) || (!visited.Add(parent)))
                    continue;

                // check parent modules
                foreach (var m in parent._modules)
                {
                    if ((m is T tModule) && (m.enabled))
                    {
                        return tModule;
                    }
                }

                // recurse
                var result = parent.GetModuleRecursive<T>(visited);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        public List<T> GetModules<T>(bool includeParents = false, List<T> result = null) where T : SOModule
        {
            result ??= new List<T>();

            for (int i = 0; i < _modules.Count; i++)
            {
                if ((_modules[i] is T tModule) && (_modules[i].enabled))
                {
                    result.Add(tModule);
                }
            }

            if (!includeParents)
            {
                return result;
            }

            HashSet<ModularScriptableObject> visited = new();
            visited.Add(this);
            GetModulesRecursive(includeParents, visited, result);
            return result;
        }

        private void GetModulesRecursive<T>(bool includeParents, HashSet<ModularScriptableObject> visited, List<T> result) where T : SOModule
        {
            foreach (var parent in _parents)
            {
                if ((parent == null) || (!visited.Add(parent)))
                    continue;

                foreach (var m in parent._modules)
                {
                    if ((m is T tModule) && (m.enabled))
                    {
                        result.Add(tModule);
                    }
                }

                parent.GetModulesRecursive(includeParents, visited, result);
            }
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
}

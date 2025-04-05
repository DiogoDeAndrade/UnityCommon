using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UC
{

    public class DefaultExpressionContextEvaluator : MonoBehaviour, Expression.IContext
    {
        [SerializeField] private List<Hypertag> tags;
        [SerializeField] private List<Item> items;
        [SerializeField] private ParamPrefabList<GameObject> prefabs;

        protected Dictionary<string, Hypertag> cachedTags;
        protected Dictionary<string, Item> cachedItems;
        protected Dictionary<string, ParamPrefab<GameObject>> cachedPrefabs;
        protected Dictionary<string, object> variables = new();

        public bool GetVarBool(string varName)
        {
            if (variables.TryGetValue(varName, out object value))
            {
                if (value is bool boolValue) return boolValue;
            }
            return false;
        }

        public float GetVarNumber(string varName)
        {
            if (variables.TryGetValue(varName, out object value))
            {
                if (value is float floatValue) return floatValue;
            }
            return 0.0f;
        }

        public string GetVarString(string varName)
        {
            if (variables.TryGetValue(varName, out object value))
            {
                if (value is string stringValue) return stringValue;
            }
            return "";
        }

        public Expression.DataType GetVariableDataType(string varName)
        {
            if (variables.TryGetValue(varName, out object value))
            {
                if (value is float) return Expression.DataType.Number;
                if (value is bool) return Expression.DataType.Bool;
                if (value is string) return Expression.DataType.String;
            }
            return Expression.DataType.Undefined;
        }

        public void SetVariable(string varName, float value)
        {
            variables[varName] = value;
        }

        public void SetVariable(string varName, bool value)
        {
            variables[varName] = value;
        }

        public void SetVariable(string varName, string value)
        {
            variables[varName] = value;
        }

        public bool Spawn(string prefabName, string locationTagName = null, string parentObjectTagName = null)
        {
            var prefab = GetPrefabByName(prefabName);
            if (prefab == null)
            {
                return false;
            }
            GameObject newObject = prefab.Instantiate();
            if (!string.IsNullOrEmpty(parentObjectTagName))
            {
                var targetTag = GetTagByName(parentObjectTagName);
                if (targetTag == null)
                {
                    return false;
                }
                var target = Hypertag.FindFirstObjectWithHypertag<Transform>(targetTag);
                if (target == null)
                {
                    Debug.LogError($"Can't find parent object tagged with {parentObjectTagName}");
                    return false;
                }
                newObject.transform.SetParent(target);
            }
            if (!string.IsNullOrEmpty(locationTagName))
            {
                var targetTag = GetTagByName(locationTagName);
                if (targetTag == null)
                {
                    return false;
                }
                var target = Hypertag.FindFirstObjectWithHypertag<Transform>(targetTag);
                if (target == null)
                {
                    Debug.LogError($"Can't find target tagged with {locationTagName}");
                    return false;
                }
                newObject.transform.position = target.position;
                newObject.transform.rotation = target.rotation;
            }

            return (newObject != null);
        }

        public bool AddItemToInventory(string targetTagName, string itemName, int quantity = 1)
        {
            Item item = GetItemByName(itemName);
            if (item == null)
            {
                return false;
            }

            var targetTag = GetTagByName(targetTagName);
            if (targetTag == null)
            {
                return false;
            }
            var inventory = Hypertag.FindFirstObjectWithHypertag<Inventory>(targetTag);
            if (inventory == null)
            {
                Debug.LogError($"Can't find inventory tagged with {targetTagName}");
                return false;
            }
            return inventory.Add(item, quantity) == quantity;
        }
        public bool RemoveItemFromInventory(string targetTagName, string itemName, int quantity = 1)
        {
            Item item = GetItemByName(itemName);
            if (item == null)
            {
                return false;
            }

            var targetTag = GetTagByName(targetTagName);
            if (targetTag == null)
            {
                return false;
            }
            var inventory = Hypertag.FindFirstObjectWithHypertag<Inventory>(targetTag);
            if (inventory == null)
            {
                Debug.LogError($"Can't find inventory tagged with {targetTagName}");
                return false;
            }
            return inventory.Remove(item, quantity) == quantity;
        }


        public bool HasItemInInventory(string targetTagName, string itemName, int quantity = 1)
        {
            Item item = GetItemByName(itemName);
            if (item == null)
            {
                return false;
            }

            var targetTag = GetTagByName(targetTagName);
            if (targetTag == null)
            {
                return false;
            }
            var inventory = Hypertag.FindFirstObjectWithHypertag<Inventory>(targetTag);
            if (inventory == null)
            {
                Debug.LogError($"Can't find inventory tagged with {targetTagName}");
                return false;
            }
            return inventory.GetItemCount(item) >= quantity;
        }

        public bool Destroy(string targetTagName)
        {
            var targetTag = GetTagByName(targetTagName);
            if (targetTag == null)
            {
                return false;
            }
            var obj = Hypertag.FindFirstObjectWithHypertag<Transform>(targetTag);
            if (obj == null)
            {
                Debug.LogError($"Can't find object tagged with {targetTagName}");
                return false;
            }

            Destroy(obj.gameObject);

            return true;
        }

        public void Close()
        {
            DialogueManager.Instance.EndDialogue();
        }

        public T EvaluateFunction<T>(string functionName, List<Expression> args)
        {
            var type = GetType();
            var methodInfo = type.GetPrivateMethod(functionName);

            if (methodInfo == null)
            {
                throw new Expression.ErrorException($"Method \"{functionName}\" not found in context!");
            }

            // Check parameters, check parameter types
            List<object> funcArgs = new();
            ParameterInfo[] parameters = methodInfo.GetParameters();

            if (parameters.Length != args.Count)
            {
                throw new Expression.ErrorException($"Invalid number of argument for \"{functionName}\": expected {parameters.Length}, received {args.Count}!");
            }
            else
            {
                for (int index = 0; index < parameters.Length; index++)
                {
                    ParameterInfo param = parameters[index];

                    System.Type paramType = param.ParameterType;
                    var expression = args[index];

                    if (paramType == typeof(bool))
                    {
                        var pType = expression.GetDataType(this);
                        if ((pType == Expression.DataType.Bool) || (pType == Expression.DataType.Undefined))
                        {
                            funcArgs.Add(Convert.ChangeType(expression.EvaluateBool(this), paramType));
                        }
                        else
                        {
                            Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\", received {pType}!");
                        }
                    }
                    else if ((paramType == typeof(float)) ||
                             (paramType == typeof(int)))
                    {
                        var pType = expression.GetDataType(this);
                        if ((pType == Expression.DataType.Number) || (pType == Expression.DataType.Undefined))
                        {
                            funcArgs.Add(Convert.ChangeType(expression.EvaluateNumber(this), paramType));
                        }
                        else
                        {
                            Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\", received {pType}!");
                        }
                    }
                    else if (paramType == typeof(string))
                    {
                        var pType = expression.GetDataType(this);
                        if ((pType == Expression.DataType.String) || (pType == Expression.DataType.Undefined))
                        {
                            funcArgs.Add(Convert.ChangeType(expression.EvaluateString(this), paramType));
                        }
                        else
                        {
                            Debug.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\", received {pType}!");
                        }
                    }
                    else
                    {
                        Debug.LogError($"Unsupported type {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\"!");
                    }
                }
                if (funcArgs.Count == parameters.Length)
                {
                    return (T)Convert.ChangeType(methodInfo.Invoke(this, funcArgs.ToArray()), typeof(T));
                }
                else
                {
                    throw new Expression.ErrorException($"Failed to call method {functionName}!");
                }
            }
        }

        public Expression.DataType GetFunctionType(string functionName)
        {
            var type = GetType();
            var methodInfo = type.GetMethod(functionName,
                                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (methodInfo == null)
            {
                throw new Expression.ErrorException($"Function {functionName} not found!");
            }

            if (methodInfo.ReturnType == typeof(bool)) return Expression.DataType.Bool;
            if (methodInfo.ReturnType == typeof(float)) return Expression.DataType.Number;
            if (methodInfo.ReturnType == typeof(string)) return Expression.DataType.String;
            if (methodInfo.ReturnType == typeof(void)) return Expression.DataType.None;

            throw new Expression.ErrorException($"Unsupported return type {methodInfo.ReturnType} for function {functionName}!");
        }

        protected Hypertag GetTagByName(string name)
        {
            if (cachedTags == null)
            {
                cachedTags = new();
                foreach (var t in tags) cachedTags.Add(t.name, t);
            }

            if (cachedTags.TryGetValue(name, out var tag))
                return tag;

            Debug.LogError($"Can't find tag {name}!");
            return null;
        }
        protected ParamPrefab<GameObject> GetPrefabByName(string name)
        {
            if (cachedPrefabs == null)
            {
                cachedPrefabs = new();
                foreach (var p in prefabs) cachedPrefabs.Add(p.name, p.prefab);
            }

            if (cachedPrefabs.TryGetValue(name, out var prefab))
            {
                return prefab;
            }
            if (cachedPrefabs.TryGetValue(name + " Variant", out prefab))
            {
                return prefab;
            }

            Debug.LogError($"Can't find prefab {name}!");
            return null;
        }
        protected Item GetItemByName(string name)
        {
            if (cachedItems == null)
            {
                cachedItems = new();
                foreach (var i in items) cachedItems.Add(i.name, i);
            }

            if (cachedItems.TryGetValue(name, out var item))
                return item;

            Debug.LogError($"Can't find item {name}!");
            return null;
        }


#if UNITY_EDITOR
        protected void AddAllTags()
        {
            tags = new List<Hypertag>(AssetUtils.GetAll<Hypertag>());
        }

        protected void AddAllItems()
        {
            items = new List<Item>(AssetUtils.GetAll<Item>());
        }
#endif
    }
}

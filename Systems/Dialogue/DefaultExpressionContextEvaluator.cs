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

        const string localVarPrefix = "local.";

        // "local.<name>" doesn't live here at all - it lives in the DialogueState of the current
        // conversation (the global one when none was passed), so a dialogue started with its own
        // state gets its own copies. Returns the state to use and the name with the prefix stripped,
        // or null when the name isn't local.
        static DialogueState GetLocalVarState(ref string varName)
        {
            if (!varName.StartsWith(localVarPrefix)) return null;

            var state = DialogueManager.ActiveOrGlobalState;
            if (state != null) varName = varName.Substring(localVarPrefix.Length);

            return state;
        }

        public bool GetVarBool(string varName)
        {
            var localState = GetLocalVarState(ref varName);
            if (localState != null) return localState.GetVarBool(varName);

            if (variables.TryGetValue(varName, out object value))
            {
                if (value is bool boolValue) return boolValue;
            }
            return false;
        }

        public float GetVarNumber(string varName)
        {
            var localState = GetLocalVarState(ref varName);
            if (localState != null) return localState.GetVarNumber(varName);

            if (variables.TryGetValue(varName, out object value))
            {
                if (value is float floatValue) return floatValue;
            }
            return 0.0f;
        }

        public string GetVarString(string varName)
        {
            var localState = GetLocalVarState(ref varName);
            if (localState != null) return localState.GetVarString(varName);

            if (variables.TryGetValue(varName, out object value))
            {
                if (value is string stringValue) return stringValue;
            }
            return "";
        }

        public Expression.DataType GetVariableDataType(string varName)
        {
            var localState = GetLocalVarState(ref varName);
            if (localState != null) return localState.GetVariableDataType(varName);

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
            var localState = GetLocalVarState(ref varName);
            if (localState != null) { localState.SetVariable(varName, value); return; }

            variables[varName] = value;
        }

        public void SetVariable(string varName, bool value)
        {
            var localState = GetLocalVarState(ref varName);
            if (localState != null) { localState.SetVariable(varName, value); return; }

            variables[varName] = value;
        }

        public void SetVariable(string varName, string value)
        {
            var localState = GetLocalVarState(ref varName);
            if (localState != null) { localState.SetVariable(varName, value); return; }

            variables[varName] = value;
        }

        // -----------------------------------------------------------------------------------------
        // Dialogue state queries, usable from any dialogue expression or code block. They read the
        // conversation's DialogueState *and* the global one (a node records in one of the two,
        // depending on its {Global} tag, so the sum is simply "its count").
        // -----------------------------------------------------------------------------------------

        // How many times the dialogue node has been entered
        protected float Visits(string dialogueKey)
        {
            return DialogueManager.GetStateCount(dialogueKey);
        }

        protected bool HasSeen(string dialogueKey)
        {
            return Visits(dialogueKey) > 0;
        }

        // Whether the node's entry code ("{ ... }") has executed at least once
        protected bool HasRun(string dialogueKey)
        {
            return DialogueManager.GetStateCount(dialogueKey + "#code") > 0;
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
                    DebugHelpers.LogError($"Can't find parent object tagged with {parentObjectTagName}");
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
                    DebugHelpers.LogError($"Can't find target tagged with {locationTagName}");
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
                DebugHelpers.LogError($"Can't find inventory tagged with {targetTagName}");
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
                DebugHelpers.LogError($"Can't find inventory tagged with {targetTagName}");
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
                DebugHelpers.LogError($"Can't find inventory tagged with {targetTagName}");
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
                DebugHelpers.LogError($"Can't find object tagged with {targetTagName}");
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
            var methodInfo = FindFunction(functionName, args.Count);

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
                            DebugHelpers.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\", received {pType}!");
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
                            DebugHelpers.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\", received {pType}!");
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
                            DebugHelpers.LogError($"Expected {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\", received {pType}!");
                        }
                    }
                    else
                    {
                        DebugHelpers.LogError($"Unsupported type {paramType} for argument #{index} ({param.Name}) for call to \"{functionName}\"!");
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

        // Resolves a function name to the single method a call with this many arguments means.
        //
        // Reflection's GetMethod throws AmbiguousMatchException the moment a name is overloaded, and
        // the exception doesn't say which name it choked on. Doing the picking here keeps the name
        // (and the candidate signatures) in the message, and lets overloads work at all: an overload
        // taking types no expression can produce - a ResourceType, an Item - is not callable from a
        // dialogue and never competes with the string/number one an author actually wrote.
        MethodInfo FindFunction(string functionName, int argCount)
        {
            var candidates = GetCallableFunctions(functionName);
            if (candidates.Count == 0)
            {
                var declared = GetType().GetPrivateMethods(functionName);
                if (declared.Count > 0)
                {
                    throw new Expression.ErrorException($"Method \"{functionName}\" can't be called from an expression - {TypeExtensions.DescribeMethods(declared)} takes types an expression can't produce!");
                }
                throw new Expression.ErrorException($"Method \"{functionName}\" not found in context!");
            }
            if (candidates.Count == 1) return candidates[0];

            MethodInfo match = null;
            foreach (var candidate in candidates)
            {
                if (candidate.GetParameters().Length != argCount) continue;
                if (match != null)
                {
                    throw new Expression.ErrorException($"Call to \"{functionName}\" with {argCount} argument(s) is ambiguous - {TypeExtensions.DescribeMethods(candidates)} can't be told apart by argument count, so one of them has to be renamed!");
                }
                match = candidate;
            }

            if (match == null)
            {
                throw new Expression.ErrorException($"No overload of \"{functionName}\" takes {argCount} argument(s): {TypeExtensions.DescribeMethods(candidates)}!");
            }

            return match;
        }

        // The overloads of <functionName> an expression could actually call: every parameter has to
        // be one of the types EvaluateFunction knows how to marshal
        List<MethodInfo> GetCallableFunctions(string functionName)
        {
            var candidates = GetType().GetPrivateMethods(functionName);

            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                foreach (var parameter in candidates[i].GetParameters())
                {
                    var parameterType = parameter.ParameterType;
                    if ((parameterType == typeof(bool)) || (parameterType == typeof(float)) ||
                        (parameterType == typeof(int)) || (parameterType == typeof(string))) continue;

                    candidates.RemoveAt(i);
                    break;
                }
            }

            return candidates;
        }

        public Expression.DataType GetFunctionType(string functionName)
        {
            // The expression only wants to know the type this call produces, so it doesn't matter
            // which overload ends up being called - as long as they agree on what they return
            var candidates = GetCallableFunctions(functionName);
            if (candidates.Count == 0)
            {
                throw new Expression.ErrorException($"Function {functionName} not found!");
            }

            var methodInfo = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (candidates[i].ReturnType != methodInfo.ReturnType)
                {
                    throw new Expression.ErrorException($"Overloads of {functionName} don't all return the same type: {TypeExtensions.DescribeMethods(candidates)}!");
                }
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

            DebugHelpers.LogError($"Can't find tag {name}!");
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

            DebugHelpers.LogError($"Can't find prefab {name}!");
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

            DebugHelpers.LogError($"Can't find item {name}!");
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

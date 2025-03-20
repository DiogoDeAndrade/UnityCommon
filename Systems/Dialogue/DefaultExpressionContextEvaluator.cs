using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public class DefaultExpressionContextEvaluator : MonoBehaviour, UCExpression.IContext
{
    [SerializeField] private List<Hypertag> tags;

    protected Dictionary<string, Hypertag> cachedTags;
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

    public UCExpression.DataType GetVariableDataType(string varName)
    {
        if (variables.TryGetValue(varName, out object value))
        {
            if (value is float) return UCExpression.DataType.Number;
            if (value is bool) return UCExpression.DataType.Bool;
            if (value is string) return UCExpression.DataType.String;
        }
        return UCExpression.DataType.Undefined;
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

    public void Close()
    {
        DialogueManager.Instance.EndDialogue();
    }

    public T EvaluateFunction<T>(string functionName, List<UCExpression> args)
    {
        var type = GetType();
        var methodInfo = type.GetMethod((string)functionName,
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (methodInfo == null)
        {
            throw new UCExpression.ErrorException($"Method \"{functionName}\" not found in context!");
        }

        // Check parameters, check parameter types
        List<object> funcArgs = new();
        ParameterInfo[] parameters = methodInfo.GetParameters();

        if (parameters.Length != args.Count)
        {
            throw new UCExpression.ErrorException($"Invalid number of argument for \"{functionName}\": expected {parameters.Length}, received {args.Count}!");
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
                    if ((pType == UCExpression.DataType.Bool) || (pType == UCExpression.DataType.Undefined))
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
                    if ((pType == UCExpression.DataType.Number) || (pType == UCExpression.DataType.Undefined))
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
                    if ((pType == UCExpression.DataType.String) || (pType == UCExpression.DataType.Undefined))
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
                throw new UCExpression.ErrorException($"Failed to call method {functionName}!");
            }
        }
    }

    public UCExpression.DataType GetFunctionType(string functionName)
    {
        var type = GetType();
        var methodInfo = type.GetMethod(functionName,
                                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (methodInfo == null)
        {
            throw new UCExpression.ErrorException($"Function {functionName} not found!");
        }

        if (methodInfo.ReturnType == typeof(bool)) return UCExpression.DataType.Bool;
        if (methodInfo.ReturnType == typeof(float)) return UCExpression.DataType.Number;
        if (methodInfo.ReturnType == typeof(string)) return UCExpression.DataType.String;
        if (methodInfo.ReturnType == typeof(void)) return UCExpression.DataType.None;

        throw new UCExpression.ErrorException($"Unsupported return type {methodInfo.ReturnType} for function {functionName}!");
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

#if UNITY_EDITOR
    private void AddAllTags()
    {
        string[] guids = AssetDatabase.FindAssets("t:Hypertag"); // Searches all Speaker assets
        var allTags = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Hypertag>)
            .Where(tag => tag != null) // Ensure it's a valid Speaker instance
            .ToArray();

        tags = new List<Hypertag>(allTags);
    }
#endif
}

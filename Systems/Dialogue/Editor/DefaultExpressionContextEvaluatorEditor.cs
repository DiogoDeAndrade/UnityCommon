using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

namespace UC
{

    [CustomEditor(typeof(DefaultExpressionContextEvaluator), true)]
    public class DefaultExpressionContextEvaluatorDrawer : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // Draw the default inspector

            DefaultExpressionContextEvaluator evaluator = (DefaultExpressionContextEvaluator)target;

            // Use reflection to access private/protected fields if needed
            FieldInfo variablesField = evaluator.GetType().GetPrivateField("variables");
            if (variablesField == null) return;

            Dictionary<string, object> variables = variablesField.GetValue(evaluator) as Dictionary<string, object>;
            if (variables == null) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variables", EditorStyles.boldLabel);

            if (variables.Count == 0)
            {
                EditorGUILayout.LabelField("No variables set.");
            }
            else
            {
                var keys = new List<string>(variables.Keys);

                foreach (var key in keys)
                {
                    object value = variables[key];

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(key, GUILayout.Width(100));

                    if (value is bool boolValue)
                    {
                        bool newBool = EditorGUILayout.Toggle(boolValue);
                        if (newBool != boolValue) variables[key] = newBool;
                    }
                    else if (value is float floatValue)
                    {
                        float newFloat = EditorGUILayout.FloatField(floatValue);
                        if (newFloat != floatValue) variables[key] = newFloat;
                    }
                    else if (value is int intValue)
                    {
                        int newInt = EditorGUILayout.IntField(intValue);
                        if (newInt != intValue) variables[key] = newInt;
                    }
                    else if (value is string stringValue)
                    {
                        string newString = EditorGUILayout.TextField(stringValue);
                        if (newString != stringValue) variables[key] = newString;
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Unsupported Type: {value.GetType()}");
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

            if (GUILayout.Button("Add All Tags"))
            {
                MethodInfo addAllTagsMethod = evaluator.GetType().GetPrivateMethod("AddAllTags");
                if (addAllTagsMethod != null)
                {
                    addAllTagsMethod.Invoke(evaluator, null);
                }
                else
                {
                    Debug.LogError("Method AddAllTags() not found on this object.");
                }
            }
            if (GUILayout.Button("Add All Items"))
            {
                MethodInfo addAllItemsMethod = evaluator.GetType().GetPrivateMethod("AddAllItems");
                if (addAllItemsMethod != null)
                {
                    addAllItemsMethod.Invoke(evaluator, null);
                }
                else
                {
                    Debug.LogError("Method AddAllItems() not found on this object.");
                }
            }
        }
    }
}
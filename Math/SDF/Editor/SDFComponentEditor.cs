using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{

    /// Custom editor for the SDFComponent.  
    [CustomEditor(typeof(SDFComponent))]
    public class SDFComponentEditor : UnityEditor.Editor
    {
        private UnityEditor.Editor _sdfEditor;

        SerializedProperty sdf;
        SerializedProperty debugDisplay;
        SerializedProperty debugDisplayOnlyWhenSelected;
        SerializedProperty displayColor;

        private void OnEnable()
        {
            sdf = serializedObject.FindProperty("sdf");
            debugDisplay = serializedObject.FindProperty("debugDisplay");
            debugDisplayOnlyWhenSelected = serializedObject.FindProperty("debugDisplayOnlyWhenSelected");
            displayColor = serializedObject.FindProperty("displayColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // If no SDF assigned, show buttons to create one
            if (sdf.objectReferenceValue == null)
            {
                EditorGUILayout.LabelField("Add SDF Shape:");
                var sdfTypes = Assembly.GetAssembly(typeof(SDF))
                                       .GetTypes()
                                       .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(SDF)));

                foreach (var t in sdfTypes)
                {
                    var displayName = GetDisplayName(t);
                    if (GUILayout.Button(displayName))
                    {
                        var instance = ScriptableObject.CreateInstance(t) as SDF;
                        instance.name = displayName;
                        instance.ownerGameObject = (serializedObject.targetObject as SDFComponent).gameObject;
                        Undo.RegisterCreatedObjectUndo(instance, "Create " + t.Name);
                        instance.hideFlags = HideFlags.HideInHierarchy;
                        sdf.objectReferenceValue = instance;
                        Undo.RecordObject(target, "Assign SDF");
                        break;
                    }
                }
            }
            else
            {
                // Draw the object-picker + foldout arrow
                EditorGUILayout.LabelField("Display", EditorStyles.boldLabel);
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(debugDisplay);
                    if (debugDisplay.boolValue)
                    {
                        EditorGUILayout.PropertyField(debugDisplayOnlyWhenSelected);
                        EditorGUILayout.PropertyField(displayColor);
                    }
                }
                serializedObject.ApplyModifiedProperties();

                // If it’s a ScriptableObject, render its inspector inline
                if (sdf.objectReferenceValue is ScriptableObject scriptable)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        GUILayout.Label($"SDF ({scriptable.name}):", EditorStyles.boldLabel);
                        if (_sdfEditor == null)
                            _sdfEditor = CreateEditor(scriptable);

                        _sdfEditor.OnInspectorGUI();
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private string GetDisplayName(Type t)
        {
            var name = t.Name;
            if (name.StartsWith("SDF_"))
                name = name.Substring(4);
            else if (name.StartsWith("SDF"))
                name = name.Substring(3);
            name = name.Replace("_", " ");
            // Insert spaces before uppercase letters preceded by lowercase or digit
            name = Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", " $1");
            return name;
        }
    }

    public static class SDFComponentGizmoDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        public static void DrawSDFGizmo(SDFComponent component, GizmoType gizmoType)
        {
            var sdfObj = component.sdf;
            if (sdfObj == null) return;

            if (component.debugDisplay)
            {
                if (component.debugDisplayOnlyWhenSelected)
                {
                    bool isSelected = (gizmoType & GizmoType.Selected) != 0;
                    if (!isSelected) return;
                }

                Gizmos.color = component.displayColor;
                sdfObj.DrawGizmos();
            }
        }
    }
}

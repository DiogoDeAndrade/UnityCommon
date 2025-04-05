using System;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Collections;

namespace UC
{

    [CustomPropertyDrawer(typeof(ParamPrefabBase), true)]
    public class ParamPrefabDrawer : PropertyDrawer
    {
        public static bool SuppressPrefabField = false;
        public static bool SuppressOptions = false;

        private UnityEngine.Object previousPrefab;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float y = position.y;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2f;

            SerializedProperty prefabProp = property.FindPropertyRelative("prefabObject");
            if (!SuppressPrefabField)
            {
                Rect objectFieldRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.ObjectField(objectFieldRect, prefabProp, label);
                y += lineHeight + spacing;
            }

            UnityEngine.Object currentPrefab = prefabProp.objectReferenceValue;
            if (currentPrefab != previousPrefab)
            {
                property.serializedObject.ApplyModifiedProperties();

                previousPrefab = currentPrefab;

                SerializedProperty prefabObjectProp = property.FindPropertyRelative("prefabObject");
                if (prefabObjectProp == null) return;

                // force Unity to apply before invoking
                property.serializedObject.ApplyModifiedProperties();

                // Find the target object from the serialized path
                object targetObject = GetTargetObject(property);
                var updateMethod = targetObject?.GetType().GetMethod("UpdateParameters", BindingFlags.NonPublic | BindingFlags.Instance);
                updateMethod?.Invoke(targetObject, null);

                property.serializedObject.Update();
                property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            if (currentPrefab is GameObject go && !EditorUtility.IsPersistent(go))
            {
                Rect warningRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.HelpBox(warningRect, "This is a scene object, not a prefab!", MessageType.Warning);
                y += lineHeight + spacing;
            }
            else if ((currentPrefab is Component component) && (component != null) && (!EditorUtility.IsPersistent(component.gameObject)))
            {
                Rect warningRect = new Rect(position.x, y, position.width, lineHeight);
                EditorGUI.HelpBox(warningRect, "This is a scene object, not a prefab!", MessageType.Warning);
                y += lineHeight + spacing;
            }

            if ((currentPrefab != null) && (!SuppressOptions))
            {
                // Draw parameter overrides
                SerializedProperty paramList = property.FindPropertyRelative("parameters");
                if (paramList != null && paramList.isArray)
                {
                    for (int i = 0; i < paramList.arraySize; i++)
                    {
                        SerializedProperty param = paramList.GetArrayElementAtIndex(i);
                        SerializedProperty nameProp = param.FindPropertyRelative("name");
                        SerializedProperty overrideProp = param.FindPropertyRelative("overrideValue");
                        SerializedProperty valueProp = param.FindPropertyRelative("value");

                        float checkboxWidth = 22f;
                        float identWidth = 20.0f;
                        float labelWidth = EditorStyles.label.CalcSize(new GUIContent(nameProp.stringValue)).x + 8f;
                        float fieldWidth = position.width - identWidth - checkboxWidth - labelWidth;
                        float basePosX = position.x + identWidth;

                        Rect checkboxRect = new Rect(basePosX, y, checkboxWidth, lineHeight);
                        Rect labelRect = new Rect(basePosX + checkboxWidth, y, labelWidth, lineHeight);
                        Rect valueRect = new Rect(basePosX + checkboxWidth + labelWidth, y, fieldWidth, lineHeight);

                        EditorGUI.BeginChangeCheck();
                        overrideProp.boolValue = EditorGUI.Toggle(checkboxRect, overrideProp.boolValue);
                        EditorGUI.LabelField(labelRect, nameProp.stringValue);
                        if (overrideProp.boolValue)
                        {
                            EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none, true);
                        }
                        y += EditorGUI.GetPropertyHeight(valueProp, GUIContent.none, true) + spacing;
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight + 2f;
            if (SuppressPrefabField) height = 0f;

            SerializedProperty prefabProp = property.FindPropertyRelative("prefabObject");
            if (prefabProp.objectReferenceValue != null && !EditorUtility.IsPersistent(prefabProp.objectReferenceValue))
            {
                height += EditorGUIUtility.singleLineHeight + 2f;
            }

            if ((prefabProp.objectReferenceValue != null) && (!SuppressOptions))
            {
                SerializedProperty paramList = property.FindPropertyRelative("parameters");
                if (paramList != null && paramList.isArray)
                {
                    for (int i = 0; i < paramList.arraySize; i++)
                    {
                        SerializedProperty param = paramList.GetArrayElementAtIndex(i);
                        SerializedProperty overrideProp = param.FindPropertyRelative("overrideValue");
                        SerializedProperty valueProp = param.FindPropertyRelative("value");

                        height += EditorGUIUtility.singleLineHeight + 2f;
                    }
                }
            }

            return height;
        }

        public static object GetParentObjectOfProperty(SerializedProperty prop)
        {
            string[] path = prop.propertyPath.Replace(".Array.data[", "[").Split('.');
            object obj = prop.serializedObject.targetObject;

            foreach (string element in path[..^1])
            {
                if (element.Contains("["))
                {
                    string elementName = element[..element.IndexOf('[')];
                    int index = Convert.ToInt32(element[(element.IndexOf('[') + 1)..^1]);
                    obj = GetValue(obj, elementName, index);
                }
                else
                {
                    obj = GetValue(obj, element);
                }
            }

            return obj;
        }

        private static object GetValue(object source, string name)
        {
            if (source == null)
                return null;

            var type = source.GetType();
            while (type != null)
            {
                var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(source);

                type = type.BaseType;
            }

            return null;
        }

        private static object GetValue(object source, string name, int index)
        {
            var enumerable = GetValue(source, name) as IEnumerable;
            var enumerator = enumerable?.GetEnumerator();
            for (int i = 0; i <= index; i++)
            {
                if (!enumerator.MoveNext())
                    return null;
            }

            return enumerator.Current;
        }

        /// Gets the actual object instance represented by the SerializedProperty
        public static object GetTargetObject(SerializedProperty prop)
        {
            if (prop == null) return null;

            string path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;

            foreach (var part in path.Split('.'))
            {
                if (part.Contains("["))
                {
                    var fieldName = part.Substring(0, part.IndexOf("["));
                    var index = int.Parse(part.Substring(part.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue(obj, fieldName, index);
                }
                else
                {
                    obj = GetValue(obj, part);
                }
            }

            return obj;
        }
    }

    [CustomPropertyDrawer(typeof(ParamValue))]
    public class ParamValueDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var typeNameProp = property.FindPropertyRelative("typeName");
            if (typeNameProp == null)
            {
                EditorGUI.LabelField(position, "<Missing type info>");
                return;
            }

            Type actualType = GetTypeFromName(typeNameProp.stringValue);
            if (actualType == null)
            {
                EditorGUI.LabelField(position, "<Unknown Type>");
                return;
            }

            // Field lookup
            SerializedProperty targetProp = null;
            if (actualType == typeof(int)) targetProp = property.FindPropertyRelative("intValue");
            else if (actualType == typeof(float)) targetProp = property.FindPropertyRelative("floatValue");
            else if (actualType == typeof(bool)) targetProp = property.FindPropertyRelative("boolValue");
            else if (actualType == typeof(string)) targetProp = property.FindPropertyRelative("stringValue");
            else if (actualType == typeof(Color)) targetProp = property.FindPropertyRelative("colorValue");
            else if (actualType == typeof(Vector2)) targetProp = property.FindPropertyRelative("vector2Value");
            else if (actualType == typeof(Vector3)) targetProp = property.FindPropertyRelative("vector3Value");
            else if (actualType == typeof(Vector4)) targetProp = property.FindPropertyRelative("vector4Value");
            else if (actualType == typeof(Quaternion)) targetProp = property.FindPropertyRelative("quaternionValue");
            else if (typeof(UnityEngine.Object).IsAssignableFrom(actualType))
            {
                var objProp = property.FindPropertyRelative("objectValue");
                objProp.objectReferenceValue = EditorGUI.ObjectField(position, GUIContent.none, objProp.objectReferenceValue, actualType, false);
                return;
            }
            else
            {
                EditorGUI.LabelField(position, $"<Unsupported Type: {actualType.Name}>");
                return;
            }

            // Draw target field
            if (targetProp != null)
            {
                EditorGUI.PropertyField(position, targetProp, GUIContent.none, true);
            }
            else
            {
                EditorGUI.LabelField(position, $"<Unhandled Field>");
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var typeNameProp = property.FindPropertyRelative("typeName");
            Type actualType = GetTypeFromName(typeNameProp?.stringValue);

            if (actualType == null)
                return EditorGUIUtility.singleLineHeight;

            if (actualType == typeof(int)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("intValue"), label, true);
            if (actualType == typeof(float)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("floatValue"), label, true);
            if (actualType == typeof(bool)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("boolValue"), label, true);
            if (actualType == typeof(string)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("stringValue"), label, true);
            if (actualType == typeof(Color)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("colorValue"), label, true);
            if (actualType == typeof(Vector2)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("vector2Value"), label, true);
            if (actualType == typeof(Vector3)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("vector3Value"), label, true);
            if (actualType == typeof(Vector4)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("vector4Value"), label, true);
            if (actualType == typeof(Quaternion)) return EditorGUI.GetPropertyHeight(property.FindPropertyRelative("quaternionValue"), label, true);
            if (typeof(UnityEngine.Object).IsAssignableFrom(actualType)) return EditorGUIUtility.singleLineHeight;

            return EditorGUIUtility.singleLineHeight;
        }

        private static Type GetTypeFromName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;
            return Type.GetType(typeName);
        }
    }
}
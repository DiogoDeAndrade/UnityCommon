using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(ResourceValueFunction), true)]
    public class ResourceValueFunctionDrawer : PropertyDrawer
    {
        private static bool     _initialized;
        private static Type[]   _derivedTypes;
        private static string[] _typeNames;
        private static string   _baseTypeName;

        private static void Init()
        {
            if (_initialized) return;

            var baseType = typeof(ResourceValueFunction);
            _baseTypeName = baseType.Name;

            _derivedTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a =>
                {
                    // Skip dynamic / reflection-only assemblies to avoid issues
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => baseType.IsAssignableFrom(t)
                            && !t.IsAbstract
                            && !t.IsInterface)
                .OrderBy(t => t.Name)
                .ToArray();

            _typeNames = new string[_derivedTypes.Length + 1];
            _typeNames[0] = "<None>";

            for (int i = 0; i < _derivedTypes.Length; i++)
            {
                var type = _derivedTypes[i];
                string niceName = BuildNiceTypeName(type);
                _typeNames[i + 1] = niceName;
            }

            _initialized = true;
        }

        private static string BuildNiceTypeName(Type type)
        {
            string name = type.Name;

            // Strip the common prefix, e.g. "ResourceValueFunction"
            if (!string.IsNullOrEmpty(_baseTypeName) && name.StartsWith(_baseTypeName))
            {
                name = name.Substring(_baseTypeName.Length);
            }

            // Fallback if we stripped everything or something weird happened
            if (string.IsNullOrEmpty(name))
                name = type.Name;

            // Use Unity's built-in nicifier (splits on camel case, etc.)
            return ObjectNames.NicifyVariableName(name);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            Init();

            float height = EditorGUIUtility.singleLineHeight; // popup line

            if (property.managedReferenceValue != null)
            {
                var iterator = property.Copy();
                var endProperty = iterator.GetEndProperty();
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren)
                       && !SerializedProperty.EqualContents(iterator, endProperty))
                {
                    height += EditorGUI.GetPropertyHeight(iterator, true)
                              + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false; // only go into children once
                }
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Init();
            EditorGUI.BeginProperty(position, label, property);

            // Draw label, then indent inner content
            position = EditorGUI.IndentedRect(position);

            // Popup rect (first line)
            Rect popupRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight
            );

            // Figure out which type is currently selected
            int currentIndex = GetCurrentTypeIndex(property);

            // Draw popup
            int newIndex = EditorGUI.Popup(popupRect, label.text, currentIndex, _typeNames);

            if (newIndex != currentIndex)
            {
                if (newIndex == 0)
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    var newType = _derivedTypes[newIndex - 1];
                    property.managedReferenceValue = Activator.CreateInstance(newType);
                }

                // Make sure the change is saved
                property.serializedObject.ApplyModifiedProperties();
            }

            // Draw the fields of the selected subclass (if any)
            if (property.managedReferenceValue != null)
            {
                Rect bodyRect = position;
                bodyRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                EditorGUI.indentLevel++;
                DrawChildren(bodyRect, property);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static int GetCurrentTypeIndex(SerializedProperty property)
        {
            if (property.managedReferenceValue == null)
                return 0;

            var instanceType = property.managedReferenceValue.GetType();

            for (int i = 0; i < _derivedTypes.Length; i++)
            {
                if (_derivedTypes[i] == instanceType)
                    return i + 1; // +1 because 0 is <None>
            }

            // Type not found (maybe renamed/moved) – treat as None
            return 0;
        }

        private static void DrawChildren(Rect position, SerializedProperty property)
        {
            var iterator = property.Copy();
            var endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            float y = position.y;

            while (iterator.NextVisible(enterChildren)
                   && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                float h = EditorGUI.GetPropertyHeight(iterator, true);
                Rect r = new Rect(position.x, y, position.width, h);

                EditorGUI.PropertyField(r, iterator, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;

                enterChildren = false;
            }
        }
    }
}

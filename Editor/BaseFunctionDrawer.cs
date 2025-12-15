using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    public class BaseFunctionDrawer<T> : PropertyDrawer
    {
        private static Type[]   _types;
        private static string[] _displayNames;

        static BaseFunctionDrawer()
        {
            // Find all non-abstract T types in the project
            var list = new List<Type>();

            foreach (var t in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (!t.IsAbstract && !t.IsGenericType && t.IsClass)
                    list.Add(t);
            }

            _types = list
                .OrderBy(t => MakeNiceName(t), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _displayNames = new string[_types.Length + 1];
            _displayNames[0] = "(None)";

            for (int i = 0; i < _types.Length; i++)
            {
                _displayNames[i + 1] = MakeNiceName(_types[i]);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight; // popup line

            if (property.managedReferenceValue != null)
            {
                height += EditorGUIUtility.standardVerticalSpacing;

                // Sum heights of all children of the managed reference
                var iterator = property.Copy();
                var end = iterator.GetEndProperty();
                int targetDepth = property.depth + 1;
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
                {
                    enterChildren = false;

                    if (iterator.depth != targetDepth)
                        continue;

                    height += EditorGUI.GetPropertyHeight(iterator, true) +
                              EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false;
                }
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = new GUIContent(property.displayName, label.image, label.tooltip);

            EditorGUI.BeginProperty(position, label, property);

            // First line: label + popup
            Rect lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            Rect labelRect = new Rect(
                lineRect.x,
                lineRect.y,
                EditorGUIUtility.labelWidth,
                lineRect.height
            );

            Rect popupRect = new Rect(
                lineRect.x + EditorGUIUtility.labelWidth,
                lineRect.y,
                lineRect.width - EditorGUIUtility.labelWidth,
                lineRect.height
            );

            EditorGUI.LabelField(labelRect, new GUIContent(property.displayName));

            int currentIndex = GetCurrentTypeIndex(property);
            int newIndex = EditorGUI.Popup(popupRect, currentIndex + 1, _displayNames) - 1;

            HandleContextMenu(popupRect, (newIndex >= 0) ? _types[newIndex] : null);

            if (newIndex != currentIndex)
            {
                if (newIndex >= 0 && newIndex < _types.Length)
                {
                    property.managedReferenceValue = Activator.CreateInstance(_types[newIndex]);
                }
                else
                {
                    property.managedReferenceValue = null;
                }
            }

            // Draw fields of the selected T below
            if (property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;

                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                var iterator = property.Copy();
                var end = iterator.GetEndProperty();
                int targetDepth = property.depth + 1;
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
                {
                    enterChildren = false;

                    // only direct children of the managed ref object
                    if (iterator.depth != targetDepth)
                        continue;

                    float h = EditorGUI.GetPropertyHeight(iterator, true);
                    Rect r = new Rect(position.x, y, position.width, h);
                    EditorGUI.PropertyField(r, iterator, true);
                    y += h + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private void HandleContextMenu(Rect popupRect, Type selectedType)
        {
            var e = Event.current;
            if (e.type != EventType.ContextClick)
                return;

            if (!popupRect.Contains(e.mousePosition))
                return;

            var menu = new GenericMenu();

            if (selectedType != null)
            {
                menu.AddItem(new GUIContent("Edit Script"), false, () =>
                {
                    var script = GUIUtils.FindScriptForType(selectedType);
                    if (script != null)
                        AssetDatabase.OpenAsset(script);
                        EditorGUIUtility.PingObject(script);
                });

                menu.AddItem(new GUIContent("Ping Script"), false, () =>
                {
                    var script = GUIUtils.FindScriptForType(selectedType);
                    if (script != null)
                        EditorGUIUtility.PingObject(script);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Edit Script"));
                menu.AddDisabledItem(new GUIContent("Ping Script"));
            }

            menu.AddSeparator("");

            menu.ShowAsContext();
            e.Use();
        }

        // --- Helpers -----------------------------------------------------------

        private static int GetCurrentTypeIndex(SerializedProperty property)
        {
            if (property.managedReferenceValue == null || _types == null)
                return -1;

            var type = property.managedReferenceValue.GetType();
            for (int i = 0; i < _types.Length; i++)
            {
                if (_types[i] == type)
                    return i;
            }

            return -1;
        }

        static string GetCleanTypeName(Type t)
        {
            var name = t.Name;
            int tick = name.IndexOf('`');
            return tick >= 0 ? name[..tick] : name;
        }

        static string GetPolymorphicLeafName(Type t, bool useFullPath = false)
        {
            // direct, non-inherited (your attribute is Inherited=false anyway)
            var attr = t.GetCustomAttribute<PolymorphicNameAttribute>(inherit: false);
            if ((attr == null) || (string.IsNullOrWhiteSpace(attr.Path)))
                return null;

            if (useFullPath)
                return attr.Path;

            // leaf after last '/'
            var path = attr.Path.Trim();
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path[(slash + 1)..] : path;
        }

        private static string MakeNiceName(Type t, bool usePolymorphicFullPath = true)
        {
            // 1) PolymorphicNameAttribute override
            var poly = GetPolymorphicLeafName(t, usePolymorphicFullPath);
            if (!string.IsNullOrEmpty(poly))
            {
                return poly;
            }

            // 2) Fallback: your existing type-name nicening
            string name = t.Name;

            string suffix = GetCleanTypeName(typeof(T));
            if (name.StartsWith(suffix, StringComparison.Ordinal))
            {
                name = name.Substring(suffix.Length);
            }
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffix.Length);
            }

            if (string.IsNullOrEmpty(name))
                name = t.Name;

            var result = new StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if ((i > 0) && (char.IsUpper(c)) && (!char.IsWhiteSpace(name[i - 1])))
                {
                    result.Append(' ');
                }
                result.Append(c);
            }

            return result.ToString();
        }
    }
}

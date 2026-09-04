using NaughtyAttributes.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    public class BaseFunctionDrawer<T> : PropertyDrawer
    {
        [NoAutoStaticsCleanup]
        private static Type[]   _types;
        [NoAutoStaticsCleanup]
        private static string[] _displayNames;

        static BaseFunctionDrawer()
        {
            // Find all non-abstract T types in the project, including the type itself
            var list = new List<Type>();

            var selfType = typeof(T);
            if ((!selfType.IsAbstract) && (!selfType.IsGenericType) && (selfType.IsClass))
                list.Add(selfType);

            foreach (var t in TypeCache.GetTypesDerivedFrom<T>())
            {
                if ((!t.IsAbstract) && (!t.IsGenericType) && (t.IsClass))
                    list.Add(t);
            }

            _types = list
                .OrderBy(t => MakeFolderFirstSortKey(t), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _displayNames = new string[_types.Length + 1];
            _displayNames[0] = "(None)";

            for (int i = 0; i < _types.Length; i++)
            {
                _displayNames[i + 1] = MakeNiceName(_types[i]);
            }
        }

        private static SerializedProperty GetMinimizeProperty(SerializedProperty property)
        {
            var minimize = property.FindPropertyRelative("minimize");

            if ((minimize != null) &&
                (minimize.propertyType == SerializedPropertyType.Boolean))
            {
                return minimize;
            }

            return null;
        }

        private static bool IsMinimized(SerializedProperty property)
        {
            return GetMinimizeProperty(property)?.boolValue ?? false;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetReferenceHeaderHeight(property) + ((property.managedReferenceValue != null) ? (EditorGUIUtility.standardVerticalSpacing + GetReferenceChildrenHeight(property)) : (0f));
        }

        public static float GetReferenceHeaderHeight(SerializedProperty property)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public static float GetReferenceChildrenHeight(SerializedProperty property)
        {
            if (property.managedReferenceValue == null)
                return 0f;

            if (IsMinimized(property))
                return 0f;

            float height = 0f;

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            int targetDepth = property.depth + 1;
            bool enterChildren = true;

            while ((iterator.NextVisible(enterChildren)) && (!SerializedProperty.EqualContents(iterator, end)))
            {
                enterChildren = false;

                if (iterator.depth != targetDepth)
                    continue;
                
                if (iterator.name == "minimize")
                    continue;

                if (PropertyUtility.IsVisible(iterator))
                {
                    height += EditorGUI.GetPropertyHeight(iterator, true) +
                              EditorGUIUtility.standardVerticalSpacing;
                }
            }

            return height > 0f ? height - EditorGUIUtility.standardVerticalSpacing : 0f;
        }

        public static float GetReferenceHeight(SerializedProperty property)
        {
            float height = EditorGUIUtility.singleLineHeight;

            if (property.managedReferenceValue != null)
            {
                height += EditorGUIUtility.standardVerticalSpacing;

                var iterator = property.Copy();
                var end = iterator.GetEndProperty();
                int targetDepth = property.depth + 1;
                bool enterChildren = true;

                while (iterator.NextVisible(enterChildren) && (!SerializedProperty.EqualContents(iterator, end)))
                {
                    enterChildren = false;

                    if (iterator.depth != targetDepth)
                        continue;

                    if (PropertyUtility.IsVisible(iterator))
                    {
                        height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                    }
                }
            }

            return height;
        }

        public static bool DrawReferenceHeader(Rect position, SerializedProperty property, GUIContent label, bool inline)
        {
            label = new GUIContent(property.displayName, label.image, label.tooltip);

            EditorGUI.BeginProperty(position, label, property);

            Rect popupRect;

            if (!inline)
            {
                Rect lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

                Rect labelRect = new Rect(lineRect.x, lineRect.y, EditorGUIUtility.labelWidth, lineRect.height);

                popupRect = new Rect(lineRect.x + EditorGUIUtility.labelWidth, lineRect.y, lineRect.width - EditorGUIUtility.labelWidth, lineRect.height);

                EditorGUI.LabelField(labelRect, new GUIContent(property.displayName));
            }
            else
            {
                popupRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            }

            var minimizeProperty = GetMinimizeProperty(property);

            if (minimizeProperty != null)
            {
                const float foldoutWidth = 14f;
                const float foldoutGap = 2f;

                Rect foldoutRect = new Rect(popupRect.x + foldoutWidth * 0.5f, popupRect.y, foldoutWidth * 0.5f, popupRect.height);

                bool expanded = !minimizeProperty.boolValue;

                bool newExpanded = EditorGUI.Foldout(foldoutRect, expanded, GUIContent.none, false);

                if (newExpanded != expanded)
                    minimizeProperty.boolValue = !newExpanded;

                // Shave the foldout space directly from the popup.
                float offset = foldoutWidth + foldoutGap;

                popupRect.x += offset;
                popupRect.width -= offset;
            }

            int currentIndex = GetCurrentTypeIndex(property);

            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            int newIndex = EditorGUI.Popup(popupRect, currentIndex + 1, _displayNames) - 1;
            EditorGUI.indentLevel = indent;

            HandleContextMenu(popupRect, (newIndex >= 0) ? _types[newIndex] : null);

            bool changed = newIndex != currentIndex;


            if (changed)
            {
                if ((newIndex >= 0) && (newIndex < _types.Length))
                    property.managedReferenceValue = Activator.CreateInstance(_types[newIndex]);
                else
                    property.managedReferenceValue = null;
            }

            EditorGUI.EndProperty();

            return changed;
        }        

        public static void DrawReferenceChildren(Rect position, SerializedProperty property)
        {
            if (property.managedReferenceValue == null)
                return;

            if (IsMinimized(property))
                return;

            int oldIndent = EditorGUI.indentLevel;

            // These are children of the managed-reference object.
            EditorGUI.indentLevel = oldIndent + 1;

            float y = position.y;

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            int targetDepth = property.depth + 1;
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.depth != targetDepth)
                    continue;

                if (!PropertyUtility.IsVisible(iterator))
                    continue;

                if (iterator.name == "minimize")
                    continue;

                float h = EditorGUI.GetPropertyHeight(iterator, true);

                Rect r = new Rect(position.x, y, position.width, h);

                NaughtyEditorGUI.PropertyField(r, iterator, true);

                y += h + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel = oldIndent;
        }

        private static bool IsArrayElement(SerializedProperty property)
        {
            string path = property.propertyPath;

            return (path.EndsWith("]")) && (path.Contains(".Array.data["));
        }

        public static void DrawReference(Rect position, SerializedProperty property, GUIContent label)
        {
            float headerHeight = GetReferenceHeaderHeight(property);

            Rect headerRect = new Rect(position.x, position.y, position.width, headerHeight);

            bool inline = IsArrayElement(property);

            bool changed = DrawReferenceHeader(headerRect, property, label, inline);

            if ((!changed) && (property.managedReferenceValue != null))
            {
                float bodyHeight = GetReferenceChildrenHeight(property);

                Rect bodyRect = new Rect(position.x, position.y + headerHeight + EditorGUIUtility.standardVerticalSpacing, position.width, bodyHeight);

                DrawReferenceChildren(bodyRect, property);
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DrawReference(position, property, label);
        }

        private static void HandleContextMenu(Rect popupRect, Type selectedType)
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

        private static string MakeFolderFirstSortKey(Type t)
        {
            // Use the same string you show in the popup (full path when available)
            string name = MakeNiceName(t, usePolymorphicFullPath: true) ?? "";

            // Normalize slashes
            name = name.Replace('\\', '/');

            // Foldered entries first: those containing '/'
            bool hasFolder = name.IndexOf('/') >= 0;

            // Split into folder + leaf for nicer ordering inside groups
            string folder = "";
            string leaf = name;

            int slash = name.LastIndexOf('/');
            if (slash >= 0)
            {
                folder = name.Substring(0, slash);
                leaf = name.Substring(slash + 1);
            }

            // Sort key format:
            // 0 = foldered, 1 = non-foldered (so foldered appear first)
            // then folder, then leaf, then full string
            return $"{(hasFolder ? "0" : "1")}\u001F{folder}\u001F{leaf}\u001F{name}";
        }
    }
}

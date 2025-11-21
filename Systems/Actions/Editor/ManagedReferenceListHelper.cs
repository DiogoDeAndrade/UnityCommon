using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UC.Interaction.Editor
{

    public class ManagedReferenceListHelper
    {
        // Inside your existing Editor (OnEventEditor / InteractableEditor)
        public static ReorderableList Build(
            SerializedObject serializedObject,
            SerializedProperty arrayProp,
            System.Type baseType,
            string header,
            string addLabel,
            string emptyLabel,
            string rightHeader = null,
            float rightHeaderWidth = 0f)
        {
            var list = new ReorderableList(serializedObject, arrayProp, true, true, true, true);

            list.drawHeaderCallback = rect =>
            {
                if (!string.IsNullOrEmpty(rightHeader) && rightHeaderWidth > 0f)
                {
                    // Left header
                    var leftRect = new Rect(rect.x, rect.y, rect.width - rightHeaderWidth, rect.height);
                    // Right header (aligned with the wait checkbox)
                    var rightRect = new Rect(rect.xMax - rightHeaderWidth, rect.y, rightHeaderWidth, rect.height);

                    EditorGUI.LabelField(leftRect, header, EditorStyles.boldLabel);

                    var italic = new GUIStyle(EditorStyles.label)
                    {
                        fontSize = Mathf.FloorToInt(EditorStyles.label.fontSize * 0.75f),
                        fontStyle = FontStyle.Italic,
                        alignment = TextAnchor.MiddleCenter
                    };
                    EditorGUI.LabelField(rightRect, rightHeader, italic);
                }
                else
                {
                    EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);
                }
            };

            list.elementHeightCallback = index =>
            {
                var el = arrayProp.GetArrayElementAtIndex(index);
                return Mathf.Max(
                    EditorGUI.GetPropertyHeight(el, includeChildren: true) + 4f,
                    EditorGUIUtility.singleLineHeight + 6f
                );
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var el = arrayProp.GetArrayElementAtIndex(index);
                rect.height = EditorGUI.GetPropertyHeight(el, includeChildren: true);
                EditorGUI.PropertyField(rect, el, new GUIContent(GetManagedLabel(el, baseType)), includeChildren: true);
            };

            list.onAddDropdownCallback = (buttonRect, l) =>
            {
                ManagedReferenceAddMenu.Show(buttonRect, baseType, instance =>
                {
                    ManagedReferenceAddMenu.InsertNewManagedElement(arrayProp, arrayProp.arraySize, instance);
                });
            };

            list.onRemoveCallback = l =>
            {
                if (l.index >= 0 && l.index < arrayProp.arraySize)
                {
                    var el = arrayProp.GetArrayElementAtIndex(l.index);
                    el.managedReferenceValue = null;
                    arrayProp.DeleteArrayElementAtIndex(l.index);
                    serializedObject.ApplyModifiedProperties();
                }
            };

            list.drawNoneElementCallback = rect =>
            {
                EditorGUI.LabelField(rect, emptyLabel, EditorStyles.miniLabel);
            };

            // Right-click -> Replace...
            list.onMouseUpCallback = l =>
            {
                if (Event.current != null && Event.current.button == 1 && l.index >= 0)
                {
                    var index = l.index;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Replace..."), false, () =>
                    {
                        var last = GUILayoutUtility.GetLastRect();
                        ManagedReferenceAddMenu.Show(last, baseType, instance =>
                        {
                            var el = arrayProp.GetArrayElementAtIndex(index);
                            el.managedReferenceValue = instance;
                            serializedObject.ApplyModifiedProperties();
                        });
                    });
                    menu.ShowAsContext();
                }
            };

            return list;
        }

        private static string GetManagedLabel(SerializedProperty element, Type baseType)
        {
            if (element.managedReferenceValue == null)
                return $"({baseType.Name}) None";

            // Example: "Assembly-CSharp WSKit.Condition_HasItem"
            var full = element.managedReferenceFullTypename;

            // Extract the "TypeFullName" part (right side of the space)
            int lastSpace = full.LastIndexOf(' ');
            string typeFullName = (lastSpace >= 0 && lastSpace < full.Length - 1)
                ? full.Substring(lastSpace + 1)
                : full;

            // Normalize nested types (A+B -> A.B), then strip namespace (WSKit.Condition_X -> Condition_X)
            typeFullName = typeFullName.Replace('+', '.');
            int lastDot = typeFullName.LastIndexOf('.');
            string shortName = (lastDot >= 0) ? typeFullName.Substring(lastDot + 1) : typeFullName;

            // Remove a leading "<BaseType>_" prefix irrespective of namespace.
            // This works for Condition_*, GameAction_*, Trigger_*, etc., even if the class lives in WSKit.*
            string basePrefix = baseType.Name + "_";
            if (shortName.StartsWith(basePrefix, StringComparison.Ordinal))
                shortName = shortName.Substring(basePrefix.Length);
            else
            {
                // Also handle some legacy/hardcoded prefixes if you mix bases.
                if (shortName.StartsWith("Condition_", StringComparison.Ordinal))
                    shortName = shortName.Substring("Condition_".Length);
                else if (shortName.StartsWith("GameAction_", StringComparison.Ordinal))
                    shortName = shortName.Substring("GameAction_".Length);
            }

            return ObjectNames.NicifyVariableName(shortName);
        }

    }
}
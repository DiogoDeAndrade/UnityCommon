using System;
using System.Collections.Generic;
using UC;
using UC.Interaction.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(TurnDriverComposite))]
    public class TurnDriverCompositeDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, ReorderableList> s_lists = new();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property == null) return 0f;

            float h = 0f;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // Draw all normal fields except the drivers list + SOModule internals
            h += SumNonListChildrenHeight(property, spacing);

            // Drivers list
            var driversProp = property.FindPropertyRelative("_drivers");
            if (driversProp != null && driversProp.isArray)
            {
                var list = GetOrCreateList(property, driversProp);
                h += list.GetHeight() + spacing;
            }

            if (h > 0f) h -= spacing;
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property == null) return;

            EditorGUI.BeginProperty(position, label, property);

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            var r = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // Draw non-list fields first
            DrawNonListChildren(ref r, property, spacing);

            // Drivers list
            var driversProp = property.FindPropertyRelative("_drivers");
            if (driversProp != null && driversProp.isArray)
            {
                r.height = GetOrCreateList(property, driversProp).GetHeight();
                GetOrCreateList(property, driversProp).DoList(r);
                r.y += r.height + spacing;
            }

            EditorGUI.EndProperty();
        }

        // -----------------------------
        // Helpers
        // -----------------------------

        private static float SumNonListChildrenHeight(SerializedProperty property, float spacing)
        {
            float height = 0f;

            var iterator = property.Copy();
            var end = property.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                // Skip SerializeReference internals
                if (iterator.name == "managedReferenceFullTypename" || iterator.name == "managedReferenceData")
                    continue;

                // Skip SOModule internals (header already shows enabled + foldout handled by outer panel)
                if (iterator.name == "_enabled" || iterator.name == "enabled" || iterator.name == "_open" || iterator.name == "_scriptableObject")
                    continue;

                // Skip list (we draw it with ReorderableList)
                if (iterator.name == "_drivers")
                    continue;

                height += EditorGUI.GetPropertyHeight(iterator, true) + spacing;
            }

            return height;
        }

        private static void DrawNonListChildren(ref Rect r, SerializedProperty property, float spacing)
        {
            var iterator = property.Copy();
            var end = property.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;

                if (iterator.name == "managedReferenceFullTypename" || iterator.name == "managedReferenceData")
                    continue;

                if (iterator.name == "_enabled" || iterator.name == "enabled" || iterator.name == "_open" || iterator.name == "_scriptableObject")
                    continue;

                if (iterator.name == "_drivers")
                    continue;

                r.height = EditorGUI.GetPropertyHeight(iterator, true);
                EditorGUI.PropertyField(r, iterator, true);
                r.y += r.height + spacing;
            }
        }

        private static ReorderableList GetOrCreateList(SerializedProperty ownerProp, SerializedProperty driversProp)
        {
            // Use property path to keep one list per instance in the inspector
            string key = ownerProp.serializedObject.targetObject.GetInstanceID() + "|" + ownerProp.propertyPath;

            if (s_lists.TryGetValue(key, out var list))
            {
                // Keep the backing prop fresh (Unity can re-create SerializedProperty instances)
                list.serializedProperty = driversProp;
                return list;
            }

            list = new ReorderableList(ownerProp.serializedObject, driversProp, true, true, true, true);

            list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Turn Drivers");
            };

            list.elementHeightCallback = index =>
            {
                var element = driversProp.GetArrayElementAtIndex(index);
                return EditorGUI.GetPropertyHeight(element, true) + 2f;
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                rect.y += 1f;
                rect.height = EditorGUI.GetPropertyHeight(driversProp.GetArrayElementAtIndex(index), true);

                var element = driversProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, element, GUIContent.none, true);
            };

            list.onAddDropdownCallback = (rect, l) =>
            {
                var menu = new GenericMenu();
                var entries = ManagedReferenceTypeCache.GetAssignableConcreteTypes(typeof(TurnDriver));

                if (entries == null || entries.Count == 0)
                {
                    menu.AddDisabledItem(new GUIContent("No TurnDriver types found"));
                }
                else
                {
                    foreach (var (displayName, type) in entries)
                    {
                        menu.AddItem(new GUIContent(displayName), false, () =>
                        {
                            AddDriver(ownerProp, driversProp, type);
                        });
                    }
                }

                menu.ShowAsContext();
            };

            list.onRemoveCallback = l =>
            {
                RemoveDriver(ownerProp, driversProp, l.index);
            };

            s_lists[key] = list;
            return list;
        }

        private static void AddDriver(SerializedProperty ownerProp, SerializedProperty driversProp, Type type)
        {
            if (type == null || !typeof(TurnDriver).IsAssignableFrom(type))
                return;

            var target = ownerProp.serializedObject.targetObject;
            Undo.RecordObject(target, "Add Turn Driver");

            int newIndex = driversProp.arraySize;
            driversProp.InsertArrayElementAtIndex(newIndex);

            var element = driversProp.GetArrayElementAtIndex(newIndex);
            element.managedReferenceValue = Activator.CreateInstance(type);

            ownerProp.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static void RemoveDriver(SerializedProperty ownerProp, SerializedProperty driversProp, int index)
        {
            if (index < 0 || index >= driversProp.arraySize)
                return;

            var target = ownerProp.serializedObject.targetObject;
            Undo.RecordObject(target, "Remove Turn Driver");

            // ManagedReference arrays sometimes need a double-delete to fully remove
            driversProp.DeleteArrayElementAtIndex(index);
            if (index < driversProp.arraySize &&
                driversProp.GetArrayElementAtIndex(index).managedReferenceValue == null)
            {
                driversProp.DeleteArrayElementAtIndex(index);
            }

            ownerProp.serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }
}

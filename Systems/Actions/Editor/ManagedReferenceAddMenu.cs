// Assets/Editor/ManagedReferenceAddMenu.cs
using System;
using UnityEditor;
using UnityEngine;

namespace UC.Interaction.Editor
{
    public static class ManagedReferenceAddMenu
    {
        public static void Show(Rect buttonRect, Type baseType, Action<object> onCreate)
        {
            var types = ManagedReferenceTypeCache.GetAssignableConcreteTypes(baseType);
            var menu = new GenericMenu();

            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent($"No concrete types for {baseType.Name}"));
            }
            else
            {
                foreach (var (display, type) in types)
                {
                    menu.AddItem(new GUIContent(display), false, () =>
                    {
                        try
                        {
                            var instance = Activator.CreateInstance(type);
                            onCreate?.Invoke(instance);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    });
                }
            }

            menu.DropDown(buttonRect);
        }

        public static void InsertNewManagedElement(SerializedProperty arrayProp, int index, object instance)
        {
            // Insert array element
            arrayProp.serializedObject.Update();
            arrayProp.InsertArrayElementAtIndex(index);

            var element = arrayProp.GetArrayElementAtIndex(index);
            element.managedReferenceValue = instance; // this sets the full typename & data

            arrayProp.serializedObject.ApplyModifiedProperties();
        }
    }
}
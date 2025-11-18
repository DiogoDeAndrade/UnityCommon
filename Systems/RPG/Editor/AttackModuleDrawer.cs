using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UC; // <-- so it sees AttackModule

[CustomPropertyDrawer(typeof(AttackModule), true)]
public class AttackModuleDrawer : PropertyDrawer
{
    private static Type[] _types;
    private static string[] _displayNames;

    static AttackModuleDrawer()
    {
        // Find all non-abstract AttackModule types in the project
        var list = new List<Type>();

        foreach (var t in TypeCache.GetTypesDerivedFrom<AttackModule>())
        {
            if (!t.IsAbstract && !t.IsGenericType && t.IsClass)
                list.Add(t);
        }

        _types = list.ToArray();
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
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                height += EditorGUI.GetPropertyHeight(iterator, true) +
                          EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
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

        EditorGUI.LabelField(labelRect, label);

        int currentIndex = GetCurrentTypeIndex(property);
        int newIndex = EditorGUI.Popup(popupRect, currentIndex + 1, _displayNames) - 1;

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

        // Draw fields of the selected AttackModule below
        if (property.managedReferenceValue != null)
        {
            EditorGUI.indentLevel++;

            float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            var iterator = property.Copy();
            var end = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                float h = EditorGUI.GetPropertyHeight(iterator, true);
                Rect r = new Rect(position.x, y, position.width, h);
                EditorGUI.PropertyField(r, iterator, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
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

    private static string MakeNiceName(Type t)
    {
        string name = t.Name;

        // Strip common suffix, similar to your ResourceValueFunction case
        const string suffix = "AttackModule";
        if (name.EndsWith(suffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - suffix.Length);
        }

        // Insert spaces in PascalCase
        if (string.IsNullOrEmpty(name))
            name = t.Name;

        var result = new System.Text.StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(name[i - 1]))
                result.Append(' ');
            result.Append(c);
        }

        return result.ToString();
    }
}

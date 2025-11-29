using UnityEditor;
using UnityEngine;
using NaughtyAttributes.Editor;
using UC;

// Inline drawer for any SOModule (and children) with no extra foldout.
// It basically does what DrawManagedReferenceChildren used to do.
[CustomPropertyDrawer(typeof(SOModule), true)]
public class SOModuleDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property == null)
            return 0f;

        float height = 0f;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty iterator = property.Copy();
        SerializedProperty end = property.GetEndProperty();

        bool enterChildren = true;

        while ((iterator.NextVisible(enterChildren)) && (!SerializedProperty.EqualContents(iterator, end)))
        {
            enterChildren = false;

            // Skip SerializeReference internals
            if (iterator.name == "managedReferenceFullTypename" ||
                iterator.name == "managedReferenceData")
                continue;

            // Skip enabled field, header already shows it
            if (iterator.name == "_enabled" || iterator.name == "enabled")
                continue;

            height += EditorGUI.GetPropertyHeight(iterator, true) + spacing;
        }

        if (height > 0f)
        {
            height -= spacing; // remove last extra spacing
        }

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property == null)
        {
            return;
        }

        EditorGUI.BeginProperty(position, label, property);

        float spacing = EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty iterator = property.Copy();
        SerializedProperty end = property.GetEndProperty();

        bool enterChildren = true;

        Rect rowRect = new Rect(position.x, position.y, position.width, 0f);

        while ((iterator.NextVisible(enterChildren)) && (!SerializedProperty.EqualContents(iterator, end)))
        {
            enterChildren = false;

            // Skip SerializeReference internals
            if ((iterator.name == "managedReferenceFullTypename") || (iterator.name == "managedReferenceData"))
                continue;

            // Skip enabled field, header already shows it
            if ((iterator.name == "_enabled") || (iterator.name == "enabled"))
                continue;

            float h = EditorGUI.GetPropertyHeight(iterator, true);
            rowRect.height = h;

            // Non-layout version of NaughtyAttributes’ drawing
            NaughtyEditorGUI.PropertyField(rowRect, iterator, true);

            rowRect.y += h + spacing;
        }

        EditorGUI.EndProperty();
    }
}

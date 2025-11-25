using UnityEditor;
using UnityEngine;
using UC.RPG;   // adjust if Archetype is in a different namespace

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(Archetype.EquipmentSlot))]
    public class EquipmentSlotDrawer : PropertyDrawer
    {
        bool IsArrayElementLabel(GUIContent label)
        {
            return label != null && label.text != null && label.text.StartsWith("Element");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Always one line, both in arrays and as a single field
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool isArrayElement = IsArrayElementLabel(label);

            EditorGUI.BeginProperty(position, label, property);

            int oldIndent = EditorGUI.indentLevel;
            float lineH = EditorGUIUtility.singleLineHeight;
            float spacing = 4f;

            Rect contentRect;

            if (isArrayElement)
            {
                // No "Element 0" label, full-width content
                EditorGUI.indentLevel = 0;
                contentRect = position;
            }
            else
            {
                // Draw the field label, get remaining rect for content
                contentRect = EditorGUI.PrefixLabel(position, label);
                EditorGUI.indentLevel = 0; // don't re-indent inside the content area
            }

            float halfWidth = (contentRect.width - spacing) * 0.5f;

            Rect slotRect = new Rect(contentRect.x, contentRect.y, halfWidth, lineH);
            Rect itemRect = new Rect(contentRect.x + halfWidth + spacing, contentRect.y, halfWidth, lineH);

            SerializedProperty slotProp = property.FindPropertyRelative("slot");
            SerializedProperty itemProp = property.FindPropertyRelative("item");

            EditorGUI.PropertyField(slotRect, slotProp, GUIContent.none);
            EditorGUI.PropertyField(itemRect, itemProp, GUIContent.none);

            EditorGUI.indentLevel = oldIndent;
            EditorGUI.EndProperty();
        }
    }
}

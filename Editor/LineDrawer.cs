using UnityEditor;
using UnityEngine;

namespace UC
{
    [CustomPropertyDrawer(typeof(Line))]
    public class LineDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Draw main label
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

            // Spacing values
            float arrowWidth = 20f;
            float spacing = 4f;
            float halfWidth = (position.width - arrowWidth - spacing * 2) / 2f;

            // Rects
            Rect p0Rect = new Rect(position.x, position.y, halfWidth, position.height);
            Rect arrowRect = new Rect(position.x + halfWidth + spacing, position.y, arrowWidth, position.height);
            Rect p1Rect = new Rect(arrowRect.xMax + spacing, position.y, halfWidth, position.height);

            // Draw fields
            EditorGUI.PropertyField(p0Rect, property.FindPropertyRelative("p0"), GUIContent.none);
            EditorGUI.LabelField(arrowRect, "->", EditorStyles.centeredGreyMiniLabel);
            EditorGUI.PropertyField(p1Rect, property.FindPropertyRelative("p1"), GUIContent.none);

            EditorGUI.EndProperty();
        }
    }
}

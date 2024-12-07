using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ColorPalette.ColorEntry))]
public class ColorEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Remove the default label
        EditorGUI.BeginProperty(position, label, property);

        // Split the rect into two parts for name and color
        float nameWidth = position.width * 0.2f; // Adjust this ratio as needed
        float colorWidth = position.width * 0.8f;

        Rect nameRect = new Rect(position.x, position.y, nameWidth, position.height);
        Rect colorRect = new Rect(position.x + nameWidth, position.y, colorWidth, position.height);

        // Draw fields for name and color
        SerializedProperty nameProperty = property.FindPropertyRelative("name");
        SerializedProperty colorProperty = property.FindPropertyRelative("color");

        EditorGUI.PropertyField(nameRect, nameProperty, GUIContent.none);
        EditorGUI.PropertyField(colorRect, colorProperty, GUIContent.none);

        EditorGUI.EndProperty();
    }
}

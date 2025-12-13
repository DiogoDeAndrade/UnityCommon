using UnityEditor;
using UnityEngine;

namespace UC.RPG.Editor
{ 
    [CustomPropertyDrawer(typeof(RPGTarget))]
    public class RPGTargetDrawer : PropertyDrawer
    {
        const float TypeWidthRatio = 0.4f;
        const float Spacing = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Draw label and get remaining rect
            position = EditorGUI.PrefixLabel(
                position,
                GUIUtility.GetControlID(FocusType.Passive),
                label
            );

            // Fetch properties
            var typeProp = property.FindPropertyRelative("type");
            var unityEntityProp = property.FindPropertyRelative("unityEntity");

            // Split line
            float typeWidth = position.width * TypeWidthRatio;
            var typeRect = new Rect(position.x, position.y, typeWidth, position.height);
            var entityRect = new Rect(
                position.x + typeWidth + Spacing,
                position.y,
                position.width - typeWidth - Spacing,
                position.height
            );

            // Draw enum
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

            // Draw unityEntity only if needed
            if ((RPGTarget.Type)typeProp.intValue == RPGTarget.Type.UnityReference)
            {
                EditorGUI.PropertyField(entityRect, unityEntityProp, GUIContent.none);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
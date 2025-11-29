using UC.RPG;
using UnityEditor;
using UnityEngine;

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(RequireModuleAttribute))]
    public class RequireModuleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float baseHeight = EditorGUI.GetPropertyHeight(property, label, true);

            return NeedsWarning(property) ? baseHeight + EditorGUIUtility.singleLineHeight * 1.5f : baseHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float fieldHeight = EditorGUI.GetPropertyHeight(property, label, true);
            Rect fieldRect = new Rect(position.x, position.y, position.width, fieldHeight);

            EditorGUI.PropertyField(fieldRect, property, label, true);

            if (NeedsWarning(property))
            {
                Rect helpRect = new Rect(
                    position.x,
                    position.y + fieldHeight + 2f,
                    position.width,
                    EditorGUIUtility.singleLineHeight * 1.3f
                );

                var attr = (RequireModuleAttribute)attribute;
                string msg = $"Item must contain module: {attr.moduleType.Name}";
                EditorGUI.HelpBox(helpRect, msg, MessageType.Warning);
            }
        }

        private bool NeedsWarning(SerializedProperty property)
        {
            var attr = (RequireModuleAttribute)attribute;

            if (property.propertyType != SerializedPropertyType.ObjectReference)
                return false;

            var obj = property.objectReferenceValue;
            if (obj == null)
                return false; // if you also want a warning when unassigned, change this

            var item = obj as Item;
            if (item == null)
                return true; // wrong type entirely

            return !item.HasModule(attr.moduleType, attr.includeParents);
        }
    }
}

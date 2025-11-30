using UnityEditor;
using UnityEngine;

namespace UC.Editor
{ 

    [CustomPropertyDrawer(typeof(GridActionCondition))]
    public class GridActionConditionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty conditionTypeProp = property.FindPropertyRelative("conditionType");

            Rect conditionTypeRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(conditionTypeRect, conditionTypeProp, GUIContent.none);

            if ((GridActionCondition.ConditionType)conditionTypeProp.enumValueIndex == GridActionCondition.ConditionType.ResourceValue)
            {
                SerializedProperty targetTagProp = property.FindPropertyRelative("targetTag");
                SerializedProperty resourceTypeProp = property.FindPropertyRelative("resourceType");
                SerializedProperty comparisonProp = property.FindPropertyRelative("comparison");
                SerializedProperty refValueProp = property.FindPropertyRelative("refValue");

                Rect targetTagRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width / 3, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(targetTagRect, targetTagProp, GUIContent.none);

                Rect resourceTypeRect = new Rect(position.x, position.y + (EditorGUIUtility.singleLineHeight + 2) * 2, position.width / 3, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(resourceTypeRect, resourceTypeProp, GUIContent.none);

                Rect comparisonRect = resourceTypeRect; comparisonRect.x += position.width / 3;
                EditorGUI.PropertyField(comparisonRect, comparisonProp, GUIContent.none);

                Rect refValueRect = comparisonRect; refValueRect.x += position.width / 3;
                EditorGUI.PropertyField(refValueRect, refValueProp, GUIContent.none);
            }
            else if ((GridActionCondition.ConditionType)conditionTypeProp.enumValueIndex == GridActionCondition.ConditionType.Expression)
            {
                SerializedProperty expressionProp = property.FindPropertyRelative("expression");

                Rect expressionRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(expressionRect, expressionProp, GUIContent.none);
            }
            else if ((GridActionCondition.ConditionType)conditionTypeProp.enumValueIndex == GridActionCondition.ConditionType.ItemCount)
            {
                SerializedProperty targetTagProp = property.FindPropertyRelative("targetTag");
                SerializedProperty itemProp = property.FindPropertyRelative("item");
                SerializedProperty comparisonProp = property.FindPropertyRelative("comparison");
                SerializedProperty refValueProp = property.FindPropertyRelative("itemQuantity");

                Rect targetTagRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width / 3, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(targetTagRect, targetTagProp, GUIContent.none);

                Rect resourceTypeRect = new Rect(position.x, position.y + (EditorGUIUtility.singleLineHeight + 2) * 2, position.width / 3, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(resourceTypeRect, itemProp, GUIContent.none);

                Rect comparisonRect = resourceTypeRect; comparisonRect.x += position.width / 3;
                EditorGUI.PropertyField(comparisonRect, comparisonProp, GUIContent.none);

                Rect refValueRect = comparisonRect; refValueRect.x += position.width / 3;
                EditorGUI.PropertyField(refValueRect, refValueProp, GUIContent.none);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty conditionTypeProp = property.FindPropertyRelative("conditionType");

            if ((GridActionCondition.ConditionType)conditionTypeProp.enumValueIndex == GridActionCondition.ConditionType.ResourceValue)
            {
                return EditorGUIUtility.singleLineHeight * 3 + 8;
            }
            else if ((GridActionCondition.ConditionType)conditionTypeProp.enumValueIndex == GridActionCondition.ConditionType.Expression)
            {
                return EditorGUIUtility.singleLineHeight * 2 + 4;
            }
            else if ((GridActionCondition.ConditionType)conditionTypeProp.enumValueIndex == GridActionCondition.ConditionType.ItemCount)
            {
                return EditorGUIUtility.singleLineHeight * 3 + 8;
            }

            return EditorGUIUtility.singleLineHeight + 2;
        }
    }
}
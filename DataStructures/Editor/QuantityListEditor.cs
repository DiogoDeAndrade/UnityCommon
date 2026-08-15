using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    public class QuantityListPropertyDrawer<T> : BaseProbListPropertyDrawer<T>
    {
        protected override float GetValueHeaderHeight(SerializedProperty valueProperty)
        {
            return EditorGUI.GetPropertyHeight(valueProperty, GUIContent.none, true);
        }

        protected override void DrawValueHeader(Rect position, SerializedProperty valueProperty)
        {
            EditorGUI.PropertyField(position, valueProperty, GUIContent.none, true);
        }

        protected override void InitializeValue(SerializedProperty valueProperty)
        {
            switch (valueProperty.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    valueProperty.objectReferenceValue = null;
                    break;

                case SerializedPropertyType.String:
                    valueProperty.stringValue = "";
                    break;
            }
        }

        protected override bool HasReplacementOption => false;
    }

    public class ReferenceQuantityListPropertyDrawer<T> : BaseProbListPropertyDrawer<T>
    {
        protected override float GetValueHeaderHeight(SerializedProperty valueProperty)
        {
            return BaseFunctionDrawer<T>.GetReferenceHeaderHeight(valueProperty);
        }

        protected override float GetValueBodyHeight(SerializedProperty valueProperty)
        {
            return BaseFunctionDrawer<T>.GetReferenceChildrenHeight(valueProperty);
        }

        protected override void DrawValueHeader(Rect position, SerializedProperty valueProperty)
        {
            BaseFunctionDrawer<T>.DrawReferenceHeader(position, valueProperty, GUIContent.none, inline: true);
        }

        protected override void DrawValueBody(Rect position, SerializedProperty valueProperty)
        {
            BaseFunctionDrawer<T>.DrawReferenceChildren(position, valueProperty);
        }

        protected override void InitializeValue(SerializedProperty valueProperty)
        {
            valueProperty.managedReferenceValue = null;
        }

        protected override bool HasReplacementOption => false;
    }

    [CustomPropertyDrawer(typeof(StringQuantityList))]
    public class StringQuantityListDrawer : QuantityListPropertyDrawer<string>
    {

    }
}

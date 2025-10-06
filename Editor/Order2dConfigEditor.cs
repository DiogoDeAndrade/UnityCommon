using UnityEditor;
using UnityEngine;
using UC;

namespace UC.Editor
{

    [CustomEditor(typeof(Order2dConfig))]
    public class Order2dConfigEditor : UnityEditor.Editor
    {
        SerializedProperty _orderMode;
        SerializedProperty _orderScaleY;
        SerializedProperty _orderMin;
        SerializedProperty _orderMax;
        SerializedProperty _orderMinZ;
        SerializedProperty _orderMaxZ;

        void OnEnable()
        {
            _orderMode = serializedObject.FindProperty("_orderMode");
            _orderScaleY = serializedObject.FindProperty("_orderScaleY");
            _orderMin = serializedObject.FindProperty("_orderMin");
            _orderMax = serializedObject.FindProperty("_orderMax");
            _orderMinZ = serializedObject.FindProperty("_orderMinZ");
            _orderMaxZ = serializedObject.FindProperty("_orderMaxZ");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_orderMode);

            OrderMode mode = (OrderMode)_orderMode.enumValueIndex;

            switch (mode)
            {
                case OrderMode.Z:
                    EditorGUILayout.PropertyField(_orderScaleY);
                    if (_orderScaleY.floatValue != 0)
                    {
                        float angleRad = Mathf.Atan(_orderScaleY.floatValue);
                        float angleDeg = angleRad * Mathf.Rad2Deg;
                        EditorGUILayout.HelpBox($"Angle of slope: {angleDeg:F2}°", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Slope is 0 (horizontal plane)", MessageType.Info);
                    }

                    EditorGUILayout.PropertyField(_orderMinZ);
                    EditorGUILayout.PropertyField(_orderMaxZ);
                    break;

                case OrderMode.Order:
                default:
                    EditorGUILayout.PropertyField(_orderMin);
                    EditorGUILayout.PropertyField(_orderMax);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
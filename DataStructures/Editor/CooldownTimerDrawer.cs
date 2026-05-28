// Assets/Editor/CooldownTimerDrawer.cs

using System;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    [CustomPropertyDrawer(typeof(CooldownTimer))]
    public class CooldownTimerDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty cooldownProp = property.FindPropertyRelative(nameof(CooldownTimer.cooldown));

            if (cooldownProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Missing cooldown field");
                EditorGUI.EndProperty();
                return;
            }

            RangeAttribute range = GetAttribute<RangeAttribute>();
            MinAttribute min = GetAttribute<MinAttribute>();
            MaxAttribute max = GetAttribute<MaxAttribute>();

            EditorGUI.BeginChangeCheck();

            float value = cooldownProp.floatValue;

            if (range != null)
            {
                value = EditorGUI.Slider(position, label, value, range.min, range.max);
            }
            else if (min != null && max != null)
            {
                value = EditorGUI.Slider(position, label, value, min.min, max.max);
            }
            else
            {
                value = EditorGUI.FloatField(position, label, value);

                if (min != null)
                    value = Mathf.Max(value, min.min);

                if (max != null)
                    value = Mathf.Min(value, max.max);
            }

            if (EditorGUI.EndChangeCheck())
            {
                cooldownProp.floatValue = value;
            }

            EditorGUI.EndProperty();
        }

        private T GetAttribute<T>() where T : Attribute
        {
            return Attribute.GetCustomAttribute(fieldInfo, typeof(T)) as T;
        }
    }
}
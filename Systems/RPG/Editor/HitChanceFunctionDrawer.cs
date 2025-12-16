using NUnit.Framework;
using UC.Editor;
using UnityEditor;
using UnityEngine;

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(HitChanceFunction), true)]
    public class HitChanceFunctionDrawer : BaseFunctionDrawer<HitChanceFunction>
    {
        const float GraphHeight = 110f;
        const float GraphPadding = 6f;
        const float GraphSpacing = 6f;

        const int DiffRange = 10; // [-5, +5]

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = base.GetPropertyHeight(property, label);

            var displayGraphProp = property.FindPropertyRelative("displayGraph");
            if (displayGraphProp != null)
            {
                h += EditorGUIUtility.singleLineHeight;
            }

            if (ShouldDrawGraph(property))
                h += GraphSpacing + GraphHeight;

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float baseHeight = base.GetPropertyHeight(property, label);

            // Normal inspector UI
            var baseRect = new Rect(position.x, position.y, position.width, baseHeight);
            base.OnGUI(baseRect, property, label);

            var displayGraphProp = property.FindPropertyRelative("displayGraph");
            if (displayGraphProp != null)
            {
                var propRect = new Rect(position.x, position.y + baseHeight, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(propRect, displayGraphProp, new GUIContent("Preview"));
            }

            if (!ShouldDrawGraph(property))
                return;

            var graphRect = new Rect(position.x + position.width * 0.1f,
                position.y + baseHeight + GraphSpacing + EditorGUIUtility.singleLineHeight,
                position.width * 0.9f,
                GraphHeight
            );

            DrawPreviewGraph(graphRect, property);
        }

        static bool ShouldDrawGraph(SerializedProperty property)
        {
            if (property.managedReferenceValue is not HitChanceFunction fn)
                return false;

            var displayGraphProp = property.FindPropertyRelative("displayGraph");
            if ((displayGraphProp != null) && (!displayGraphProp.boolValue)) return false;

            return fn.CanPreview();
        }

        static void DrawPreviewGraph(Rect rect, SerializedProperty property)
        {
            var fn = property.managedReferenceValue as HitChanceFunction;
            if (fn == null)
                return;

            var title = $"Hit chance preview (level diff -{DiffRange} ... +{DiffRange})";
            GUIUtils.DrawPreviewGraph(rect, title, GraphPadding, -DiffRange, DiffRange, 1.0f, 1.0f, (x) => fn.GetPreviewValue((int)x), LabelFunction, true, 0.0f, 1.0f);
        }

        static string LabelFunction(float x, float y, bool isVerticalAxis)
        {
            if ((x == 0) || (x == -1) || (x == +1) || (x == -DiffRange) || (x == +DiffRange) || isVerticalAxis)
                return $"{Mathf.FloorToInt(y * 100.0f)}%";

            return "";
        }
    }
}

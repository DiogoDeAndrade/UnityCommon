using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    [CustomPropertyDrawer(typeof(UC.ValueRange))]
    public class ValueRangeDrawer : PropertyDrawer
    {
        const float Line = 18f;
        const float VSpace = 2f;
        const float GraphH = 110f;

        // mini-label drag sensitivity: pixels -> value delta
        const float DragSpeed = 0.02f;

        static bool showGraph = true;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var mode = GetMode(property);

            bool needsSecondLine = mode == (ValueRange.Mode.GaussianClamped) || (mode == ValueRange.Mode.BiasedUniform);

            float h = Line;                  // main line
            if (needsSecondLine) h += VSpace + Line;

            if (showGraph)
            {
                h += VSpace + GraphH;        // preview graph
            }
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var modeProp = property.FindPropertyRelative("mode");
            var meanProp = property.FindPropertyRelative("mean");
            var rangeProp = property.FindPropertyRelative("range");
            var sigmaProp = property.FindPropertyRelative("gaussianSigmaFrac");
            var biasProp = property.FindPropertyRelative("bias");

            var mode = (ValueRange.Mode)modeProp.enumValueIndex;
            bool needsSecondLine = (mode == ValueRange.Mode.GaussianClamped) || (mode == ValueRange.Mode.BiasedUniform);

            // ---- Layout rects ----
            Rect line1 = new Rect(position.x, position.y, position.width - 20.0f, Line);

            // First line: [LABEL][TYPE][Mean][Range?]
            DrawFirstLine(line1, label, modeProp, meanProp, rangeProp, mode);

            float y = position.y + Line + VSpace;

            if (needsSecondLine)
            {
                Rect line2 = new Rect(position.x, y, position.width, Line);
                DrawSecondLine(line2, mode, sigmaProp, biasProp);

                // Toggle at end of 2nd line, with "Preview:"
                DrawGraphToggle(line2, showLabel: true);

                y += Line + VSpace;
            }
            else
            {
                // Toggle at end of 1st line, no label
                line1.width += 20.0f;
                DrawGraphToggle(line1, showLabel: false);
            }

            // Graph
            if (showGraph)
            {
                Rect graphRect = new Rect(position.x, y, position.width, GraphH);

                ValueRange vr = GUIUtils.GetTargetObjectOfProperty(property) as ValueRange;
                if (vr == null)
                    return;

                vr.GetPreviewDomain(out float xMin, out float xMax);
                float range = (xMax - xMin);
                float divs = 0.1f;
                GUIUtils.DrawPreviewGraph(graphRect, null, 20.0f, xMin - divs * range, xMax + divs * range, range * divs * 0.25f, range * divs, (x) => vr.GetRelativeDensity(x), null, xCenter: vr.mean, centralColor: Color.red);
            }

            EditorGUI.EndProperty();
        }

        // =========================
        // Line 1
        // =========================
        static void DrawFirstLine(Rect rect, GUIContent label, SerializedProperty modeProp, SerializedProperty meanProp, SerializedProperty rangeProp, ValueRange.Mode mode)
        {
            float spacing = 6f;

            // Label
            Rect rLabel = rect;
            rLabel.width = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(rLabel, label);

            // Remaining
            float x = rect.x + rLabel.width + spacing;
            float w = rect.xMax - x;

            // Give more space to type
            float typeW = Mathf.Clamp(w * 0.38f, 110f, 180f);

            // Two numeric slots
            float fieldW = Mathf.Clamp((w - typeW - spacing * 2f) / 2f, 80f, 140f);

            Rect rType = new Rect(x, rect.y, typeW, rect.height);
            EditorGUI.PropertyField(rType, modeProp, GUIContent.none);

            x += typeW + spacing;

            // Mean (mini label + draggable + float field)
            Rect rMean = new Rect(x, rect.y, fieldW, rect.height);
            DrawMiniLabeledFloat(rMean, "Mean", meanProp);

            x += fieldW + spacing;

            if (UsesRange(mode))
            {
                Rect rRange = new Rect(x, rect.y, fieldW, rect.height);
                DrawMiniLabeledFloat(rRange, "Range", rangeProp);

                // Clamp: Range can never be negative
                if (rangeProp.floatValue < 0f)
                    rangeProp.floatValue = 0f;
            }
        }

        // =========================
        // Line 2
        // =========================
        static void DrawSecondLine(Rect rect, ValueRange.Mode mode, SerializedProperty sigmaProp, SerializedProperty biasProp)
        {
            float spacing = 6f;

            // align under fields (skip inspector label column)
            float x = rect.x + EditorGUIUtility.labelWidth + spacing;
            float w = rect.xMax - x;

            float fieldW = Mathf.Clamp(w * 0.35f, 110f, 180f);

            if (mode == ValueRange.Mode.GaussianClamped)
            {
                Rect r = new Rect(x, rect.y, fieldW, rect.height);
                DrawMiniLabeledFloat(r, "Sigma", sigmaProp);
            }
            else if (mode == ValueRange.Mode.BiasedUniform)
            {
                Rect r = new Rect(x, rect.y, fieldW, rect.height);
                DrawMiniLabeledFloat(r, "Bias", biasProp);
            }
        }

        // =========================
        // Mini label + draggable float field
        // =========================
        static void DrawMiniLabeledFloat(Rect rect, string miniLabel, SerializedProperty valueProp)
        {
            // Split: mini label (left) + field (right)
            float labelW = Mathf.Max(rect.width * 0.45f, 42f);
            Rect rLab = new Rect(rect.x, rect.y, labelW, rect.height);
            Rect rVal = new Rect(rect.x + labelW, rect.y, rect.width - labelW, rect.height);

            // Draw mini label
            GUI.Label(rLab, miniLabel, EditorStyles.miniLabel);

            // Show draggable cursor like Unity numeric labels
            EditorGUIUtility.AddCursorRect(rLab, MouseCursor.SlideArrow);

            // Drag on the label like Unity numeric labels
            HandleLabelDrag(rLab, valueProp);

            // Value field (no label)
            EditorGUI.PropertyField(rVal, valueProp, GUIContent.none);
        }

        static void HandleLabelDrag(Rect labelRect, SerializedProperty prop)
        {
            // Only supports float/int props
            bool isFloat = prop.propertyType == SerializedPropertyType.Float;
            bool isInt = prop.propertyType == SerializedPropertyType.Integer;
            if (!isFloat && !isInt)
                return;

            int id = GUIUtility.GetControlID(FocusType.Passive, labelRect);
            var e = Event.current;

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && labelRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        EditorGUIUtility.SetWantsMouseJumping(1);

                        DragState.startMouse = e.mousePosition;
                        DragState.startValue = isFloat ? prop.floatValue : prop.intValue;

                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        float dx = e.mousePosition.x - DragState.startMouse.x;
                        float dy = DragState.startMouse.y - e.mousePosition.y;

                        float baseVal = DragState.startValue;
                        float scale = Mathf.Max(1f, Mathf.Abs(baseVal) * 0.1f);
                        float delta = (dx + dy) * DragSpeed * scale;

                        if (isFloat)
                        {
                            float v = DragState.startValue + delta;

                            // Clamp "range" to >= 0
                            if (prop.name == "range")
                                v = Mathf.Max(0f, v);

                            prop.floatValue = v;
                        }
                        else
                            prop.intValue = Mathf.RoundToInt(DragState.startValue + delta);

                        prop.serializedObject.ApplyModifiedProperties();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        EditorGUIUtility.SetWantsMouseJumping(0);
                        e.Use();
                    }
                    break;
            }
        }

        struct DragState
        {
            public static Vector2 startMouse;
            public static float startValue;
        }

        // =========================
        // Utilities
        // =========================
        static bool UsesRange(ValueRange.Mode mode) => mode != ValueRange.Mode.Constant;

        static ValueRange.Mode GetMode(SerializedProperty property)
        {
            var modeProp = property.FindPropertyRelative("mode");
            return (ValueRange.Mode)modeProp.enumValueIndex;
        }

        static void DrawGraphToggle(Rect lineRect, bool showLabel)
        {
            const float toggleW = 16f;
            const float labelW = 52f;
            const float pad = 4f;

            var toggleRect = new Rect(lineRect.xMax - toggleW, lineRect.y + 1f, toggleW, lineRect.height);

            if (showLabel)
            {
                var labelRect = new Rect(toggleRect.x - pad - labelW, lineRect.y, labelW, lineRect.height);
                EditorGUI.LabelField(labelRect, "Preview:", EditorStyles.miniLabel);
            }

            // Tiny checkbox (no text). Use Toggle so it looks like Unity's normal checkbox.
            showGraph = EditorGUI.Toggle(toggleRect, showGraph);
        }
    }
}

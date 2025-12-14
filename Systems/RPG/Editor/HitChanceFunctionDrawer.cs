using UC.Editor;
using UnityEditor;
using UnityEngine;

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(HitChanceFunction), true)]
    public class HitChanceFunctionDrawer : BaseFunctionDrawer<HitChanceFunction>
    {
        const float GraphHeight = 110f;
        const float Padding = 6f;
        const float Spacing = 6f;

        const int DiffRange = 10; // [-5, +5]

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = base.GetPropertyHeight(property, label);

            if (ShouldDrawGraph(property))
                h += Spacing + GraphHeight;

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float baseHeight = base.GetPropertyHeight(property, label);

            // Normal inspector UI
            var baseRect = new Rect(position.x, position.y, position.width, baseHeight);
            base.OnGUI(baseRect, property, label);

            if (!ShouldDrawGraph(property))
                return;

            var graphRect = new Rect(
                position.x + position.width * 0.1f,
                position.y + baseHeight + Spacing,
                position.width * 0.9f,
                GraphHeight
            );

            DrawPreviewGraph(graphRect, property);
        }

        static bool ShouldDrawGraph(SerializedProperty property)
        {
            if (property.managedReferenceValue is not HitChanceFunction fn)
                return false;

            return fn.CanPreview();
        }

        static void DrawPreviewGraph(Rect rect, SerializedProperty property)
        {
            var fn = property.managedReferenceValue as HitChanceFunction;
            if (fn == null)
                return;

            // Background
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.12f));

            // Header
            EditorGUI.LabelField(
                new Rect(rect.x + 6, rect.y + 2, rect.width - 12, 16),
                $"Hit chance preview (level diff -{DiffRange} ... +{DiffRange})",
                EditorStyles.miniLabel
            );

            // Plot area
            Rect plot = rect;
            plot.xMin += Padding;
            plot.xMax -= Padding;
            plot.yMin += 18f;
            plot.yMax -= 16f;

            Handles.BeginGUI();

            // Border
            Handles.color = new Color(1, 1, 1, 0.15f);
            Handles.DrawAAPolyLine(1f, new Vector3[]
            {
                new(plot.xMin, plot.yMin),
                new(plot.xMax, plot.yMin),
                new(plot.xMax, plot.yMax),
                new(plot.xMin, plot.yMax),
                new(plot.xMin, plot.yMin),
            });

            // Horizontal grid (fixed 0..1)
            DrawHGrid(plot, 0.0f);
            DrawHGrid(plot, 0.25f);
            DrawHGrid(plot, 0.5f);
            DrawHGrid(plot, 0.75f);
            DrawHGrid(plot, 1.0f);

            // Vertical grid (integer diffs)
            for (int d = -DiffRange; d <= DiffRange; d++)
            {
                float u = (d + DiffRange) / (float)(2 * DiffRange);
                float x = Mathf.Lerp(plot.xMin, plot.xMax, u);
                Handles.color = new Color(1, 1, 1, d == 0 ? 0.22f : 0.10f);
                Handles.DrawLine(new Vector3(x, plot.yMin), new Vector3(x, plot.yMax));
            }

            // Curve sampling
            int samples = (2 * DiffRange) * 40;
            Vector3[] pts = new Vector3[samples];

            for (int i = 0; i < samples; i++)
            {
                float u = i / (samples - 1f);
                float diff = Mathf.Lerp(-DiffRange, DiffRange, u);

                float p = Mathf.Clamp01(fn.GetPreviewValue(Mathf.RoundToInt(diff)));

                float x = Mathf.Lerp(plot.xMin, plot.xMax, u);
                float y = Mathf.Lerp(plot.yMax, plot.yMin, p);

                pts[i] = new Vector3(x, y);
            }

            Handles.color = new Color(0.35f, 0.9f, 1f, 0.9f);
            Handles.DrawAAPolyLine(2f, pts);

            // Markers + labels
            for (int d = -DiffRange; d <= DiffRange; d++)
            {
                float u = (d + DiffRange) / (float)(2 * DiffRange);
                float p = Mathf.Clamp01(fn.GetPreviewValue(d));

                float x = Mathf.Lerp(plot.xMin, plot.xMax, u);
                float y = Mathf.Lerp(plot.yMax, plot.yMin, p);

                Handles.color = Color.white;
                Handles.DrawSolidDisc(new Vector3(x, y), Vector3.forward, 2.4f);

                GUI.Label(
                    new Rect(x - 10, rect.yMax - 16, 20, 16),
                    d.ToString(),
                    EditorStyles.centeredGreyMiniLabel
                );

                if (d == 0 || d == -1 || d == +1 || d == -DiffRange || d == +DiffRange)
                {
                    GUI.Label(
                        new Rect(x + 4, y - 14, 50, 16),
                        Mathf.RoundToInt(p * 100f) + "%",
                        EditorStyles.miniLabel
                    );
                }
            }

            Handles.EndGUI();
        }

        static void DrawHGrid(Rect plot, float y01)
        {
            float y = Mathf.Lerp(plot.yMax, plot.yMin, y01);
            Handles.color = new Color(1, 1, 1, y01 == 0.5f ? 0.18f : 0.10f);
            Handles.DrawLine(new Vector3(plot.xMin, y), new Vector3(plot.xMax, y));

            GUI.Label(
                new Rect(plot.xMin - 34, y - 7, 32, 14),
                Mathf.RoundToInt(y01 * 100f) + "%",
                EditorStyles.miniLabel
            );
        }
    }
}

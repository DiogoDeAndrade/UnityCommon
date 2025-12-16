using UC;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(DistanceRange))]
public class DistanceRangeDrawer : PropertyDrawer
{
    // Small positive value so it is strictly > 0 (not >= 0)
    const float kEpsilon = 0.0f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Prefix label
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        var typeProp = property.FindPropertyRelative("type");
        var minProp = property.FindPropertyRelative("min");
        var maxProp = property.FindPropertyRelative("max");

        float oldMin = minProp.floatValue;
        float oldMax = maxProp.floatValue;

        float spacing = 4f;
        float typeWidth = 90f;
        float miniLabelWidth = 24f;

        float fieldWidth = (position.width - typeWidth - miniLabelWidth * 2 - spacing * 4) * 0.5f;

        Rect typeRect = new Rect(position.x, position.y, typeWidth, position.height);

        Rect minLabelRect = new Rect(typeRect.xMax + spacing, position.y, miniLabelWidth, position.height);
        Rect minRect = new Rect(minLabelRect.xMax + spacing, position.y, fieldWidth, position.height);

        Rect maxLabelRect = new Rect(minRect.xMax + spacing, position.y, miniLabelWidth, position.height);
        Rect maxRect = new Rect(maxLabelRect.xMax + spacing, position.y, fieldWidth, position.height);

        // Type
        EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);

        // Mini-label style (smaller + subtle)
        var miniStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };

        // Drag on labels (like Unity numeric labels)
        float minValue = minProp.floatValue;
        float maxValue = maxProp.floatValue;

        minValue = DragMiniLabelFloat(minLabelRect, "Min", miniStyle, minValue);
        maxValue = DragMiniLabelFloat(maxLabelRect, "Max", miniStyle, maxValue);

        // Normal typing in fields
        EditorGUI.BeginChangeCheck();
        GUI.SetNextControlName("MinMaxDistance_MinField");
        minValue = EditorGUI.FloatField(minRect, minValue);

        GUI.SetNextControlName("MinMaxDistance_MaxField");
        maxValue = EditorGUI.FloatField(maxRect, maxValue);

        bool fieldsChanged = EditorGUI.EndChangeCheck();

        // Apply constraints:
        // 1) strictly > 0
        minValue = Mathf.Max(kEpsilon, minValue);
        maxValue = Mathf.Max(kEpsilon, maxValue);

        // 2) enforce min <= max
        // Decide which one the user likely edited
        bool minChanged = !Mathf.Approximately(minValue, oldMin);
        bool maxChanged = !Mathf.Approximately(maxValue, oldMax);

        if (minValue > maxValue)
        {
            // If user changed min, push max up. Otherwise, pull min down.
            if (minChanged && !maxChanged)
                maxValue = minValue;
            else
                minValue = maxValue;
        }

        // Write back if anything changed (dragging labels counts too)
        if (fieldsChanged || minChanged || maxChanged)
        {
            minProp.floatValue = minValue;
            maxProp.floatValue = maxValue;
        }

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => EditorGUIUtility.singleLineHeight;

    // --- Mini label drag support ---

    static float DragMiniLabelFloat(Rect rect, string text, GUIStyle style, float value)
    {
        EditorGUI.LabelField(rect, text, style);

        // Make it feel like Unity: horizontal drag with left mouse
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.SlideArrow);

        int id = GUIUtility.GetControlID(FocusType.Passive, rect);
        Event e = Event.current;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button == 0 && rect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    // Sensitivity: pixels -> value
                    // Shift = finer, Ctrl/Cmd = coarser (Unity-ish)
                    float sensitivity = 0.02f;
                    if (e.shift) sensitivity *= 0.2f;
                    if (e.control || e.command) sensitivity *= 5f;

                    float delta = e.delta.x * sensitivity;
                    value += delta;

                    GUI.changed = true;
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    e.Use();
                }
                break;
        }

        return value;
    }
}

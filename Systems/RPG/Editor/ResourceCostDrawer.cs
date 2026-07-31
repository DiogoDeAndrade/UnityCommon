using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(ResourceCost))]
    public class ResourceCostDrawer : PropertyDrawer
    {
        const float spacing = 4.0f;
        const float maxCostWidth = 70.0f;

        // An element inside a list has a path ending in ".Array.data[3]". Anchored at the end on
        // purpose: a ResourceCost sitting in a field *of* a list element ("list.Array.data[0].cost")
        // contains the same text but is a named field and still wants its label.
        static readonly Regex arrayElementPath = new Regex(@"\.Array\.data\[\d+\]$", RegexOptions.Compiled);

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);

            SerializedProperty typeProp = property.FindPropertyRelative(nameof(ResourceCost.type));
            SerializedProperty costProp = property.FindPropertyRelative(nameof(ResourceCost.cost));

            if ((typeProp == null) || (costProp == null))
            {
                EditorGUI.LabelField(position, label.text, "Missing type/cost field");
                EditorGUI.EndProperty();
                return;
            }

            // A list element's label is only ever "Element 3", so it's dropped and the two fields
            // get the whole row. A named field keeps its label like any other property.
            Rect fieldRect = (arrayElementPath.IsMatch(property.propertyPath)) ? (position) : (EditorGUI.PrefixLabel(position, label));

            // Nothing left in the row carries a label, so there's nothing for the indent to line up
            // against - without this the fields get pushed right by the surrounding foldout depth.
            // After PrefixLabel, which does its own indent handling and hands back a rect that has
            // already been adjusted.
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // The number is always short, so it takes a fixed slice and the type gets everything
            // else - that's the one that actually becomes unreadable when it's narrow. The
            // proportional cap keeps it sane inside a cramped list or a two column inspector.
            float costWidth = Mathf.Min(maxCostWidth, fieldRect.width * 0.35f);

            Rect typeRect = new Rect(fieldRect.x, fieldRect.y, fieldRect.width - costWidth - spacing, EditorGUIUtility.singleLineHeight);
            Rect costRect = new Rect(typeRect.xMax + spacing, position.y, costWidth, EditorGUIUtility.singleLineHeight);

            // PropertyField rather than ObjectField/FloatField: it keeps drag and drop, the prefab
            // override bar, undo and multi object editing without this drawer having to know the
            // field types at all
            EditorGUI.PropertyField(typeRect, typeProp, GUIContent.none);
            EditorGUI.PropertyField(costRect, costProp, GUIContent.none);

            EditorGUI.indentLevel = indent;

            EditorGUI.EndProperty();
        }
    }
}

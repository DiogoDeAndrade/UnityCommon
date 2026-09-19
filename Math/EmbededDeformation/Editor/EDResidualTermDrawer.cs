using UnityEditor;
using UnityEngine;
using UC.Editor;

#if UC_ENABLE_ED
namespace UC.ED.Editor
{
    /// <summary>
    /// Type picker for the energy model's term list. Reuses the generic managed-reference drawer,
    /// which also routes each term's own fields through NaughtyEditorGUI - so a term's ShowIf and
    /// Min attributes work inside the list.
    ///
    /// A term whose weight is at or under 1e-12 is drawn dimmed, popup and fields alike, so the
    /// active terms of a model read at a glance. The threshold is the epsilon the schedule runner
    /// uses to keep a term's columns in the export while leaving it out of the objective, so
    /// "dimmed" means exactly "not in the objective". The fields stay editable; only the colour
    /// changes, and it is restored after the draw whatever happens inside it.
    /// </summary>
    [CustomPropertyDrawer(typeof(EDResidualTerm), true)]
    public class EDResidualTermDrawer : BaseFunctionDrawer<EDResidualTerm>
    {
        private const float inactiveWeight = 1e-12f;
        private static readonly Color inactiveTint = new Color(0.55f, 0.55f, 0.55f, 1.0f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Color previous = GUI.color;

            if (IsInactive(property)) GUI.color = previous * inactiveTint;

            try
            {
                base.OnGUI(position, property, label);
            }
            finally
            {
                GUI.color = previous;
            }
        }

        private static bool IsInactive(SerializedProperty property)
        {
            if (property.managedReferenceValue == null) return false;

            SerializedProperty weight = property.FindPropertyRelative("weight");

            return (weight != null) && (weight.propertyType == SerializedPropertyType.Float) && (weight.floatValue <= inactiveWeight);
        }
    }
}
#endif

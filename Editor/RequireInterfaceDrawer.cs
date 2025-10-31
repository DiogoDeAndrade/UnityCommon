using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RequireInterfaceAttribute))]
public class RequireInterfaceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var attr = (RequireInterfaceAttribute)attribute;
        EditorGUI.BeginProperty(position, label, property);

        // Draw the regular object field first
        Object newObj = EditorGUI.ObjectField(
            position,
            label,
            property.objectReferenceValue,
            typeof(Object),
            true
        );

        if (newObj != property.objectReferenceValue)
            property.objectReferenceValue = Validate(newObj, attr.RequiredType);

        // If empty, show interface type as right-aligned overlay text
        if (property.objectReferenceValue == null)
        {
            var typeName = attr.RequiredType.Name;

            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Italic,
                normal = { textColor = EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.5f) : new Color(0, 0, 0, 0.5f) }
            };

            // Compute the rect for the overlay text (right-aligned within the field area)
            var fieldRect = position;
            fieldRect.xMin += EditorGUIUtility.labelWidth + 4;
            fieldRect.xMax -= 20;
            GUI.Label(fieldRect, $"<{typeName}>", style);
        }

        EditorGUI.EndProperty();
    }

    private static Object Validate(Object obj, System.Type iface)
    {
        if (obj == null) return null;

        if (obj is GameObject go)
            return go.GetComponent(iface) as Component;

        if (obj is Component c && iface.IsAssignableFrom(c.GetType()))
            return c;

        if (obj is ScriptableObject so && iface.IsAssignableFrom(so.GetType()))
            return so;

        return null;
    }
}
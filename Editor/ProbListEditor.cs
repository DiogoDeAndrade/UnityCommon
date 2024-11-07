using UnityEditor;
using UnityEngine;

public class ProbListPropertyDrawer<T> : PropertyDrawer
{
    private int selectedIndex = -1;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty originalElements = property.FindPropertyRelative("originalElements");
        // Height includes title, boolean field, each element line, and the bottom space for buttons
        return EditorGUIUtility.singleLineHeight * (4 + originalElements.arraySize) + 10; // Additional padding for buttons
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw the title with bold font
        position.height = EditorGUIUtility.singleLineHeight;
        EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
        position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        // Draw the outer outline box for the list, making space for buttons at the bottom
        Rect outlineRect = new Rect(position.x, position.y, position.width, GetPropertyHeight(property, label) - EditorGUIUtility.singleLineHeight);
        EditorGUI.HelpBox(outlineRect, GUIContent.none.text, MessageType.None);

        // Padding for the inner content
        Rect innerRect = new Rect(outlineRect.x + 10, outlineRect.y + 10, outlineRect.width - 20, outlineRect.height - 30);

        // Draw the withReplacement boolean inside the outline
        SerializedProperty withReplacement = property.FindPropertyRelative("withReplacement");
        Rect withReplacementRect = new Rect(innerRect.x, innerRect.y, innerRect.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(withReplacementRect, withReplacement, new GUIContent("With Replacement"));

        innerRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        // Draw each element in the originalElements list
        SerializedProperty originalElements = property.FindPropertyRelative("originalElements");
        float padding = 5f;
        float percentageWidth = 50f;
        float weightWidth = 100f;

        for (int i = 0; i < originalElements.arraySize; i++)
        {
            SerializedProperty element = originalElements.GetArrayElementAtIndex(i);
            SerializedProperty valueProperty = element.FindPropertyRelative("value");
            SerializedProperty weightProperty = element.FindPropertyRelative("weight");

            // Calculate the position for each element row within the innerRect with padding
            Rect elementRect = new Rect(innerRect.x, innerRect.y, innerRect.width, EditorGUIUtility.singleLineHeight);

            // Highlight selected item
            if (i == selectedIndex)
            {
                EditorGUI.DrawRect(elementRect, new Color(0.2f, 0.4f, 0.8f, 0.3f));
            }

            // Define positions for the fields
            float valueWidth = elementRect.width - (percentageWidth + weightWidth + 3 * padding);

            // Draw the value field
            Rect valueRect = new Rect(elementRect.x, elementRect.y, valueWidth, elementRect.height);
            EditorGUI.PropertyField(valueRect, valueProperty, GUIContent.none);

            // Draw the weight field with interactive editing
            Rect weightRect = new Rect(valueRect.x + valueWidth + padding, elementRect.y, weightWidth, elementRect.height);
            EditorGUI.PropertyField(weightRect, weightProperty, GUIContent.none);

            // Display the computed percentage
            float percentage = CalculatePercentage(weightProperty.floatValue, originalElements);
            Rect percentageRect = new Rect(weightRect.x + weightWidth + padding, elementRect.y, percentageWidth, elementRect.height);
            EditorGUI.LabelField(percentageRect, $"{percentage:0.##}%");

            // Detect selection click only outside the value and weight fields
            if (Event.current.type == EventType.MouseDown && elementRect.Contains(Event.current.mousePosition) &&
                !valueRect.Contains(Event.current.mousePosition) && !weightRect.Contains(Event.current.mousePosition))
            {
                selectedIndex = i;
                Event.current.Use();
            }

            innerRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        // Draw the add and remove buttons at the bottom, within the HelpBox, aligned to the right
        float buttonWidth = 20f;
        float buttonPadding = 5f;
        Rect addButtonRect = new Rect(innerRect.x + innerRect.width - 2 * (buttonWidth), innerRect.y + 5, buttonWidth, EditorGUIUtility.singleLineHeight);
        if (GUI.Button(addButtonRect, "+"))
        {
            AddElement(property);
        }

        bool prevEnabled = GUI.enabled;
        Rect removeButtonRect = new Rect(addButtonRect.x + buttonWidth, innerRect.y + 5, buttonWidth, EditorGUIUtility.singleLineHeight);
        GUI.enabled = selectedIndex >= 0;
        if (GUI.Button(removeButtonRect, "-") && selectedIndex >= 0)
        {
            RemoveElement(property, selectedIndex);
            selectedIndex = -1; // Reset selection after deletion
        }
        GUI.enabled = prevEnabled;

        EditorGUI.EndProperty();
    }

    private void AddElement(SerializedProperty property)
    {
        SerializedProperty originalElements = property.FindPropertyRelative("originalElements");
        originalElements.InsertArrayElementAtIndex(originalElements.arraySize);
        SerializedProperty newElement = originalElements.GetArrayElementAtIndex(originalElements.arraySize - 1);

        // Initialize new element with default values
        newElement.FindPropertyRelative("value").objectReferenceValue = null;
        newElement.FindPropertyRelative("weight").floatValue = 1f;
    }

    private void RemoveElement(SerializedProperty property, int index)
    {
        SerializedProperty originalElements = property.FindPropertyRelative("originalElements");
        originalElements.DeleteArrayElementAtIndex(index);
    }
      
    private float CalculatePercentage(float weight, SerializedProperty originalElements)
    {
        float totalWeight = 0f;
        for (int i = 0; i < originalElements.arraySize; i++)
        {
            totalWeight += originalElements.GetArrayElementAtIndex(i).FindPropertyRelative("weight").floatValue;
        }
        return totalWeight > 0 ? (weight / totalWeight) * 100f : 0f;
    }
}

[CustomPropertyDrawer(typeof(AudioClipProbList))]
public class AudioClipProbListDrawer : ProbListPropertyDrawer<AudioClip>
{
   
}

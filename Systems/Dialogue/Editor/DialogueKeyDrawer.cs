using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CustomPropertyDrawer(typeof(DialogueKeyAttribute))]
public class DialogueKeyDrawer : PropertyDrawer
{
    private bool showPopup = false;
    private List<string> filteredKeys = new List<string>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            var keys = GetDialogueKeys();

            // Draw the popup dropdown
            position.width -= 25; // Reduce width to make space for the button
            int selectedIndex = Mathf.Max(0, System.Array.IndexOf(keys, property.stringValue));
            selectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, keys);
            if (selectedIndex >= 0 && selectedIndex < keys.Length)
            {
                property.stringValue = keys[selectedIndex];
            }

            // Draw the button
            var buttonRect = new Rect(position.x + position.width + 5, position.y, 20, EditorGUIUtility.singleLineHeight);
            if (GUI.Button(buttonRect, "..."))
            {
                showPopup = true;
                filteredKeys = new List<string>(keys);
            }

            // Handle the popup window
            if (showPopup)
            {
                ShowPopupWindow(property, keys);
            }
        }
        else
        {
            EditorGUI.PropertyField(position, property, label);
        }
    }

    private void ShowPopupWindow(SerializedProperty property, string[] keys)
    {
        var window = EditorWindow.GetWindow<DialogueKeyPopupWindow>(true, "Select Dialogue Key", true);
        window.Initialize(property, keys);
        showPopup = false;
    }

    private string[] GetDialogueKeys()
    {
        var dialogueDataObjects = AssetUtils.GetAll<DialogueData>();
        var keySet = new HashSet<string>();

        foreach (var dialogueData in dialogueDataObjects)
        {
            foreach (var key in dialogueData.GetKeys())
            {
                keySet.Add(key);
            }
        }

        return keySet.ToArray();
    }
}

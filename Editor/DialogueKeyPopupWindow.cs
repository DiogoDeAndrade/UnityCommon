using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class DialogueKeyPopupWindow : EditorWindow
{
    private SerializedProperty property;
    private string[] keys;
    private string searchString = "";
    private Vector2 scrollPosition;

    public void Initialize(SerializedProperty property, string[] keys)
    {
        this.property = property;
        this.keys = keys;
    }

    private void OnGUI()
    {
        // Search box
        searchString = EditorGUILayout.TextField("Search", searchString);

        // Filtered keys
        List<string> filteredKeys = new List<string>();
        foreach (var key in keys)
        {
            if (string.IsNullOrEmpty(searchString) || key.IndexOf(searchString, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                filteredKeys.Add(key);
            }
        }

        // Scroll view
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        foreach (var key in filteredKeys)
        {
            if (GUILayout.Button(key))
            {
                property.stringValue = key;
                property.serializedObject.ApplyModifiedProperties();
                Close();
            }
        }
        EditorGUILayout.EndScrollView();

        // Close button
        if (GUILayout.Button("Close"))
        {
            Close();
        }
    }
}

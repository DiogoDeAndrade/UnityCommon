using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UC
{

    [CustomPropertyDrawer(typeof(DialogueKeyAttribute))]
    public class DialogueKeyDrawer : PropertyDrawer
    {
        private bool showPopup = false;
        private List<string> filteredKeys = new List<string>();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                var keys = GetDialogueKeys(property);

                // A value that isn't among the keys (the dialogue was swapped, the key renamed) stays
                // visible at the top, marked, rather than being silently replaced by the first key
                var options = keys;
                int selectedIndex = System.Array.IndexOf(keys, property.stringValue);
                bool stale = (selectedIndex < 0) && (!string.IsNullOrEmpty(property.stringValue));
                if (stale)
                {
                    options = new string[keys.Length + 1];
                    options[0] = $"{property.stringValue} (not found)";
                    keys.CopyTo(options, 1);
                    selectedIndex = 0;
                }

                // Draw the popup dropdown - only writes back on an actual pick, so an empty or stale
                // field isn't rewritten just by being looked at
                position.width -= 25; // Reduce width to make space for the button
                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUI.Popup(position, label.text, Mathf.Max(0, selectedIndex), options);
                if (EditorGUI.EndChangeCheck())
                {
                    int keyIndex = (stale) ? (newIndex - 1) : (newIndex);
                    if ((keyIndex >= 0) && (keyIndex < keys.Length))
                    {
                        property.stringValue = keys[keyIndex];
                    }
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

        private string[] GetDialogueKeys(SerializedProperty property)
        {
            // Restricted to one dialogue when the attribute names a field for it
            var dialogueKeyAttribute = attribute as DialogueKeyAttribute;
            if (!string.IsNullOrEmpty(dialogueKeyAttribute?.dialogueField))
            {
                var dialogue = FindSiblingDialogue(property, dialogueKeyAttribute.dialogueField);
                if (dialogue != null)
                {
                    return dialogue.GetKeys().Distinct().ToArray();
                }

                // No dialogue assigned yet - fall through to offering everything, the same as the
                // attribute without an argument
            }

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

        // The named field sits next to the one being drawn, so its path is this property's path with
        // the last element swapped - which also makes it work inside nested classes and list elements
        private static DialogueData FindSiblingDialogue(SerializedProperty property, string fieldName)
        {
            string path = property.propertyPath;
            int lastDot = path.LastIndexOf('.');
            string siblingPath = (lastDot >= 0) ? (path.Substring(0, lastDot + 1) + fieldName) : (fieldName);

            var sibling = property.serializedObject.FindProperty(siblingPath);
            if (sibling == null)
            {
                Debug.LogWarning($"[DialogueKey] {property.serializedObject.targetObject.name}: no field \"{fieldName}\" next to \"{property.name}\"");
                return null;
            }
            if (sibling.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogWarning($"[DialogueKey] {property.serializedObject.targetObject.name}: \"{fieldName}\" is not a DialogueData reference");
                return null;
            }

            return sibling.objectReferenceValue as DialogueData;
        }
    }
}
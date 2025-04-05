using UnityEditor;
using UnityEngine;

namespace UC
{

    [CustomPropertyDrawer(typeof(ParamPrefabListBase), true)]
    public class ParamPrefabListDrawer : PropertyDrawer
    {
        private int selectedIndex = -1;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var itemsProp = property.FindPropertyRelative("items");
            float height = EditorGUIUtility.singleLineHeight + 8f; // label + spacing

            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var prefabProp = element.FindPropertyRelative("prefab");

                // Height for name + prefab field + nested parameter UI
                height += EditorGUI.GetPropertyHeight(prefabProp, GUIContent.none, true) + 6f;
            }

            // Add space for + / - buttons
            height += EditorGUIUtility.singleLineHeight + 6f;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var itemsProp = property.FindPropertyRelative("items");

            float y = position.y;
            float line = EditorGUIUtility.singleLineHeight;
            float spacing = 4f;

            EditorGUI.LabelField(new Rect(position.x, y, position.width, line), label, EditorStyles.boldLabel);
            y += line + spacing;

            GUI.Box(new Rect(position.x, y, position.width, GetPropertyHeight(property, label) - line - spacing), GUIContent.none, EditorStyles.helpBox);

            y += 4f;

            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                var element = itemsProp.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("name");
                var prefabProp = element.FindPropertyRelative("prefab");

                float lineHeight = EditorGUIUtility.singleLineHeight;

                // Highlight selection
                Rect rowRect = new Rect(position.x + 6, y, position.width - 12, lineHeight);
                if (i == selectedIndex)
                {
                    Rect highlightRect = new Rect(position.x + 2, y, position.width - 4, lineHeight);
                    EditorGUI.DrawRect(highlightRect, new Color(0.8f, 0.6f, 0.85f, 0.4f));
                }

                float nameWidth = rowRect.width * 0.25f;
                float prefabWidth = rowRect.width - nameWidth - 4f;

                var nameRect = new Rect(rowRect.x, y, nameWidth, lineHeight);
                var prefabFieldRect = new Rect(rowRect.x + nameWidth + 4f, y, prefabWidth, lineHeight);

                var prefabObject = prefabProp.FindPropertyRelative("prefabObject").objectReferenceValue;

                string displayName = nameProp.stringValue;

                // Case: prefab assigned but name not set → show prefab name
                if (prefabObject != null && string.IsNullOrEmpty(displayName))
                {
                    displayName = prefabObject.name;
                }

                // Disable editing if prefab is null
                bool editable = prefabObject != null;

                EditorGUI.BeginDisabledGroup(!editable);

                EditorGUI.BeginChangeCheck();
                string newName = EditorGUI.TextField(nameRect, displayName);
                if (EditorGUI.EndChangeCheck() && editable)
                {
                    nameProp.stringValue = newName;
                }

                EditorGUI.EndDisabledGroup();
                ParamPrefabDrawer.SuppressOptions = true;
                EditorGUI.PropertyField(prefabFieldRect, prefabProp, GUIContent.none);
                ParamPrefabDrawer.SuppressOptions = false;

                y += lineHeight + spacing;

                // Draw nested ParamPrefab (drawn via existing drawer)
                ParamPrefabDrawer.SuppressPrefabField = true;
                float nestedHeight = EditorGUI.GetPropertyHeight(prefabProp, GUIContent.none, true);
                Rect nestedRect = new Rect(position.x + 12, y, position.width - 24, nestedHeight);
                EditorGUI.PropertyField(nestedRect, prefabProp, GUIContent.none, true);
                ParamPrefabDrawer.SuppressPrefabField = false;

                y += nestedHeight + spacing;

                // Selection logic
                Rect fullRow = new Rect(position.x, y - (nestedHeight + lineHeight + 2 * spacing), position.width, nestedHeight + lineHeight + 2 * spacing);
                if (Event.current.type == EventType.MouseDown && fullRow.Contains(Event.current.mousePosition))
                {
                    selectedIndex = i;
                    Event.current.Use();
                }
            }

            // Draw buttons
            float buttonWidth = 22f;
            Rect addBtn = new Rect(position.x + position.width - 2 * buttonWidth - 4, y, buttonWidth, line);
            Rect removeBtn = new Rect(position.x + position.width - buttonWidth, y, buttonWidth, line);

            if (GUI.Button(addBtn, "+"))
            {
                itemsProp.InsertArrayElementAtIndex(itemsProp.arraySize);
            }

            bool prevEnabled = GUI.enabled;
            GUI.enabled = selectedIndex >= 0;
            if (GUI.Button(removeBtn, "-") && selectedIndex >= 0)
            {
                itemsProp.DeleteArrayElementAtIndex(selectedIndex);
                selectedIndex = -1;
            }
            GUI.enabled = prevEnabled;

            EditorGUI.EndProperty();
        }
    }
}
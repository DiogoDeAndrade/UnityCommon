using UnityEngine;
using UnityEditor;

namespace UC
{
    public abstract class SerializedDictionaryEditor<KEY, VALUE> : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty listProp = property.FindPropertyRelative("_itemList");
            if (listProp == null || !property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            // header + items + add button
            int rows = listProp.arraySize + 2;
            return EditorGUIUtility.singleLineHeight * rows + 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty listProp = property.FindPropertyRelative("_itemList");
            if (listProp == null)
            {
                EditorGUI.LabelField(position, label.text, "No _itemList field found");
                return;
            }

            // Foldout
            Rect row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded) return;

            EditorGUI.indentLevel++;
            float lineH = EditorGUIUtility.singleLineHeight;
            float y = row.y + lineH + 2;

            // Draw elements
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty elemProp = listProp.GetArrayElementAtIndex(i);
                SerializedProperty keyProp = elemProp.FindPropertyRelative("key");
                SerializedProperty valProp = elemProp.FindPropertyRelative("value");

                Rect r = new Rect(position.x, y, position.width, lineH);

                float btnW = 22f;
                float colW = (r.width - btnW - 6f) * 0.5f;

                Rect keyRect = new Rect(r.x, r.y, colW, lineH);
                Rect valRect = new Rect(r.x + colW + 4f, r.y, colW, lineH);
                Rect delRect = new Rect(r.x + 2f * colW + 6f, r.y, btnW, lineH);

                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none, true);
                EditorGUI.PropertyField(valRect, valProp, GUIContent.none, true);

                if (GUI.Button(delRect, "-"))
                {
                    Undo.RecordObject(property.serializedObject.targetObject, "Remove Dictionary Entry");
                    listProp.DeleteArrayElementAtIndex(i);
                    property.serializedObject.ApplyModifiedProperties();
                    // After Apply, Unity will deserialize and rebuild _itemDic via OnAfterDeserialize.
                    EditorGUI.indentLevel--;
                    return;
                }

                y += lineH;
            }

            // Add button
            Rect addRect = new Rect(position.x, y, position.width, lineH);
            if (GUI.Button(addRect, "+ Add"))
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Add Dictionary Entry");

                int idx = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(idx);

                SerializedProperty newElem = listProp.GetArrayElementAtIndex(idx);
                SerializedProperty newKey = newElem.FindPropertyRelative("key");
                SerializedProperty newVal = newElem.FindPropertyRelative("value");

                // Initialize default values for clarity
                SetDefaultValue(newKey, typeof(KEY));
                SetDefaultValue(newVal, typeof(VALUE));

                // Optional: prevent duplicate keys if the key is enum or primitive comparable
                if (typeof(KEY).IsEnum)
                {
                    MakeEnumKeyUnique(listProp, idx);
                }

                property.serializedObject.ApplyModifiedProperties();
                // After Apply, OnAfterDeserialize() rebuilds the mirror dictionary safely.
            }

            EditorGUI.indentLevel--;
        }

        private static void SetDefaultValue(SerializedProperty prop, System.Type type)
        {
            if (type.IsEnum)
            {
                prop.enumValueIndex = 0;
                return;
            }

            if (type == typeof(int)) { prop.intValue = 0; return; }
            if (type == typeof(float)) { prop.floatValue = 0f; return; }
            if (type == typeof(bool)) { prop.boolValue = false; return; }
            if (type == typeof(string)) { prop.stringValue = string.Empty; return; }

            // Unity objects
            if (typeof(Object).IsAssignableFrom(type))
            {
                prop.objectReferenceValue = null;
                return;
            }

            // For structs or other serializable types, leave as default
        }

        // Simple helper to avoid duplicate enum keys (optional).
        private static void MakeEnumKeyUnique(SerializedProperty listProp, int newIndex)
        {
            var newElem = listProp.GetArrayElementAtIndex(newIndex);
            var newKey = newElem.FindPropertyRelative("key");
            int tries = 0;
            int enumCount = newKey.enumDisplayNames.Length;

            while (HasDuplicateEnumKey(listProp, newIndex) && tries < enumCount)
            {
                newKey.enumValueIndex = (newKey.enumValueIndex + 1) % enumCount;
                tries++;
            }
        }

        private static bool HasDuplicateEnumKey(SerializedProperty listProp, int idx)
        {
            var e = listProp.GetArrayElementAtIndex(idx);
            var key = e.FindPropertyRelative("key").enumValueIndex;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                if (i == idx) continue;
                var other = listProp.GetArrayElementAtIndex(i);
                if (other.FindPropertyRelative("key").enumValueIndex == key)
                    return true;
            }
            return false;
        }
    }
}

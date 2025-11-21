// Assets/Editor/BaseGameActionDrawer.cs
using UnityEditor;
using UnityEngine;

namespace UC.Interaction.Editor
{
    /// <summary>
    /// Base drawer for all GameAction drawers.
    /// Provides a right-aligned "wait" checkbox and helpers for line layout.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameAction), true)]
    public class BaseGameActionDrawer : PropertyDrawer
    {
        // Public so editors can align a header column with this width
        public const float WaitColumnWidth = 42f; // fits a toggle & some padding

        protected static Rect NextLine(Rect r)
        {
            r.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return r;
        }

        /// <summary>Split one line into "content (left)" and "wait" (right) columns.</summary>
        protected static void SplitForWait(Rect position, out Rect contentRect, out Rect waitRect)
        {
            var w = WaitColumnWidth;
            waitRect = new Rect(position.xMax - w, position.y, w, position.height);
            contentRect = new Rect(position.x, position.y, position.width - w, position.height);
        }

        /// <summary>Draw the small checkbox (no label) bound to GameAction.wait.</summary>
        protected static void DrawWait(Rect waitRect, SerializedProperty actionProperty)
        {
            var waitProp = actionProperty.FindPropertyRelative("wait");
            if (waitProp == null) return;

            const float size = 16f; // checkbox size
            float x = waitRect.xMax - size - 2f; // align to right edge with a tiny padding
            float y = waitRect.y + (waitRect.height - size) * 0.5f;

            var toggleRect = new Rect(x, y, size, size);
            waitProp.boolValue = EditorGUI.Toggle(toggleRect, waitProp.boolValue);
        }

        protected float GetDefaultHeight(SerializedProperty property, GUIContent label)
        {
            // Height for the foldout line
            float height = EditorGUIUtility.singleLineHeight;

            if (!property.isExpanded)
                return height;

            // Add height for all visible children
            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(iterator, endProperty))
            {
                if (iterator.name == "wait")
                    continue;

                height += EditorGUI.GetPropertyHeight(iterator, true)
                          + EditorGUIUtility.standardVerticalSpacing;

                enterChildren = false;
            }

            return height;
        }

        protected void DefaultDrawer(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // --- HEADER LINE (type name + wait) ---

            // Figure out a nice type name from the managed reference
            string typeName = GetTypeNameFromManagedReference(property);
            string header = string.IsNullOrEmpty(typeName)
                ? label.text
                : ObjectNames.NicifyVariableName(typeName);

            // One line for the header
            Rect headerRect = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight
            );

            // Split for content vs wait
            SplitForWait(headerRect, out var contentRect, out var waitRect);

            // Respect indentation for the left content, but keep wait anchored
            contentRect = EditorGUI.IndentedRect(contentRect);

            EditorGUI.LabelField(contentRect, header);
            if (ShouldShowWait(property))
                DrawWait(waitRect, property);

            // --- CHILD FIELDS ---

            EditorGUI.indentLevel++;

            float y = position.y + EditorGUIUtility.singleLineHeight
                      + EditorGUIUtility.standardVerticalSpacing;

            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) &&
                   !SerializedProperty.EqualContents(iterator, endProperty))
            {
                enterChildren = false;

                if (iterator.name == "wait")
                    continue;

                float h = EditorGUI.GetPropertyHeight(iterator, true);

                Rect fieldRect = new Rect(
                    position.x,
                    y,
                    position.width,
                    h
                );

                fieldRect = EditorGUI.IndentedRect(fieldRect);
                EditorGUI.PropertyField(fieldRect, iterator, true);

                y += h + EditorGUIUtility.standardVerticalSpacing;
            }

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        private static string GetTypeNameFromManagedReference(SerializedProperty property)
        {
            // Example: "Assembly-CSharp UC.RPG.Actions.GameAction_PickupItem"
            string full = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full))
                return null;

            int spaceIdx = full.LastIndexOf(' ');
            if (spaceIdx >= 0 && spaceIdx + 1 < full.Length)
                full = full.Substring(spaceIdx + 1); // "UC.RPG.Actions.GameAction_PickupItem"

            int dotIdx = full.LastIndexOf('.');
            string shortName = (dotIdx >= 0 && dotIdx + 1 < full.Length)
                ? full.Substring(dotIdx + 1)
                : full;

            return CleanTypeName(shortName);
        }

        private bool ShouldShowWait(SerializedProperty property)
        {
            if (property.managedReferenceValue == null)
                return false;

            var obj = property.managedReferenceValue;

            if (obj is GameAction action)
                return action.NeedWait();

            return false;
        }

        private static string CleanTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return string.Empty;

            // Remove known prefixes
            const string actPrefix = "GameAction_";
            const string condPrefix = "Condition_";

            if (typeName.StartsWith(actPrefix))
                typeName = typeName.Substring(actPrefix.Length);
            else if (typeName.StartsWith(condPrefix))
                typeName = typeName.Substring(condPrefix.Length);

            // Replace underscores with spaces
            typeName = typeName.Replace("_", " ");

            // Nicify for safety
            return ObjectNames.NicifyVariableName(typeName);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GetDefaultHeight(property, label);
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DefaultDrawer(position, property, label);
        }
    }
}

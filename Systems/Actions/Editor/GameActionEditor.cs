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
    }
}

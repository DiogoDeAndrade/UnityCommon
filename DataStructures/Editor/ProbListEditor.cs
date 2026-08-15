using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace UC.Editor
{
    public abstract class BaseProbListPropertyDrawer<T> : PropertyDrawer
    {
        private int selectedIndex = -1;

        const float itemPaddingTop = 4f;
        const float itemPaddingBottom = 4f;
        const float bodyLeftPadding = 12f;

        protected abstract float GetValueHeaderHeight(SerializedProperty valueProperty);
        protected virtual float GetValueBodyHeight(SerializedProperty valueProperty) => 0f;

        protected abstract void DrawValueHeader(Rect position, SerializedProperty valueProperty);
        protected virtual void DrawValueBody(Rect position, SerializedProperty valueProperty) { }

        protected abstract void InitializeValue(SerializedProperty valueProperty);

        protected virtual bool HasReplacementOption
        {
            get
            {
                var attr = fieldInfo?.GetCustomAttribute<ProbListReplacementOptionAttribute>();
                return attr?.Enabled ?? true;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var originalElements = property.FindPropertyRelative("originalElements");

            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // Title + box padding + withReplacement + buttons/padding.
            float height = line + spacing +     // title
                           10f +                // top box padding
                           ((HasReplacementOption) ? (line + spacing) : (0.0f)) +     // withReplacement
                           5f + line + 10f;     // buttons + bottom padding

            for (int i = 0; i < originalElements.arraySize; i++)
            {
                var element = originalElements.GetArrayElementAtIndex(i);
                var value = element.FindPropertyRelative("value");

                float header = Mathf.Max(line, GetValueHeaderHeight(value));
                float body = GetValueBodyHeight(value);

                height += itemPaddingTop;
                height += header;

                if (body > 0f)
                    height += spacing + body;

                height += itemPaddingBottom;
                height += spacing;
            }

            return height;
        }

        GUIStyle weightLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight
        };

        GUIStyle percentStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Respect whatever indentation the parent property has.
            position = EditorGUI.IndentedRect(position);

            int oldIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Draw the title with bold font
            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(position, label, EditorStyles.boldLabel);
            position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Draw the outer outline box for the list, making space for buttons at the bottom
            Rect outlineRect = new Rect(position.x, position.y, position.width, GetPropertyHeight(property, label) - EditorGUIUtility.singleLineHeight - 2);
            EditorGUI.HelpBox(outlineRect, GUIContent.none.text, MessageType.None);

            // Padding for the inner content
            Rect innerRect = new Rect(outlineRect.x + 10, outlineRect.y + 10, outlineRect.width - 20, outlineRect.height - 30);

            // Draw the withReplacement boolean inside the outline
            SerializedProperty withReplacement = property.FindPropertyRelative("withReplacement");

            if (withReplacement != null)
            {
                if (HasReplacementOption)
                {
                    Rect withReplacementRect = new Rect(innerRect.x, innerRect.y, innerRect.width, EditorGUIUtility.singleLineHeight);

                    EditorGUI.PropertyField(withReplacementRect, withReplacement, new GUIContent("With Replacement"));

                    innerRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                }
                else
                {
                    // Hidden option always behaves as "with replacement".
                    withReplacement.boolValue = true;
                }
            }

            // Draw each element in the originalElements list
            SerializedProperty originalElements = property.FindPropertyRelative("originalElements");
            float valueGap = 0f;
            float weightGap = 0;
            float weightLabelWidth = 40f;
            float weightWidth = 68f;
            float percentWidth = 42f;

            for (int i = 0; i < originalElements.arraySize; i++)
            {
                SerializedProperty element = originalElements.GetArrayElementAtIndex(i);
                SerializedProperty valueProperty = element.FindPropertyRelative("value");
                SerializedProperty weightProperty = element.FindPropertyRelative("weight");
                SerializedProperty quantityProperty = element.FindPropertyRelative("quantity");

                float headerHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, GetValueHeaderHeight(valueProperty));
                float bodyHeight = GetValueBodyHeight(valueProperty);
                float elementHeight = itemPaddingTop + headerHeight + ((bodyHeight > 0f) ? (EditorGUIUtility.standardVerticalSpacing + bodyHeight) : 0f) + itemPaddingBottom;

                Rect elementRect = new Rect(innerRect.x, innerRect.y, innerRect.width, elementHeight);

                // Alternate row tint

                Rect elementRectWithPad = new Rect(elementRect.x - 2, elementRect.y, elementRect.width + 4, elementRect.height);
                if (i == selectedIndex)
                {
                    EditorGUI.DrawRect(elementRectWithPad, new Color(0.2f, 0.4f, 0.8f, 0.30f));
                }
                else if ((i & 1) == 1)
                {
                    EditorGUI.DrawRect(elementRectWithPad, new Color(1f, 1f, 1f, 0.05f));
                }

                float rightWidth = weightLabelWidth + weightGap + weightWidth + 1f + percentWidth;
                float valueHeaderWidth = Mathf.Max(60f, elementRect.width - rightWidth);

                float contentY = elementRect.y + itemPaddingTop;
                Rect valueHeaderRect = new Rect(elementRect.x, contentY, valueHeaderWidth, headerHeight);
                Rect weightLabelRect = new Rect(valueHeaderRect.xMax + weightGap, contentY, weightLabelWidth, EditorGUIUtility.singleLineHeight);
                Rect weightRect = new Rect(weightLabelRect.xMax + valueGap, contentY, weightWidth, EditorGUIUtility.singleLineHeight);
                Rect percentRect = new Rect(weightRect.xMax + 1f, contentY, percentWidth, EditorGUIUtility.singleLineHeight);

                // Top row
                DrawValueHeader(valueHeaderRect, valueProperty);

                if (weightProperty != null)
                {
                    GUI.Label(weightLabelRect, "wgt:", weightLabelStyle);
                    EditorGUI.PropertyField(weightRect, weightProperty, GUIContent.none);

                float percentage = CalculatePercentage(weightProperty.floatValue, originalElements);
                GUI.Label(percentRect, $"({percentage:0.#}%)", percentStyle);
                }
                else if (quantityProperty != null)
                {
                    GUI.Label(weightLabelRect, "qt:", weightLabelStyle);
                    EditorGUI.PropertyField(weightRect, quantityProperty, GUIContent.none);
                }

                // Children below, full width
                if (bodyHeight > 0f)
                {
                    Rect bodyRect = new Rect(elementRect.x + bodyLeftPadding, contentY + headerHeight + EditorGUIUtility.standardVerticalSpacing, elementRect.width - bodyLeftPadding, bodyHeight);

                    DrawValueBody(bodyRect, valueProperty);
                }

                // Selection click
                if ((Event.current.type == EventType.MouseDown) && (elementRect.Contains(Event.current.mousePosition)) && (!valueHeaderRect.Contains(Event.current.mousePosition)) && (!weightRect.Contains(Event.current.mousePosition)))
                {
                    selectedIndex = i;
                    Event.current.Use();
                }

                innerRect.y += elementHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            // Draw the add and remove buttons at the bottom, within the HelpBox, aligned to the right
            float buttonWidth = 20f;
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

            EditorGUI.indentLevel = oldIndent;

            EditorGUI.EndProperty();
        }

        private void AddElement(SerializedProperty property)
        {
            SerializedProperty originalElements = property.FindPropertyRelative("originalElements");

            originalElements.InsertArrayElementAtIndex(originalElements.arraySize);

            SerializedProperty newElement = originalElements.GetArrayElementAtIndex(originalElements.arraySize - 1);

            SerializedProperty valueProperty = newElement.FindPropertyRelative("value");

            InitializeValue(valueProperty);

            var propNumeric = newElement.FindPropertyRelative("weight");
            if (propNumeric == null) propNumeric = newElement.FindPropertyRelative("quantity");
            propNumeric.floatValue = 1f;
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

    public class ProbListPropertyDrawer<T> : BaseProbListPropertyDrawer<T>
    {
        protected override float GetValueHeaderHeight(SerializedProperty valueProperty)
        {
            return EditorGUI.GetPropertyHeight(valueProperty, GUIContent.none, true);
        }

        protected override void DrawValueHeader(Rect position, SerializedProperty valueProperty)
        {
            EditorGUI.PropertyField(position, valueProperty, GUIContent.none, true);
        }

        protected override void InitializeValue(SerializedProperty valueProperty)
        {
            switch (valueProperty.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    valueProperty.objectReferenceValue = null;
                    break;

                case SerializedPropertyType.String:
                    valueProperty.stringValue = "";
                    break;
            }
        }
    }

    public class ReferenceProbListPropertyDrawer<T> : BaseProbListPropertyDrawer<T>
    {
        protected override float GetValueHeaderHeight(SerializedProperty valueProperty)
        {
            return BaseFunctionDrawer<T>.GetReferenceHeaderHeight(valueProperty);
        }

        protected override float GetValueBodyHeight(SerializedProperty valueProperty)
        {
            return BaseFunctionDrawer<T>.GetReferenceChildrenHeight(valueProperty);
        }

        protected override void DrawValueHeader(Rect position, SerializedProperty valueProperty)
        {
            BaseFunctionDrawer<T>.DrawReferenceHeader(position, valueProperty, GUIContent.none, inline: true);
        }

        protected override void DrawValueBody(Rect position, SerializedProperty valueProperty)
        {
            BaseFunctionDrawer<T>.DrawReferenceChildren(position, valueProperty);
        }

        protected override void InitializeValue(SerializedProperty valueProperty)
        {
            valueProperty.managedReferenceValue = null;
        }
    }

    [CustomPropertyDrawer(typeof(AudioClipProbList))]
    public class AudioClipProbListDrawer : ProbListPropertyDrawer<AudioClip>
    {

    }

    [CustomPropertyDrawer(typeof(StringProbList))]
    public class StringProbListDrawer : ProbListPropertyDrawer<string>
    {

    }

    [CustomPropertyDrawer(typeof(AnimationClipProbList))]
    public class AnimationClipProbListDrawer : ProbListPropertyDrawer<AnimationClip>
    {

    }

}
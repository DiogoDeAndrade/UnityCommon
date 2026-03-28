using UnityEngine;
using UnityEditor;
using UC.Editor;

namespace UC.RPG.Editor
{

    [CustomEditor(typeof(ResourceHandler))]
    public class ResourceHandlerEditor : UnityCommonEditor
    {
        private SerializedProperty typeProperty;
        private SerializedProperty overrideModeProperty;
        private SerializedProperty initialValueProperty;
        private SerializedProperty overrideMaxValueProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            typeProperty = serializedObject.FindProperty("type");  
            overrideModeProperty = serializedObject.FindProperty("overrideMode");  
            initialValueProperty = serializedObject.FindProperty("initialValue");
            overrideMaxValueProperty = serializedObject.FindProperty("overrideMaxValue");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            ResourceHandler handler = (ResourceHandler)target;

            if (WriteTitle())
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(typeProperty);

                EditorGUILayout.Space(10);

                if ((handler != null) && (handler.type != null))
                {
                    // Progress Bar for resource
                    /*float normalizedValue = handler.normalizedResource;
                    EditorGUILayout.LabelField("Resource Level", EditorStyles.boldLabel);
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), normalizedValue, $"{handler.resource}/{handler.maxValue}");*/

                    // Editable resource value (slider)
                    float newResource = EditorGUILayout.Slider(handler.type.displayName, handler.resource, 0, handler.maxValue);

                    if (newResource != handler.resource)
                    {
                        Undo.RecordObject(handler, "Change Resource Value");
                        handler.SetResource(newResource);
                        EditorUtility.SetDirty(handler);
                    }

                    if (!handler.fromInstance)
                    {
                        EditorGUILayout.PropertyField(overrideModeProperty);
                        var overrideMode = (ResourceHandler.OverrideMode)overrideModeProperty.enumValueFlag;
                        if ((overrideMode & ResourceHandler.OverrideMode.InitialValue) != 0)
                            EditorGUILayout.PropertyField(initialValueProperty);
                        if ((overrideMode & ResourceHandler.OverrideMode.MaxValue) != 0)
                            EditorGUILayout.PropertyField(overrideMaxValueProperty);
                    }

                    // Progress Bar Section
                    DrawProgressBar(handler);

                    EditorGUILayout.Space(10);
                }

                EditorGUI.EndChangeCheck();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProgressBar(ResourceHandler handler)
        {
            float normalizedValue = handler.normalizedResource;
            string progressText = $"{handler.resource}/{handler.maxValue}";

            // Define color based on resource level
            Color barColor = handler.type.displayBarColor;

            // Progress bar background
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f, 1.0f)); // Dark gray background

            // Progress bar foreground
            Rect fillRect = new Rect(rect.x, rect.y, rect.width * normalizedValue, rect.height);
            EditorGUI.DrawRect(fillRect, barColor);

            // Draw text
            GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.black;
            EditorGUI.LabelField(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), progressText, style);
            style.normal.textColor = Color.white;
            EditorGUI.LabelField(rect, progressText, style);
        }

        protected override GUIStyle GetTitleSyle()
        {
            return GUIUtils.GetBehaviourTitleStyle();
        }

        protected override string GetTitle()
        {
            ResourceHandler handler = (ResourceHandler)target;
            if ((handler != null) && (handler.type)) return handler.type.displayName;
            return "Resource Handler";
        }

        protected override (Texture2D, Rect) GetIcon()
        {
            ResourceHandler handler = (ResourceHandler)target;

            if ((handler != null) && (handler.type) && (handler.type.displaySprite))
            {
                Vector2 s = handler.type.displaySprite.texture.texelSize;
                Rect uv = handler.type.displaySprite.rect;
                uv.x *= s.x; uv.y *= s.y;
                uv.width *= s.x; uv.height *= s.y;

                return (handler.type.displaySprite.texture, uv);
            }
            var varTexture = GUIUtils.GetTexture("Resource");
            return (varTexture, new Rect(0.0f, 0.0f, 1.0f, 1.0f));
        }

        protected override (Color, Color, Color) GetColors()
        {
            var backColor = GUIUtils.ColorFromHex("#D0FFFF");
            ResourceHandler handler = (ResourceHandler)target;
            if ((handler != null) && (handler.type)) backColor = handler.type.displayBarColor;

            var textColor = GUIUtils.ColorFromHex("#2f4858");
            if ((handler != null) && (handler.type)) textColor = handler.type.displayTextColor;

            return (backColor, textColor, GUIUtils.ColorFromHex("#FFFFFF"));
        }

        protected override bool HasTitleShadow()
        {
            return true;
        }
    }
}
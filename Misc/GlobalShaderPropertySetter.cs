using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NaughtyAttributes;
using NUnit.Framework.Internal;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class GlobalShaderPropertySetter : Singleton<GlobalShaderPropertySetter>
    {
        public enum GlobalShaderPropertyType
        {
            Float,
            Integer,
            Color,
            Vector,
            Texture
        }

        [Serializable]
        public sealed class GlobalShaderProperty
        {
            [SerializeField] private bool enabled = true;

            [Tooltip("Shader property reference name, e.g. _GlobalTint, _GlobalMask, _WindStrength")]
            [SerializeField] private string propertyName = "_MyGlobalProperty";

            public GlobalShaderPropertyType type = GlobalShaderPropertyType.Float;

            // [AllowNesting] is required for the ShowIf to work here: this class is a nested
            // [Serializable] inside a List, and NaughtyAttributes only auto-applies meta attributes
            // (ShowIf/HideIf/...) to the inspected object's top-level fields. AllowNesting gives the
            // field a real property drawer (PropertyDrawerBase) that re-evaluates ShowIf visibility.
            [SerializeField, ShowIf(nameof(isFloat)), AllowNesting] private float floatValue;
            [SerializeField, ShowIf(nameof(isInteger)), AllowNesting] private int integerValue;
            [SerializeField, ShowIf(nameof(isColor)), AllowNesting] private Color colorValue = Color.white;
            [SerializeField, ShowIf(nameof(isVector)), AllowNesting] private Vector4 vectorValue;
            [SerializeField, ShowIf(nameof(isTexture)), AllowNesting] private Texture textureValue;

            [Tooltip("Only used when Texture is a RenderTexture and you want Color, Depth, or Stencil data.")]
            [SerializeField, ShowIf(nameof(isTexture)), AllowNesting] private bool useRenderTextureSubElement;

            [SerializeField, ShowIf(nameof(isRenderTexture)), AllowNesting] private RenderTextureSubElement renderTextureSubElement = RenderTextureSubElement.Color;

            [NonSerialized] private string cachedPropertyName;
            [NonSerialized] private int cachedPropertyId;

            public bool isFloat => type == GlobalShaderPropertyType.Float;
            public bool isInteger => type == GlobalShaderPropertyType.Integer;
            public bool isColor => type == GlobalShaderPropertyType.Color;
            public bool isVector => type == GlobalShaderPropertyType.Vector;
            public bool isTexture => type == GlobalShaderPropertyType.Texture;
            public bool isRenderTexture => isTexture && useRenderTextureSubElement && (textureValue is RenderTexture);

            public string name => propertyName;

            public int PropertyId
            {
                get
                {
                    if (cachedPropertyName != propertyName)
                    {
                        cachedPropertyName = propertyName;
                        cachedPropertyId = Shader.PropertyToID(propertyName);
                    }

                    return cachedPropertyId;
                }
            }

            public void Apply()
            {
                if ((!enabled) || (string.IsNullOrWhiteSpace(propertyName)))
                    return;

                int id = PropertyId;

                switch (type)
                {
                    case GlobalShaderPropertyType.Float:
                        Shader.SetGlobalFloat(id, floatValue);
                        break;

                    case GlobalShaderPropertyType.Integer:
                        SetGlobalIntegerCompat(id, integerValue);
                        break;

                    case GlobalShaderPropertyType.Color:
                        Shader.SetGlobalColor(id, colorValue);
                        break;

                    case GlobalShaderPropertyType.Vector:
                        Shader.SetGlobalVector(id, vectorValue);
                        break;

                    case GlobalShaderPropertyType.Texture:
                        ApplyTexture(id);
                        break;
                }
            }

            private void ApplyTexture(int id)
            {
                if (useRenderTextureSubElement && textureValue is RenderTexture renderTexture)
                {
                    Shader.SetGlobalTexture(id, renderTexture, renderTextureSubElement);
                }
                else
                {
                    Shader.SetGlobalTexture(id, textureValue);
                }
            }

            private static void SetGlobalIntegerCompat(int id, int value)
            {
#if UNITY_2022_1_OR_NEWER
                Shader.SetGlobalInteger(id, value);
#else
#pragma warning disable 0618
            Shader.SetGlobalInt(id, value);
#pragma warning restore 0618
#endif
            }

            public void SetColor(Color c) { colorValue = c; Apply(); }
            public void SetFloat(float v) { floatValue = v; Apply(); }
        }

        [Header("Apply Timing")]

        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool applyOnValidate = true;

        [Tooltip("Usually off. Enable only if values are being changed continuously in edit mode or play mode.")]
        [SerializeField] private bool applyEveryUpdate;

#if UNITY_EDITOR
        [Tooltip("Useful in edit mode so Scene View updates after changing values in the Inspector.")]
        [SerializeField] private bool repaintSceneViewInEditor = true;
#endif

        [Header("Global Shader Properties")]

        [SerializeField] private List<GlobalShaderProperty> properties = new();

        private void OnEnable()
        {
            if (applyOnEnable)
                ApplyAll();
        }

        private void OnValidate()
        {
            if (applyOnValidate)
                ApplyAll();
        }

        private void Update()
        {
            if (applyEveryUpdate)
                ApplyAll();
        }

        [Button("Apply Shader Globals Now")]
        public void ApplyAll()
        {
            for (int i = 0; i < properties.Count; i++)
            {
                properties[i]?.Apply();
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && repaintSceneViewInEditor)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                SceneView.RepaintAll();
            }
#endif
        }

        public void SetColor(string name, Color color)
        {
            int propId = Shader.PropertyToID(name);

            foreach (var prop in properties)
            {
                if ((prop.PropertyId == propId) && (prop.isColor))
                {
                    prop.SetColor(color);

                    return;
                }
            }
        }

        public void SetFloat(string name, float v)
        {
            int propId = Shader.PropertyToID(name);

            foreach (var prop in properties)
            {
                if ((prop.PropertyId == propId) && (prop.isFloat))
                {
                    prop.SetFloat(v);

                    return;
                }
            }
        }
    }
}

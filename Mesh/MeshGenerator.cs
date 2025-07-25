using NaughtyAttributes;
using System;
using UnityEngine;

namespace UC
{
    [ExecuteInEditMode]
    public class MeshGenerator : MonoBehaviour
    {
        public enum ColorMode { Single, Outer, OuterAndColor, OuterAndInner, GradientX, GradientY };

        [SerializeField]
        protected ColorSpace colorSpace = ColorSpace.Linear;
        [SerializeField, Header("Generic Generator"), ValidateInput(nameof(colorModeSupported))]
        protected ColorMode colorMode = ColorMode.Single;
        [SerializeField, Range(-1.0f, 1.0f), ShowIf(nameof(needColorOffset))]
        protected float colorOffset;
        [SerializeField, ShowIf(nameof(needsColor))]
        protected Color color = Color.white;
        [SerializeField, ShowIf(nameof(needInnerGradient))]
        protected Gradient innerColorOverCircle;
        [SerializeField, ShowIf(nameof(needOuterGradient))]
        protected Gradient outerColorOverCircle;
        [SerializeField, ShowIf(nameof(needGradient))]
        protected Gradient gradient;
        [SerializeField]
        protected Material material;
        [SerializeField]
        protected bool dynamicUpdate = false;

        bool dirty = false;

        bool needColorOffset => (colorMode == ColorMode.Outer) || (colorMode == ColorMode.OuterAndColor) || (colorMode == ColorMode.OuterAndInner);
        bool needsColor => (colorMode == ColorMode.Single) || (colorMode == ColorMode.OuterAndColor);
        bool needOuterGradient => (colorMode == ColorMode.Outer) || (colorMode == ColorMode.OuterAndColor) || (colorMode == ColorMode.OuterAndInner);
        bool needInnerGradient => (colorMode == ColorMode.OuterAndInner);
        bool needGradient => (colorMode == ColorMode.GradientX) || (colorMode == ColorMode.GradientY);

        protected virtual bool colorModeSupported(ColorMode colorMode)
        {
            return true;
        }


        void Start()
        {
            Build();
        }

#if UNITY_EDITOR
        private void Update()
        {
            if ((dirty) && (dynamicUpdate))
            {
                ForceUpdate();
            }
        }
#endif

        [Button("Build")]
        protected void ForceUpdate()
        {
            Build();
            dirty = false;
        }

        protected virtual void Build()
        {
            throw new NotImplementedException();
        }

        private void OnValidate()
        {
            if (dynamicUpdate)
            {
                dirty = true;
            }
        }//*/
    }
}
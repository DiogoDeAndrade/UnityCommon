using System;
using NaughtyAttributes;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// How a point picks the deformation nodes that move it, and how much each of them counts.
    ///
    /// Not polymorphic on purpose. Only the visibility of the fields changes between the weighting
    /// schemes, not the shape of the algorithm - GetNearestK_Generic already handles all four in one
    /// function - so four subclasses would buy nothing but indirection.
    /// </summary>
    [Serializable]
    public class EDBindingConfig
    {
        [SerializeField]
        private BindingSelectionMode    mode = BindingSelectionMode.ClosestOne;
        [SerializeField, Min(2), ShowIf(nameof(isNearestK))]
        private int                     k = 4;
        [SerializeField]
        private BindingWeightMode       weights = BindingWeightMode.Uniform;
        [SerializeField, Min(0.1f), ShowIf(nameof(isInversePower)), Label("Attenuation Smoothness")]
        private float                   power = 2.0f;
        [SerializeField, Min(0.1f), ShowIf(nameof(isGaussian)), Label("Attenuation Sigma (x sample distance)")]
        private float                   sigmaScale = 1.0f;

        private bool isNearestK => (mode == BindingSelectionMode.NearestK);
        private bool isInversePower => (isNearestK) && (weights == BindingWeightMode.InversePower);
        private bool isGaussian => (isNearestK) && (weights == BindingWeightMode.Gaussian);

        public BindingSelectionMode selectionMode => mode;
        public BindingWeightMode weightMode => weights;
        public int nearestK => k;
        public float attenuationPower => power;

        /// <summary>
        /// Sigma is expressed relative to the node spacing, so a graph sampled more finely keeps
        /// the same falloff shape. The caller used to perform this multiplication itself, which
        /// left the stored value meaningless on its own.
        /// </summary>
        public float ResolveSigma(float sampleMinDistance) => sigmaScale * sampleMinDistance;

        public float rawSigmaScale => sigmaScale;
    }
}
#endif

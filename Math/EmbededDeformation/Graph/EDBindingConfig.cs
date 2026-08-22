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
    ///
    /// **The three conditional fields carry no [Min], and that is not an oversight.** This is a
    /// nested serialized class, so their ShowIf needs [AllowNesting] to be evaluated at all - and
    /// once it is, a built-in Unity drawer attribute alongside it owns the draw while NaughtyAttributes
    /// owns the height, so a hidden row reserves no space and is painted over the field below it.
    /// The floors live in the accessors instead, which is the better place regardless: an inspector
    /// attribute only ever clamps a value someone typed, and these are also read from assets and set
    /// from code. See the header of EDDebugSettings, where the same pairing was diagnosed.
    /// </summary>
    [Serializable]
    public class EDBindingConfig
    {
        [SerializeField]
        private BindingSelectionMode    mode = BindingSelectionMode.ClosestOne;
        [SerializeField, ShowIf(nameof(isNearestK)), AllowNesting]
        private int                     k = 4;
        [SerializeField]
        private BindingWeightMode       weights = BindingWeightMode.Uniform;
        [SerializeField, ShowIf(nameof(isInversePower)), Label("Attenuation Smoothness"), AllowNesting]
        private float                   power = 2.0f;
        [SerializeField, ShowIf(nameof(isGaussian)), Label("Attenuation Sigma (x sample distance)"), AllowNesting]
        private float                   sigmaScale = 1.0f;

        private bool isNearestK => (mode == BindingSelectionMode.NearestK);
        private bool isInversePower => (isNearestK) && (weights == BindingWeightMode.InversePower);
        private bool isGaussian => (isNearestK) && (weights == BindingWeightMode.Gaussian);

        public BindingSelectionMode selectionMode => mode;
        public BindingWeightMode weightMode => weights;

        /// <summary>
        /// At least two, because "the nearest k" with k below two is not a blend and the weighting
        /// modes below all divide by a k-th distance.
        /// </summary>
        public int nearestK => Mathf.Max(2, k);

        /// <summary>
        /// Kept away from zero, where the inverse power stops attenuating at all and every node in
        /// range would count equally however far away it was.
        /// </summary>
        public float attenuationPower => Mathf.Max(0.1f, power);

        /// <summary>
        /// Sigma is expressed relative to the node spacing, so a graph sampled more finely keeps
        /// the same falloff shape. The caller used to perform this multiplication itself, which
        /// left the stored value meaningless on its own.
        /// </summary>
        public float ResolveSigma(float sampleMinDistance) => rawSigmaScale * sampleMinDistance;

        /// <summary>
        /// The multiplier by itself, for the golden dump.
        ///
        /// Clamped, and it has to be the same clamp ResolveSigma applies - a dump reporting the
        /// stored value while the solve used a floored one would be a diagnostic describing a
        /// quantity nothing computed with, which this codebase has been caught by before.
        /// </summary>
        public float rawSigmaScale => Mathf.Max(0.1f, sigmaScale);
    }
}
#endif

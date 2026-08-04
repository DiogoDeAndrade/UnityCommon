using System;
using UnityEngine;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The determinant term, measured against the scaling a terminal is allowed to have.
    ///
    /// A structure graph deliberately lets a terminal node scale along its right axis - that is what
    /// widens or narrows a connector, and EDRotationTermStructure already drops its right-axis length
    /// row to permit it. The plain determinant term does not know that, and would read a connector
    /// narrowing to a third of its width as a node two thirds of the way to collapse.
    ///
    /// Scaling the right axis by s multiplies the determinant by exactly s, so dividing by the
    /// terminal's target scale puts a correctly-scaled node back at 1 and leaves the floor measuring
    /// only what nobody sanctioned - collapse along up or forward, and the sign. Node 15 of the
    /// structure baselines is the case this has to keep catching: two axes untouched at 1.00 and one
    /// crushed to 0.224 along *forward*, which is corridor length rather than connector width.
    ///
    /// Nodes without a terminal scale constraint fall through to a reference of 1, so this behaves
    /// exactly like the base term everywhere else in the graph.
    /// </summary>
    [Serializable]
    [PolymorphicName("Determinant (Structure)")]
    public class EDDeterminantTermStructure : EDDeterminantTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new StructureDeterminantInstance(this, deformation);

        public class StructureDeterminantInstance : DeterminantInstance
        {
            public StructureDeterminantInstance(EDDeterminantTermStructure term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            protected override double ReferenceScale(int nodeIndex)
            {
                if (!deformation.HasTerminalScaleConstraint(nodeIndex))
                    return 1.0;

                if (!deformation.TryGetTerminalTargetFrame(nodeIndex, out _, out float targetScale))
                    return 1.0;

                // A target at or below zero is not a scale anyone asked for, and dividing by it would
                // turn the guard into noise. Fall back to measuring the determinant directly, which
                // is the stricter reading of the two.
                return (targetScale > 1e-4f) ? (targetScale) : (1.0);
            }
        }
#endif
    }
}
#endif

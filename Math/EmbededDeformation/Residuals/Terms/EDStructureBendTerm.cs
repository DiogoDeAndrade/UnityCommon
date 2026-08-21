using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Holds each node's rest bend - the angle between the structure links meeting there - so the
    /// skeleton keeps its shape rather than folding flat or splaying open as the piece deforms.
    ///
    /// Two rows per constraint, a cosine and a sine of the angle error rather than the angle itself.
    /// That is what makes it well behaved all the way round: an angle residual jumps when it wraps,
    /// and the pair does not.
    ///
    /// **It preserves shape, not orientation, and the two are worth keeping apart.** The residual is
    /// invariant under a global rigid rotation: positions rotate together so `dot(dA, dB)` does not
    /// move, and the node's up rotates with them so `dot(up, cross(dA, dB))` does not either, since
    /// `cross(R a, R b) = R cross(a, b)` for any R in SO(3). A rotated structure scores exactly zero
    /// here. Whatever aligns a node's frame with the world, or with its neighbour's, it is not this -
    /// see `EDOrientationTerm`, which is the term that word belongs to. Named `linkAngle` until
    /// 2026-08-21, when it turned out to be answering a different question from the one its name
    /// suggested.
    ///
    /// **It is not a junction-only term either.** `Reset` emits one constraint per unordered *pair*
    /// of neighbours at every node of degree two or more, so an ordinary corridor node gets exactly
    /// one and a degree-three junction gets three. Only terminals, having one link, get none. On the
    /// structure graph the goldens carry, that is 29 constraints over 30 nodes: 26 corridor nodes
    /// with one each, one junction with three, three terminals with none.
    ///
    /// **Where it is weakest is where most of the graph lives.** 22 of those 29 rest angles are
    /// straight-through, at ~180 degrees, and both rows go soft there. The cosine is stationary in
    /// the bend angle - d(cos)/d(theta) = -sin(theta), which vanishes at 180 - so it only sees
    /// bending quadratically. The sine row is linear in the bend and is what actually carries a
    /// straight node, but it measures `dot(up, cross(a, b))`, which responds to bending *across* the
    /// node's up and is blind to bending *within* it. So at a straight corridor node this term is
    /// linearly sensitive to yaw and only quadratically sensitive to pitch.
    ///
    /// Note what the residual is built from: the deformed *positions* of the centre node and its two
    /// neighbours. Node rotation enters only through the centre node's up vector, and only to sign
    /// the sine row. So this term constrains the shape of the path and cannot see a node frame
    /// spinning about that path - if node forwards disagree along a chain of links, this is not the
    /// energy that failed to hold them.
    ///
    /// Structure graphs only - the constraints are built from the structure's links, so a navmesh
    /// graph has none and the term contributes nothing there.
    ///
    /// **This is the first of the three structure-only energies in row order**, ahead of both
    /// terminal blocks. The layout struct lists it last, which is a field ordering that feeds only a
    /// sum; the residual evaluator and the Jacobian both emit it first, and they are what places
    /// rows.
    /// </summary>
    [Serializable]
    [PolymorphicName("Structure Bend")]
    public class EDStructureBendTerm : EDResidualTerm
    {
        public override string name => "structureBend";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new StructureBendInstance(this, deformation);

        public class StructureBendInstance : Instance
        {
            private readonly List<EDStructureBendConstraint> constraints = new();

            /// <summary>
            /// The pairs this term holds, for the diagnostics dump. Rest angles are measured once
            /// per graph build and frozen - see Reset.
            /// </summary>
            public IReadOnlyList<EDStructureBendConstraint> constraintList => constraints;

            public StructureBendInstance(EDStructureBendTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
            }

            /// <summary>
            /// Records the rest angle of every pair of links meeting at a node.
            ///
            /// This used to be EmbededDeformation.BuildLinkAngleConstraints, called by the structure
            /// graph builder, with the result living in a public serialized list on the deformation
            /// that nothing but this term read. Both halves of that were wrong: the solver carried
            /// data belonging to one energy, and the builder had to remember to call a method whose
            /// only purpose was to feed it.
            ///
            /// Nothing is derived here for a sampled graph. Neighbour lists exist there too, so this
            /// would happily produce constraints - they would just be angles between arbitrary
            /// sampling neighbours rather than between structure links, which is not what the term
            /// means. The graph source is the test rather than the presence of neighbours.
            /// </summary>
            public override void Reset()
            {
                constraints.Clear();

                if (deformation.deformationGraphSource != DeformationGraphSource.StructureOnly) return;

                if ((deformation.nodes == null) || (deformation.nodes.Count == 0)) return;

                const double epsilon = 1e-12;

                for (int centerIndex = 0; centerIndex < deformation.nodes.Count; centerIndex++)
                {
                    EDNode centerNode = deformation.nodes[centerIndex];

                    if ((centerNode.neighbors == null) || (centerNode.neighbors.Count < 2))
                        continue;

                    DVector3 center = centerNode.restPosition;
                    DVector3 restUp = centerNode.restUp;

                    if (restUp.sqrMagnitude < epsilon)
                        restUp = DVector3.up;
                    else
                        restUp.Normalize();

                    for (int a = 0; a < centerNode.neighbors.Count - 1; a++)
                    {
                        int neighborA = centerNode.neighbors[a];
                        DVector3 directionA = deformation.nodes[neighborA].restPosition - center;

                        if (directionA.sqrMagnitude < epsilon)
                            continue;

                        directionA.Normalize();

                        for (int b = a + 1; b < centerNode.neighbors.Count;b++)
                        {
                            int neighborB = centerNode.neighbors[b];

                            DVector3 directionB = deformation.nodes[neighborB].restPosition - center;

                            if (directionB.sqrMagnitude < epsilon)
                                continue;

                            directionB.Normalize();

                            double restCos = Math.Clamp(DVector3.Dot(directionA, directionB), -1.0, 1.0);

                            double restSin = Math.Clamp(DVector3.Dot(restUp, DVector3.Cross(directionA, directionB)), -1.0, 1.0);

                        constraints.Add(new EDStructureBendConstraint {
                                centerNode = centerIndex,
                                neighborA = neighborA,
                                neighborB = neighborB,
                                restCos = restCos,
                                restSin = restSin
                            });
                        }
                    }
                }

                Debug.Log($"Built {constraints.Count} structure bend constraints.");
            }

            protected override int ComputeRowCount()
                => 2 * constraints.Count;

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 2); i++)
                {
                    EvaluateSingleResidual(state, i, out double cosineResidual, out double sineResidual);

                    residual[row++] = cosineResidual;
                    residual[row++] = sineResidual;
                }
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                for (int i = 0; i < (rowCount / 2); i++)
                    row = FillJacobianBlock(state, jacobian, row, i, ref jacobianNormSq);
            }

            /// <summary>
            /// Cosine and sine of the angle error at one node, weighted. The two neighbour
            /// directions come from deformed node positions; the centre node's up only orients the
            /// sine, so the sign of the bend is measured in the plane the node actually sits in
            /// rather than against a fixed world axis.
            /// </summary>
            private void EvaluateSingleResidual(EDStateView state, int constraintIndex, out double cosineResidual, out double sineResidual)
            {
                const double epsilon = 1e-12;

                double wStructureBend = residualWeight;

                EDStructureBendConstraint constraint = constraints[constraintIndex];
                DVector3 center = state.DeformNodePosition(constraint.centerNode, deformation.nodes[constraint.centerNode].restPosition);
                DVector3 positionA = state.DeformNodePosition(constraint.neighborA, deformation.nodes[constraint.neighborA].restPosition);
                DVector3 positionB = state.DeformNodePosition(constraint.neighborB, deformation.nodes[constraint.neighborB].restPosition);
                DVector3 directionA = positionA - center;
                DVector3 directionB = positionB - center;

                if ((directionA.sqrMagnitude < epsilon) || (directionB.sqrMagnitude < epsilon))
                {
                    // A collapsed link is strongly invalid.
                    cosineResidual = wStructureBend * (0.0 - constraint.restCos);
                    sineResidual = wStructureBend * (0.0 - constraint.restSin);

                    return;
                }

                directionA.Normalize();
                directionB.Normalize();

                DVector3 currentUp = state.TransformDirection(constraint.centerNode, deformation.nodes[constraint.centerNode].restUp);

                if (currentUp.sqrMagnitude < epsilon)
                    currentUp = deformation.nodes[constraint.centerNode].restUp.normalized;

                double currentCos = Math.Clamp(DVector3.Dot(directionA, directionB), -1.0, 1.0);
                double currentSin = Math.Clamp(DVector3.Dot(currentUp, DVector3.Cross(directionA, directionB)), -1.0, 1.0);

                cosineResidual = wStructureBend * (currentCos - constraint.restCos);
                sineResidual = wStructureBend * (currentSin - constraint.restSin);
            }

            /// <summary>
            /// One finite-difference column. No analytic derivative here - the residual runs through
            /// two normalizations and a cross product, and the perturbed re-evaluation is cheap
            /// enough at two rows per constraint that it has never been worth deriving.
            /// </summary>
            private void FillJacobianColumn(EDState state, DenseMatrix J, int row, int constraintIndex, double baseCosineResidual, double baseSineResidual, int col, ref double jNorm)
            {
                double original = state.Get(col);

                double eps = 1e-6 * Math.Max(1.0, Math.Abs(original));

                EDStateView modified = new EDStateView(state, col, eps);

                EvaluateSingleResidual(modified, constraintIndex, out double modifiedCosineResidual, out double modifiedSineResidual);

                double cosineDerivative = (modifiedCosineResidual - baseCosineResidual) / eps;

                double sineDerivative = (modifiedSineResidual - baseSineResidual) / eps;

                J[row + 0, col] = cosineDerivative;
                J[row + 1, col] = sineDerivative;

                jNorm += cosineDerivative * cosineDerivative + sineDerivative * sineDerivative;
            }

            /// <summary>
            /// The two rows for one constraint. Column order is a contract, not a convenience: centre
            /// node's twelve parameters, then neighbour A's three translations, then neighbour B's.
            /// Reordering these permutes nothing in exact arithmetic and changes the last bits of
            /// jNorm, which is enough to flip an LM step from accepted to rejected.
            /// </summary>
            private int FillJacobianBlock(EDState state, DenseMatrix J, int row, int constraintIndex, ref double jNorm)
            {
                EDStructureBendConstraint constraint = constraints[constraintIndex];

                EDStateView baseView = new EDStateView(state);

                EvaluateSingleResidual(baseView, constraintIndex, out double baseCosineResidual, out double baseSineResidual);

                // Centre node: orientation and translation both matter.
                int centerBase = EDStateView.ParamBase(constraint.centerNode);

                for (int localParameter = 0; localParameter < 12; localParameter++)
                {
                    FillJacobianColumn(state, J, row, constraintIndex, baseCosineResidual, baseSineResidual, centerBase + localParameter, ref jNorm);
                }

                // Neighbour positions depend only on translation.
                int neighborABase = EDStateView.ParamBase(constraint.neighborA);

                for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                {
                    int col = neighborABase + outputAxis * 4 + 3;

                    FillJacobianColumn(state, J, row, constraintIndex, baseCosineResidual, baseSineResidual, col, ref jNorm);
                }

                int neighborBBase = EDStateView.ParamBase(constraint.neighborB);

                for (int outputAxis = 0; outputAxis < 3; outputAxis++)
                {
                    int col = neighborBBase + outputAxis * 4 + 3;

                    FillJacobianColumn(state, J, row, constraintIndex, baseCosineResidual, baseSineResidual, col, ref jNorm);
                }

                return row + 2;
            }
        }
#endif
    }
}
#endif

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
    /// Keeps the navigable corridor around each structure segment from closing up. One row per
    /// segment, penalising clearance that has shrunk below a fraction of what it was at rest and
    /// saying nothing at all about clearance that grew - this is a floor, not a target.
    ///
    /// Both graph sources share it. What differs is how a point is carried from rest to deformed
    /// underneath, and the deformation already decides that for itself.
    ///
    /// Two things make this term unlike the others:
    ///
    /// It has no analytic derivative. The clearance query walks the navmesh, so the Jacobian row is
    /// built by finite differences, one perturbation per parameter. That is expensive enough to be
    /// worth filling rows in parallel, which is what supportsParallelRows opts into - and the
    /// finite-difference path mutates the node frame list it is given, so each worker needs its own
    /// copy rather than sharing one.
    ///
    /// It reads state the solver computes rather than deriving everything from the parameters. The
    /// clearances live on EDState and are refreshed by the navigation solver at each accepted step,
    /// so pairing this term with a solver that never refreshes them silently measures stale values.
    /// That is existing behaviour, not something introduced here.
    /// </summary>
    [Serializable]
    [PolymorphicName("Clearance")]
    public class EDClearanceTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("Clearance may shrink to this fraction of its rest value before the term objects.")]
        private float minRatio = 0.85f;

        public override string name => "clearance";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new ClearanceInstance(this, deformation);

        public class ClearanceInstance : Instance
        {
            private readonly EDClearanceTerm clearanceTerm;

            public ClearanceInstance(EDClearanceTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                clearanceTerm = term;
            }

            /// <summary>
            /// Gated on the navigation data being present, not merely on the weight. Clearance is
            /// measured against the navmesh topology and the per-segment bindings, and without them
            /// there is nothing to measure - so the term contributes no rows rather than rows of
            /// zeros. The weight alone cannot stand in for this: the inspector hides the navigation
            /// weights outside NavED mode but still serializes them, so they are routinely non-zero
            /// in a plain ED solve.
            /// </summary>
            protected override int ComputeRowCount()
            {
                if (!deformation.isNavConfigured) return 0;

                return (deformation.structure != null) ? (deformation.structure.Count) : (0);
            }

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = residualWeight;
                int row = rowOffset;

                for (int i = 0; i < deformation.structure.Count; i++)
                {
                    double originalClearance = deformation.restState.GetClearance(i);
                    double currentClearance = state.GetClearance(i);

                    residual[row++] = w * ComputeLoss(originalClearance, currentClearance);
                }
            }

            /// <summary>
            /// How much of a segment's rest clearance has been lost, as a fraction of that rest
            /// value - so the same absolute pinch counts for more in a narrow corridor than in a
            /// wide one, and the row is comparable between segments of different widths.
            ///
            /// Two floors, and the more permissive wins: a fraction of the rest clearance, and the
            /// rest clearance less a fixed world-space slack. The slack is what stops a very wide
            /// corridor being asked to hold a proportional margin it has no reason to.
            ///
            /// A clearance of MaxValue means "measured nothing", not "infinitely clear" - a segment
            /// with no non-opening wall anywhere near it - so either end being MaxValue scores zero
            /// rather than an enormous loss.
            /// </summary>
            private double ComputeLoss(double original, double current)
            {
                if ((original == double.MaxValue) || (current == double.MaxValue))
                    return 0.0;

                const double epsilon = 1e-3;

                // Optional world-space slack, useful when original clearance is large.
                const double absoluteSlack = 0.05;

                double allowedByRatio = original * clearanceTerm.minRatio;
                double allowedBySlack = Math.Max(0.0, original - absoluteSlack);

                // More permissive of the two.
                double allowed = Math.Min(allowedByRatio, allowedBySlack);

                double loss = (allowed - current) / (original + epsilon);

                return Math.Max(0.0, loss);
            }

            /// <summary>
            /// One segment's residual measured from the geometry rather than read from the cache.
            ///
            /// This is what the Jacobian differences, and it is deliberately not the same route the
            /// residual above takes: that one reads the clearance the solver cached on the state,
            /// which is refreshed only at an accepted step, while a perturbed column has no cached
            /// value and has to walk the navmesh. A segment whose clearance cannot be measured at
            /// all contributes zero, which flattens the row rather than inventing a gradient.
            /// </summary>
            private double EvaluateMeasuredRow(EDStateView state, int segmentIndex, double wClearance, FullDeformationField.TransformBlender blender = null)
            {
                double original = deformation.restState.GetClearance(segmentIndex);

                // Fallback for serial callers. The optimized Jacobian path supplies
                // the blender explicitly, so it does not reach this allocation.
                if ((deformation.UseDeformationFieldForClearance) && (blender == null))
                {
                    blender = deformation.CreateFieldBlender(state);
                }

                if (!deformation.TryComputeSegmentClearance(state, segmentIndex, blender, out double current))
                {
                    return 0.0;
                }

                return wClearance * ComputeLoss(original, current);
            }

            public override bool supportsParallelRows => true;

            /// <summary>
            /// One blender per worker. The finite-difference path overrides the perturbed node's
            /// transform and measures, so workers sharing one would overwrite each other's
            /// perturbations - this is the ownership rule TransformBlender's override field states,
            /// and this method is what satisfies it. Null when the deformation does not carry points
            /// through the field, in which case the row needs no blender at all.
            /// </summary>
            public override object CreateRowScratch(EDState state)
            {
                if (!deformation.UseDeformationFieldForClearance) return null;

                return deformation.CreateFieldBlender(new EDStateView(state));
            }

            public override double FillJacobianRow(EDState state, DenseMatrix jacobian, int rowOffset, int localIndex, object scratch)
                => FillRow(state, jacobian, rowOffset + localIndex, localIndex, residualWeight, scratch as FullDeformationField.TransformBlender);

            /// <summary>
            /// One row, by one finite difference per parameter - every parameter, since a segment's
            /// clearance is measured on geometry carried by the whole graph and there is no column
            /// that can be argued to be zero in advance.
            ///
            /// The early-out when the segment is not being pinched skips all of it and returns a
            /// zero norm contribution, which is why this term is affordable at all: on a solved
            /// piece most segments are clear and cost one measurement rather than one per column.
            ///
            /// The blender override is set and cleared around each column, and the clear is in a
            /// finally rather than after the call. Clearing is total, so a clear that runs when
            /// nothing was set loses nothing - whereas a Set left standing would silently apply the
            /// previous column's perturbation to every measurement after it.
            /// </summary>
            private double FillRow(EDState state, DenseMatrix J, int row, int segmentIndex, double wClearance, FullDeformationField.TransformBlender blender = null)
            {
                var baseView = new EDStateView(state);

                // Serial fallback. In the StructureOnly Jacobian path, this should
                // already have been supplied by the worker-local scratch object.
                if ((deformation.UseDeformationFieldForClearance) && (blender == null))
                {
                    blender = deformation.CreateFieldBlender(baseView);
                }

                double r0 = EvaluateMeasuredRow(baseView, segmentIndex, wClearance, blender);

                if (Math.Abs(r0) <= 1e-12) return 0.0;

                double localJNorm = 0.0;

                // This is the loop over each perturbed Jacobian column.
                for (int col = 0; col < state.Count; col++)
                {
                    double originalParameter = state.Get(col);

                    double eps = 1e-6 * Math.Max(1.0, Math.Abs(originalParameter));

                    var modifiedState = new EDStateView(state, col, eps);

                    if (blender != null)
                    {
                        // Twelve consecutive parameters belong to one ED node, so only this node's frame
                        // changes for this perturbation.
                        blender.SetNodeOverride(col / 12, deformation.GetNodeFrame(col / 12, modifiedState));
                    }

                    double r1;

                    try
                    {
                        r1 = EvaluateMeasuredRow(modifiedState, segmentIndex, wClearance, blender);
                    }
                    finally
                    {
                        // Back to the frozen transforms before the next column. Unconditional, because
                        // clearing is total - there is no state left over from a Set that did not run.
                        blender?.ClearNodeOverride();
                    }

                    double value = (r1 - r0) / eps;

                    J[row, col] = value;
                    localJNorm += value * value;
                }

                return localJNorm;
            }

            /// <summary>
            /// Serial fallback, for a caller that does not take the parallel path. Kept equivalent
            /// rather than merely similar: same rows, same order, same contributions to the norm.
            /// </summary>
            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                object scratch = CreateRowScratch(state);

                for (int i = 0; i < rowCount; i++)
                    jacobianNormSq += FillJacobianRow(state, jacobian, rowOffset, i, scratch);
            }
        }
#endif
    }
}
#endif

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
        public override string name => "clearance";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new ClearanceInstance(this, deformation);

        public class ClearanceInstance : Instance
        {
            public ClearanceInstance(EDClearanceTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
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

                    residual[row++] = w * deformation.ComputeClearanceLoss(originalClearance, currentClearance);
                }
            }

            public override bool supportsParallelRows => true;

            /// <summary>
            /// One node frame list per worker. The finite-difference path perturbs a parameter,
            /// rewrites the affected frames and measures, so workers sharing a list would overwrite
            /// each other's perturbations. Null when the deformation does not carry points through
            /// the field, in which case the row needs no frames at all.
            /// </summary>
            public override object CreateRowScratch(EDState state)
            {
                if (!deformation.UseDeformationFieldForClearance) return null;

                return deformation.BuildNodeFrames(new EDStateView(state));
            }

            public override double FillJacobianRow(EDState state, DenseMatrix jacobian, int rowOffset, int localIndex, object scratch)
                => deformation.FillClearanceJacobianRow(state, jacobian, rowOffset + localIndex, localIndex, residualWeight, scratch as List<FullDeformationField.Frame>);

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

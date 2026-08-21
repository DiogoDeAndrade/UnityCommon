using System;
using System.Collections.Generic;
using UnityEngine;
using UC.DoubleMath;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    public partial class EmbededDeformation
    {
#if MATH_NET_AVAILABLE
        /// <summary>
        /// Emits the residual row layout into an active golden dump, in the order the terms are
        /// actually evaluated in - which is the order the rows are in.
        ///
        /// This used to name all ten blocks from a fixed list, zeros included, in an order that did
        /// not match the rows: link angle was printed after the terminal blocks but evaluated
        /// before them. Naming the terms the model actually carries removes the possibility of the
        /// dump and the solve disagreeing, and a term that is not in the model is simply absent
        /// rather than reported as zero.
        /// </summary>
        private void TraceResidualLayout(EDEnergyModel.Instance energy)
        {
            if (EDDiagnostics.activeTrace == null) return;
            if (energy == null) return;

            EDDiagnostics.Trace("[layout]");

            var layout = energy.DescribeLayout();

            for (int i = 0; i < layout.Count; i++)
                EDDiagnostics.Trace($"{layout[i].name} {layout[i].rows}");

            EDDiagnostics.Trace($"total {energy.totalRows}");
        }

        #region NavMesh-based constraints

        // Widened as terms adopt it - see FillRotationJacobianBlock.
        internal double FillClearanceJacobianRow(EDState state, DenseMatrix J, int row, int segmentIndex, double wClearance, FullDeformationField.TransformBlender blender = null)
        {
            var baseView = new EDStateView(state);

            // Serial fallback. In the StructureOnly Jacobian path, this should
            // already have been supplied by the worker-local scratch object.
            if ((UseDeformationFieldForClearance) && (blender == null))
            {
                blender = CreateFieldBlender(baseView);
            }

            double r0 = EvaluateSingleClearanceResidual(baseView, segmentIndex, wClearance, blender);

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
                    blender.SetNodeOverride(col / 12, GetNodeFrame(col / 12, modifiedState));
                }

                double r1;

                try
                {
                    r1 = EvaluateSingleClearanceResidual(modifiedState, segmentIndex, wClearance, blender);
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

        #endregion

#endif
    }
}
#endif

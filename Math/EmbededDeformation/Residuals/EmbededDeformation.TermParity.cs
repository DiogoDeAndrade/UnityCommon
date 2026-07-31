using System.Text;
using UnityEngine;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Compares a term-based energy model against the monolithic residual and Jacobian it is
    /// replacing.
    ///
    /// This is what makes migrating the terms safe. Each one can be moved across on its own and
    /// checked to produce bit-identical rows before the next is started, instead of migrating all
    /// thirteen and then trying to work out which of them moved the answer. The legacy path stays
    /// in place for the duration and goes once the last term has landed.
    /// </summary>
    public partial class EmbededDeformation
    {
#if MATH_NET_AVAILABLE
        public string VerifyTermParity(EDEnergyModel model, WeightConfig weights)
        {
            if (model == null)
                return "No energy model assigned.";

            if ((nodes == null) || (nodes.Count == 0))
                return "No deformation graph - press Build first.";

            if (currentState == null)
                return "No current deformation state - run a solve first.";

            var modelInstance = model.NewInstance(this);
            modelInstance.ApplyRuntimeParameters();
            modelInstance.Resolve();

            var stateView = new EDStateView(currentState);

            var legacyResidual = evaluateResidualVector(stateView, weights);
            var legacyJacobian = buildJacobian(currentState, out double legacyJNorm, weights);

            var termResidual = modelInstance.EvaluateResidual(stateView);
            var termJacobian = modelInstance.BuildJacobian(currentState, out double termJNorm);

            var report = new StringBuilder();

            report.AppendLine($"legacy rows {legacyResidual.Count}, term rows {termResidual.Count}");
            report.AppendLine($"legacy jNorm {EDDiagnostics.F(legacyJNorm)}, term jNorm {EDDiagnostics.F(termJNorm)}");

            // Partial migration is the normal state here, so the term list is compared as a prefix
            // of the legacy rows rather than requiring the two to be the same length. That only
            // holds while terms are migrated top-down in the legacy block order, which is why the
            // order is not negotiable.
            if (termResidual.Count > legacyResidual.Count)
            {
                report.AppendLine("The term list produces more rows than the legacy path. Check the row counts of the migrated terms.");
                return report.ToString();
            }

            if (termResidual.Count < legacyResidual.Count)
                report.AppendLine($"Partial migration: comparing the first {termResidual.Count} rows; {legacyResidual.Count - termResidual.Count} legacy rows are not yet covered.");

            var layout = modelInstance.DescribeLayout();

            for (int i = 0; i < layout.Count; i++)
            {
                var block = layout[i];

                if (block.rows == 0)
                {
                    report.AppendLine($"{block.name}: disabled");
                    continue;
                }

                double worstResidual = 0.0;
                double worstJacobian = 0.0;
                int firstDifferingRow = -1;

                for (int r = block.offset; r < (block.offset + block.rows); r++)
                {
                    double dr = System.Math.Abs(legacyResidual[r] - termResidual[r]);

                    if ((dr > 0.0) && (firstDifferingRow < 0)) firstDifferingRow = r;

                    worstResidual = System.Math.Max(worstResidual, dr);

                    for (int c = 0; c < legacyJacobian.ColumnCount; c++)
                    {
                        double dj = System.Math.Abs(legacyJacobian[r, c] - termJacobian[r, c]);

                        if ((dj > 0.0) && (firstDifferingRow < 0)) firstDifferingRow = r;

                        worstJacobian = System.Math.Max(worstJacobian, dj);
                    }
                }

                string verdict = ((worstResidual == 0.0) && (worstJacobian == 0.0)) ? ("EXACT") : ("DIFFERS");

                report.AppendLine($"{block.name}: rows [{block.offset}..{block.offset + block.rows - 1}] {verdict} maxResidualDiff {EDDiagnostics.F(worstResidual)} maxJacobianDiff {EDDiagnostics.F(worstJacobian)}" +
                                  ((firstDifferingRow >= 0) ? ($" firstRow {firstDifferingRow}") : ("")));
            }

            return report.ToString();
        }
#else
        public string VerifyTermParity(EDEnergyModel model, WeightConfig weights) => "Math.NET is not available.";
#endif
    }
}
#endif

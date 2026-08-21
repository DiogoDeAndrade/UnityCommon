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
    /// <summary>
    /// What is left of the residual machinery once every energy owns its own.
    ///
    /// This file used to hold a filler per energy - the row count, the residual and the Jacobian for
    /// one term living in three places that had to be kept in step by hand. They are on the terms
    /// now, so what remains is the one thing that is genuinely about the problem rather than about
    /// any energy in it: reporting the row layout the terms between them produced.
    /// </summary>
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
#endif
    }
}
#endif

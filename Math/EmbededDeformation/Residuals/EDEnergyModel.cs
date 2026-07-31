using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// The energy being minimised, as an ordered list of terms.
    ///
    /// Order is a contract, not a presentation detail. Reordering the terms permutes the rows of the
    /// residual and the Jacobian, and while JtJ and Jtf are mathematically invariant to that,
    /// floating-point summation is not - a last-bit change in the residual norm can flip a
    /// Levenberg-Marquardt step from accepted to rejected, and ten iterations later that is visibly
    /// different geometry.
    /// </summary>
    [CreateAssetMenu(fileName = "EDEnergyModel", menuName = "Unity Common/ED/Energy Model")]
    public class EDEnergyModel : ScriptableObject
    {
        [SerializeReference]
        protected List<EDResidualTerm> terms = new List<EDResidualTerm>();

        [SerializeField, Tooltip("Divide each term's weight by its row count, so a term's influence does not grow with the size of the graph.")]
        protected bool normalizeWeights = false;

        public IReadOnlyList<EDResidualTerm> termList => terms;
        public bool normalizesWeights => normalizeWeights;

#if MATH_NET_AVAILABLE
        public Instance NewInstance(EmbededDeformation deformation) => new Instance(this, deformation);

        public class Instance
        {
            public EDEnergyModel                model { get; private set; }
            public EmbededDeformation           deformation { get; private set; }
            public List<EDResidualTerm.Instance> termInstances { get; private set; }

            public int totalRows { get; private set; }

            public Instance(EDEnergyModel model, EmbededDeformation deformation)
            {
                this.model = model;
                this.deformation = deformation;

                termInstances = new List<EDResidualTerm.Instance>();

                for (int i = 0; i < model.terms.Count; i++)
                {
                    if (model.terms[i] == null) continue;

                    termInstances.Add(model.terms[i].NewInstance(deformation, model.normalizeWeights));
                }
            }

            /// <summary>
            /// Re-resolves every term's row count and weight. Run at the start of each solve rather
            /// than only at construction, because a weight can be edited on the asset and the graph
            /// can be rebuilt while this instance is cached.
            /// </summary>
            public void Resolve()
            {
                totalRows = 0;

                for (int i = 0; i < termInstances.Count; i++)
                {
                    termInstances[i].Resolve(model.normalizeWeights);
                    totalRows += termInstances[i].rowCount;
                }
            }

            public void ApplyRuntimeParameters()
            {
                for (int i = 0; i < termInstances.Count; i++)
                    termInstances[i].term.ApplyRuntimeParameters(deformation);
            }

            /// <summary>
            /// Row ranges in list order, which is the layout. Replaces the hand-maintained
            /// EDResidualLayout struct and the four places that had to agree with it.
            /// </summary>
            public IReadOnlyList<(string name, int offset, int rows)> DescribeLayout()
            {
                var described = new List<(string, int, int)>();

                int offset = 0;
                for (int i = 0; i < termInstances.Count; i++)
                {
                    described.Add((termInstances[i].term.name, offset, termInstances[i].rowCount));
                    offset += termInstances[i].rowCount;
                }

                return described;
            }

            public Vector<double> EvaluateResidual(EDStateView state)
            {
                var residual = DenseVector.Create(totalRows, 0.0);

                int offset = 0;
                for (int i = 0; i < termInstances.Count; i++)
                {
                    if (termInstances[i].rowCount == 0) continue;

                    termInstances[i].EvaluateResidual(state, residual, offset);
                    offset += termInstances[i].rowCount;
                }

                return residual;
            }

            public Matrix<double> BuildJacobian(EDState state, out double jNorm)
            {
                double jNormSq = 0.0;

                var jacobian = DenseMatrix.Create(totalRows, 12 * deformation.nodes.Count, 0.0);

                int offset = 0;
                for (int i = 0; i < termInstances.Count; i++)
                {
                    if (termInstances[i].rowCount == 0) continue;

                    if (termInstances[i].supportsParallelRows)
                        FillJacobianRowsInParallel(termInstances[i], state, jacobian, offset, ref jNormSq);
                    else
                        termInstances[i].FillJacobian(state, jacobian, offset, ref jNormSq);

                    offset += termInstances[i].rowCount;
                }

                jNorm = System.Math.Sqrt(jNormSq);

                return jacobian;
            }

            /// <summary>
            /// Fills a term's rows across workers, each with its own scratch.
            ///
            /// The norms are collected into one slot per row and summed serially afterwards, in row
            /// order. Adding them as the workers finished instead made the total vary in the low
            /// bits run to run, because double addition is not associative and completion order is
            /// not fixed. It only feeds an early-out, but it was enough to stop a diagnostic dump
            /// reproducing, which is the one thing this whole exercise depends on.
            /// </summary>
            private static void FillJacobianRowsInParallel(EDResidualTerm.Instance term, EDState state, DenseMatrix jacobian, int rowOffset, ref double jNormSq)
            {
                double[] rowNorms = new double[term.rowCount];

                Parallel.For(
                    0,
                    term.rowCount,
                    EDDiagnostics.parallelOptions,

                    () => term.CreateRowScratch(state),

                    (localIndex, loopState, scratch) =>
                    {
                        rowNorms[localIndex] = term.FillJacobianRow(state, jacobian, rowOffset, localIndex, scratch);

                        return scratch;
                    },

                    scratch => { }
                );

                for (int i = 0; i < rowNorms.Length; i++)
                    jNormSq += rowNorms[i];
            }
        }
#endif
    }
}
#endif

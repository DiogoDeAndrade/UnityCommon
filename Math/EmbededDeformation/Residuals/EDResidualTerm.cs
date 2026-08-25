using System;
using UnityEngine;
using UC;

#if MATH_NET_AVAILABLE
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
#endif

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// One energy in the least-squares problem: how many rows it occupies, what residual it puts in
    /// them, and what its derivative contributes to the Jacobian.
    ///
    /// The point of naming these individually is that those three used to live in three separate
    /// places that had to be kept in step by hand, and had already drifted apart at least once - the
    /// terminal and link-angle rows were silently unreachable in the navmesh layout because the
    /// shared layout builder never allocated them. A term owns all three, so they cannot disagree.
    /// As of 2026-08-21 every term does: nothing in the residual or the Jacobian of any energy is on
    /// EmbededDeformation any more, and its parameters are read off it rather than pushed onto the
    /// deformation before a solve.
    ///
    /// What a term still asks the deformation for is the graph and the geometry - the node list, the
    /// structure, the bindings, the navmesh measurements, the constraint sets built for it. Those
    /// belong to the piece rather than to any one energy, and several energies read the same ones.
    ///
    /// Definition and instance are split as elsewhere: the definition is a serialized description
    /// that may be shared, the instance resolves it against a particular graph and holds the
    /// per-solve state.
    /// </summary>
    [Serializable]
    public abstract class EDResidualTerm
    {
        [SerializeField, Min(0.0f)]
        protected float weight = 0.0f;

        /// <summary>
        /// Label used in the residual layout, the energy breakdown and the parity report.
        /// </summary>
        public abstract string name { get; }

        public float conceptualWeight => weight;

        /// <summary>
        /// Sets the conceptual weight, exactly as editing the field in the inspector would.
        /// Instance.Resolve re-reads it on every solve, so the next iteration runs under the new
        /// value with no rebuild - which is what a continuation schedule is. For experiment
        /// drivers such as the schedule runner; a solve never writes a term, and nothing here
        /// should either.
        /// </summary>
        public void SetConceptualWeight(float value) => weight = Mathf.Max(0.0f, value);

#if MATH_NET_AVAILABLE
        public abstract Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights);

        public abstract class Instance
        {
            public EDResidualTerm       term { get; private set; }
            public EmbededDeformation   deformation { get; private set; }

            /// <summary>
            /// Rows this term occupies, resolved once per solve. Zero disables it entirely, which
            /// is how a zero weight and an unavailable term are expressed uniformly.
            /// </summary>
            public int rowCount { get; protected set; }

            /// <summary>
            /// sqrt(weight / rows) when normalising, sqrt(weight) otherwise, and zero when the term
            /// is off. Applied to both the residual and its Jacobian, which is what makes the
            /// least-squares problem weighted rather than merely scaled.
            /// </summary>
            public double residualWeight { get; protected set; }

            /// <summary>
            /// What this term costs to differentiate, accumulated over a solve. Per term rather than
            /// from a fixed list on the deformation, so the breakdown describes whichever energies a
            /// configuration actually uses and cannot fall out of step when one is added.
            ///
            /// The Jacobian rather than the residual, because that is where the cost is: a term with
            /// no analytic derivative perturbs every parameter and re-evaluates, which is thousands
            /// of times the work of evaluating the residual once.
            /// </summary>
            public DebugProfiler jacobianTimer { get; private set; } = new DebugProfiler();

            protected Instance(EDResidualTerm term, EmbededDeformation deformation)
            {
                this.term = term;
                this.deformation = deformation;
            }

            /// <summary>
            /// Recomputes the row count and weight against the current graph. Called at the start of
            /// every solve, because a weight can be edited between runs and the graph can be rebuilt
            /// under a cached instance.
            /// </summary>
            public void Resolve(bool normalizeWeights)
            {
                rowCount = ComputeRowCount();
                residualWeight = BuildResidualWeight(term.conceptualWeight, rowCount, normalizeWeights);

                if (residualWeight <= 0.0)
                    rowCount = 0;
            }

            /// <summary>
            /// Rebuilds whatever this term derives from the graph - the constraint set it will put
            /// rows for, typically. Called once when the instance is created and again whenever the
            /// graph underneath it is rebuilt; never per solve and never per iteration.
            ///
            /// **This is separate from Resolve on purpose.** Resolve answers "how many rows and at
            /// what weight", which is a question about the term's configuration and is cheap enough
            /// to ask every solve. This answers "what am I constraining", which is a question about
            /// the graph, and doing it inside ComputeRowCount would be a query that mutates - the
            /// kind of thing that is correct until someone reasonably assumes a getter is a getter.
            ///
            /// A term deriving nothing from the graph does not override it, which is most of them.
            /// </summary>
            public virtual void Reset() { }

            /// <summary>
            /// Rows this term will occupy. A pure query - anything that has to be *built* first
            /// belongs in Reset, which has already run by the time this is called.
            /// </summary>
            protected abstract int ComputeRowCount();

            /// <summary>
            /// Labels for the term's own tracked values - the columns this term adds to the energy
            /// breakdown beyond the shared rows/weight/energy/rms/max/share, so a quality term's
            /// inverted counts and measures land in a CSV as columns rather than as prose. Empty
            /// for the terms whose residual says everything, which is most of them. Labels only,
            /// no measuring - the values half is <see cref="Describe"/>, and the two must return
            /// arrays of the same length.
            /// </summary>
            public virtual string[] DescribeHeader() => Array.Empty<string>();

            /// <summary>
            /// The term's own tracked values at a state, one bare string per
            /// <see cref="DescribeHeader"/> entry, in units a reader can check against the scene.
            /// A term that overrides this may re-measure, so callers treat it as a
            /// residual-evaluation-sized cost, not a getter - it is asked once per breakdown,
            /// never per iteration.
            /// </summary>
            public virtual string[] Describe(EDStateView state) => Array.Empty<string>();

            public abstract void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset);

            public abstract void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq);

            /// <summary>
            /// Opt-in for terms whose rows are independent and expensive enough to be worth filling
            /// in parallel. Only clearance needs it, and it needs per-worker scratch because the
            /// finite-difference path mutates a shared frame list.
            /// </summary>
            public virtual bool supportsParallelRows => false;

            public virtual object CreateRowScratch(EDState state) => null;

            public virtual double FillJacobianRow(EDState state, DenseMatrix jacobian, int rowOffset, int localIndex, object scratch)
                => throw new NotSupportedException($"{term.name} does not support per-row Jacobian filling.");
        }

        /// <summary>
        /// sqrt(weight / rows) when normalising, sqrt(weight) otherwise.
        ///
        /// Dividing by the row count before the square root is what makes conceptual weights
        /// comparable across terms: a term then contributes conceptualWeight times mean(r squared),
        /// so a term with three thousand rows does not outvote one with thirty by being larger.
        ///
        /// Public and static because it was shared with the legacy layout builder, so that a
        /// migrated term produced exactly the weight the block it replaced did. That builder is
        /// gone and Resolve is the only caller left - but the weights in the goldens are this
        /// expression, so it stays one place rather than being inlined into it.
        /// </summary>
        public static double BuildResidualWeight(double conceptualWeight, int residualRows, bool normalizeResidualGroups)
        {
            if ((conceptualWeight <= 0.0) || (residualRows <= 0))
                return 0.0;

            double denom = (normalizeResidualGroups) ? (residualRows) : (1.0);

            return Math.Sqrt(conceptualWeight / denom);
        }
#endif
    }
}
#endif

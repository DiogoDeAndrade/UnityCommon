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
    /// One energy in the least-squares problem: how many rows it occupies, what residual it puts in
    /// them, and what its derivative contributes to the Jacobian.
    ///
    /// The point of naming these individually is that the row count, the residual and the Jacobian
    /// for a single energy currently live in three separate places that have to be kept in step by
    /// hand, and have already drifted apart at least once - the terminal and link-angle rows are
    /// silently unreachable in the navmesh layout because the shared layout builder never allocates
    /// them. A term owns all three, so they cannot disagree.
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
        /// Pushes any limits this term needs onto the deformation before a solve - the slope term's
        /// maximum angle, for instance. Called once per solve, not per evaluation.
        /// </summary>
        public virtual void ApplyRuntimeParameters(EmbededDeformation deformation) { }

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

            protected abstract int ComputeRowCount();

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
        /// Shared with the legacy layout builder so a migrated term produces exactly the same
        /// weight the block it replaces did.
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

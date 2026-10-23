using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
    /// Pulls constrained navmesh vertices onto their target positions - three rows each, the
    /// deformed vertex minus where the handle says it should be. This is the term that actually
    /// drives the deformation; every other energy only says what the graph may do on the way.
    ///
    /// The vertex is deformed by whatever carries the geometry, asked of the deformation
    /// (DeformPinnedVertex), so a pin is met where the output puts the vertex: through its binding,
    /// as ED deforms and as every golden pins; or, on a sampled graph whose field is the carrier
    /// (2026-10-20), through the field under its own blend. The Jacobian follows: analytic over the
    /// binding's nodes and weights, analytic over the field's nodes and weights under the linear
    /// blend - the same block, since the field's per-node map at a point is the node's own affine -
    /// and by finite differences through the blender under a decomposed blend, which is not linear
    /// in the parameters.
    ///
    /// A constrained vertex writes into every node it reaches. That is why the blocks accumulate
    /// into the Jacobian rather than assigning: two entries can name the same node.
    ///
    /// A constraint naming a vertex outside the rest mesh contributes three zero rows rather than
    /// being dropped, which keeps the row count a plain three per constraint.
    /// </summary>
    [Serializable]
    [PolymorphicName("Terminal Position (Vertices)")]
    public class EDVertexConstraintTerm : EDResidualTerm
    {
        // Renamed from "constraint" on 2026-08-27, so the dumps and CSVs group it with
        // terminalOrientation and terminalScale - the three are one question about the
        // terminals. The class name deliberately did not move: [SerializeReference] persists
        // it, and a renamed class detaches every energy asset that carries the term.
        public override string name => "terminalPosition";

        // Off is what every golden and every run before 2026-10-23 solved under: rows in world units.
        [SerializeField, Tooltip("Divide the rows by the rest geometry's size - the largest side of its bounding box - so the term is dimensionless: its energy is the weight times the mean squared miss as a fraction of the size, and a weight tuned on a unit mesh means the same on one a hundred across. Off, the rows are world distances. Read at every solve, so it needs no Build.")]
        protected bool normalizeByRestSize = false;

        public bool normalizesByRestSize => normalizeByRestSize;

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new VertexConstraintInstance(this, deformation);

        public class VertexConstraintInstance : Instance
        {
            // The term as its own type, for the option it carries beside its weight. Read where it
            // is used and never copied: the option can change between two solves.
            private readonly EDVertexConstraintTerm constraintTerm;

            public VertexConstraintInstance(EDVertexConstraintTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                constraintTerm = term;
            }

            // The weight each row carries: the resolved residual weight, over the rest size when the
            // term is dimensionless (2026-10-23); every Jacobian path below scales by the same number.
            private double rowWeight => (constraintTerm.normalizeByRestSize) ? (residualWeight / deformation.restSize) : (residualWeight);

            protected override int ComputeRowCount()
                => 3 * ((deformation.vertexConstraints != null) ? (deformation.vertexConstraints.Count) : (0));

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double w = rowWeight;
                int row = rowOffset;

                // The carrier's blender at this state, or null when the bindings carry.
                FullDeformationField.TransformBlender blender = deformation.CreatePinBlender(state);

                for (int c = 0; c < deformation.vertexConstraints.Count; c++)
                {
                    EDVertexConstraint vc = deformation.vertexConstraints[c];

                    if ((vc.vertexIndex < 0) || (vc.vertexIndex >= deformation.restVertices.Length))
                    {
                        residual[row++] = 0.0;
                        residual[row++] = 0.0;
                        residual[row++] = 0.0;
                        continue;
                    }

                    DVector3 deformed = deformation.DeformPinnedVertex(vc.vertexIndex, state, blender);
                    DVector3 r = deformed - vc.targetPosition;

                    residual[row++] = w * r.x;
                    residual[row++] = w * r.y;
                    residual[row++] = w * r.z;
                }
            }

            // A step in each parameter for the decomposed blend's columns. 1e-3 and not the 1e-6 the
            // double-precision terms use, for the field quality terms' reason: the field deforms in
            // float, and at 1e-6 a vertex a few units from its node moves by about one ulp.
            private const double fieldFiniteDifferenceStep = 1e-3;

            // The field's nodes and weights at each pinned vertex, by vertex index, read once per
            // field - the lookup is a function of the rest position and the field alone - so the
            // Jacobian does not repeat it every build. Filled serially before any column is taken.
            private FullDeformationField influencesField;
            private readonly Dictionary<int, PinInfluences> influences = new();
            private bool warnedUnreached;

            private struct PinInfluences
            {
                public int[]    nodes;
                public float[]  weights;
            }

            public override void Reset()
            {
                base.Reset();

                influences.Clear();
                influencesField = null;
                warnedUnreached = false;
            }

            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                int row = rowOffset;

                if (deformation.pinsThroughField)
                {
                    EnsureInfluences();

                    if (deformation.pinsBlendLinearly)
                        FillLinearFieldJacobian(jacobian, rowOffset, ref jacobianNormSq);
                    else
                        FillFiniteDifferenceJacobian(state, jacobian, rowOffset, ref jacobianNormSq);

                    return;
                }

                // Deliberately not guarding the vertex index the way the residual does, which is a
                // real asymmetry rather than an oversight: the residual emits three zero rows for a
                // constraint naming a vertex outside the mesh, and this throws. It is preserved
                // because the baselines were captured against it and no golden configuration
                // contains such a constraint - so the guard has never had anything to do.
                for (int c = 0; c < deformation.vertexConstraints.Count; c++)
                    row = FillJacobianBlock(jacobian, row, deformation.vertexConstraints[c].vertexIndex, rowWeight, ref jacobianNormSq);
            }

            /// <summary>
            /// The three rows for one constrained vertex through its binding, analytically.
            ///
            /// It accumulates rather than assigns, and that is load-bearing: the vertex is deformed
            /// through its binding, so it writes into every node it is bound to, and two entries of
            /// one binding can name the same node. Assigning would keep only the last of them.
            ///
            /// No state, for the same reason the regularization block needs none - the residual is
            /// affine in the parameters, so every entry is a rest offset times a binding weight.
            /// </summary>
            private int FillJacobianBlock(DenseMatrix J, int row, int vertexIndex, double wCon, ref double jNormRunningTotalSq)
            {
                DVector3 v = deformation.restVertices[vertexIndex];
                EDVertexBinding binding = deformation.bindings[vertexIndex];

                for (int b = 0; b < binding.nodeIndices.Length; b++)
                {
                    int nodeIndex = binding.nodeIndices[b];
                    if (nodeIndex < 0)
                        continue;

                    double wb = ((binding.weights != null) && (b < binding.weights.Length)) ? (binding.weights[b]) : (1.0 / binding.nodeIndices.Length);

                    AccumulateNodeBlock(J, row, nodeIndex, v, wCon * wb, ref jNormRunningTotalSq);
                }

                return row + 3;
            }

            /// <summary>
            /// One node's twelve columns of a pinned vertex's three rows, for a vertex whose deformed
            /// position is s times the node's map at it plus terms the node does not touch: each entry
            /// is the rest offset from the node, or one, times s. Shared by the bindings path, where s
            /// is the term's weight times the binding weight, and the field's linear path, where it is
            /// the term's weight times the field's.
            /// </summary>
            private void AccumulateNodeBlock(DenseMatrix J, int row, int nodeIndex, DVector3 v, double s, ref double jNormRunningTotalSq)
            {
                int p = EDStateView.ParamBase(nodeIndex);

                DVector3 g = deformation.nodes[nodeIndex].restPosition;
                DVector3 u = v - g;

                double ux = u.x;
                double uy = u.y;
                double uz = u.z;

                // residual x
                J[row + 0, p + 0] += s * ux; // m00
                J[row + 0, p + 1] += s * uy; // m01
                J[row + 0, p + 2] += s * uz; // m02
                J[row + 0, p + 3] += s;      // m03 / tx

                // residual y
                J[row + 1, p + 4] += s * ux; // m10
                J[row + 1, p + 5] += s * uy; // m11
                J[row + 1, p + 6] += s * uz; // m12
                J[row + 1, p + 7] += s;      // m13 / ty

                // residual z
                J[row + 2, p + 8] += s * ux; // m20
                J[row + 2, p + 9] += s * uy; // m21
                J[row + 2, p + 10] += s * uz; // m22
                J[row + 2, p + 11] += s;      // m23 / tz

                double u2 = ux * ux + uy * uy + uz * uz;
                jNormRunningTotalSq += 3.0 * s * s * (u2 + 1.0);
            }

            /// <summary>
            /// Reads the field's nodes and weights at every pinned vertex not yet read under this
            /// field, and says once when a pinned vertex is reached by none - the output leaves such
            /// a vertex at rest, so its pin cannot be met and its rows have no gradient.
            /// </summary>
            private void EnsureInfluences()
            {
                FullDeformationField field = deformation.GetDeformationField();

                if (field != influencesField)
                {
                    influences.Clear();
                    influencesField = field;
                    warnedUnreached = false;
                }

                int unreached = 0;

                for (int c = 0; c < deformation.vertexConstraints.Count; c++)
                {
                    int v = deformation.vertexConstraints[c].vertexIndex;

                    if (influences.ContainsKey(v)) continue;

                    if (!deformation.TryGetPinInfluences(v, out int[] nodes, out float[] weights))
                    {
                        nodes = Array.Empty<int>();
                        weights = Array.Empty<float>();

                        unreached++;
                    }

                    influences.Add(v, new PinInfluences { nodes = nodes, weights = weights });
                }

                if ((unreached > 0) && (!warnedUnreached))
                {
                    warnedUnreached = true;

                    Debug.LogWarning($"[ED] {term.name}: {unreached} pinned vertices are reached by no node through the field, so the output leaves them at rest and their pins cannot be met.");
                }
            }

            /// <summary>
            /// The field's linear blend, analytically: at a point the field's position is the
            /// weighted sum of its nodes' matrices applied to it, and a node's matrix applied to a
            /// point is that node's own map, A(v - g) + g + t. So the block is the bindings path's,
            /// over the field's nodes and normalized weights.
            /// </summary>
            private void FillLinearFieldJacobian(DenseMatrix J, int rowOffset, ref double jNormRunningTotalSq)
            {
                double wCon = rowWeight;
                int row = rowOffset;

                for (int c = 0; c < deformation.vertexConstraints.Count; c++)
                {
                    int vertexIndex = deformation.vertexConstraints[c].vertexIndex;

                    PinInfluences pin = influences[vertexIndex];

                    DVector3 v = deformation.restVertices[vertexIndex];

                    for (int b = 0; b < pin.nodes.Length; b++)
                    {
                        int nodeIndex = pin.nodes[b];

                        if ((nodeIndex < 0) || (nodeIndex >= deformation.nodes.Count)) continue;

                        AccumulateNodeBlock(J, row, nodeIndex, v, wCon * pin.weights[b], ref jNormRunningTotalSq);
                    }

                    row += 3;
                }
            }

            private sealed class ColumnScratch
            {
                public FullDeformationField.TransformBlender blender;
                public double normSq;
            }

            /// <summary>
            /// A decomposed blend, by finite differences: for each pinned vertex, each of the twelve
            /// parameters of each node that reaches it, the node's frame at the perturbed state handed
            /// to the blender as an override and the vertex deformed again - the arrangement the field
            /// quality and clearance terms use. The vertices are spread over the workers, each with a
            /// blender of its own; a vertex's rows are its own, so no two workers write one entry.
            /// </summary>
            private void FillFiniteDifferenceJacobian(EDState state, DenseMatrix J, int rowOffset, ref double jNormRunningTotalSq)
            {
                var constraints = deformation.vertexConstraints;

                var baseView = new EDStateView(state);

                double wCon = rowWeight;
                double totalNormSq = 0.0;
                object normLock = new object();

                Parallel.For(
                    0,
                    constraints.Count,
                    EDDiagnostics.parallelOptions,

                    () => new ColumnScratch { blender = deformation.CreatePinBlender(baseView) },

                    (c, loopState, scratch) =>
                    {
                        int vertexIndex = constraints[c].vertexIndex;
                        int row = rowOffset + (3 * c);

                        PinInfluences pin = influences[vertexIndex];

                        if (pin.nodes.Length == 0) return scratch;

                        DVector3 basePosition = deformation.DeformPinnedVertex(vertexIndex, baseView, scratch.blender);

                        for (int b = 0; b < pin.nodes.Length; b++)
                        {
                            int n = pin.nodes[b];

                            if ((n < 0) || (n >= deformation.nodes.Count)) continue;

                            // A node can be listed once per trilinear corner it reaches; its columns
                            // are taken once.
                            if (Array.IndexOf(pin.nodes, n, 0, b) >= 0) continue;

                            int p = EDStateView.ParamBase(n);

                            for (int k = 0; k < 12; k++)
                            {
                                int col = p + k;

                                double original = state.Get(col);
                                double eps = fieldFiniteDifferenceStep * Math.Max(1.0, Math.Abs(original));

                                var modifiedState = new EDStateView(state, col, eps);

                                // Twelve consecutive parameters belong to one node, so only this
                                // node's frame changes for this perturbation.
                                scratch.blender.SetNodeOverride(n, deformation.GetNodeFrame(n, modifiedState));

                                try
                                {
                                    DVector3 d = (deformation.DeformPinnedVertex(vertexIndex, modifiedState, scratch.blender) - basePosition) * (wCon / eps);

                                    J[row + 0, col] += d.x;
                                    J[row + 1, col] += d.y;
                                    J[row + 2, col] += d.z;

                                    scratch.normSq += (d.x * d.x) + (d.y * d.y) + (d.z * d.z);
                                }
                                finally
                                {
                                    scratch.blender.ClearNodeOverride();
                                }
                            }
                        }

                        return scratch;
                    },

                    scratch =>
                    {
                        lock (normLock) totalNormSq += scratch.normSq;
                    }
                );

                jNormRunningTotalSq += totalNormSq;
            }
        }
#endif
    }
}
#endif

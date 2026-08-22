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
    /// Penalises output triangles that have turned over.
    ///
    /// Every other energy is a statement about the graph - node frames, segment lengths, probe
    /// normals - and the failures this method has are the ones none of those measure: the node
    /// determinants say one thing and the geometry several sampling distances away does another.
    /// This is the first term that looks at the output itself. It deforms the mesh the output is
    /// generated from and reads the signed area of every triangle against the normal that triangle
    /// had at rest. A triangle is charged how far its area ratio a_t / A_t has fallen below a floor,
    /// r_t = max(0, rho - a_t / A_t), and the row is the root mean square of those over the mesh,
    /// weighted by each triangle's share of the rest area:
    ///
    ///     r = sqrt( sum_t (A_t / A) * r_t^2 )
    ///
    /// so the least-squares energy is weight times the area-weighted mean of r_t^2 - which is
    /// exactly what per-triangle rows would produce under normalised weights, collapsed into one
    /// row. The two formulations share an objective and differ only in the Gauss-Newton
    /// approximation, rank one here against the full J^T J there.
    ///
    /// **Squared per triangle and then averaged, not averaged and then squared.** The first version
    /// summed the inverted area, divided by the rest area, and let the solver square the result; on
    /// a mesh where two percent of the area is inverted that residual is a few thousandths and its
    /// square a few millionths, and it took a weight of five million to matter. Squaring a sparse
    /// mean punishes sparsity quadratically - by Jensen the mean of the squares is never smaller, and
    /// for violations confined to a small region it is larger by roughly the inverse of that
    /// region's share. The RMS form puts the weight in the range every other term uses, because it
    /// is the convention every other term uses: weight times the mean squared violation of a
    /// per-element, dimensionless quantity.
    ///
    /// Dimensionless and area-weighted, so it is independent of piece size and of tessellation: a
    /// uniform subdivision changes neither a triangle's share of the area nor its ratio. The rest
    /// areas are taken from the same triangles the term measures, once in Reset, so they are exact
    /// for the mesh in front of it rather than an estimate off the source geometry.
    ///
    /// **The floor is what the hinge at zero could not see.** With rho = 0 only inversion is charged,
    /// and a triangle squashed to no area at all scores exactly as a healthy one does - which is the
    /// configuration the solver reached for once the inversions were gone, seen on the terminals the
    /// first time this ran at a weight that mattered. A floor at a fraction of the rest area charges
    /// the squash too, with the same shape the determinant floor and the clearance ratio have. It is
    /// a margin rather than a scale limit, and it has to stay well below one, because a terminal
    /// asked to scale by a half legitimately halves its triangles' area.
    ///
    /// **One row, deliberately, and a baseline rather than a design.** This exists to find out
    /// whether the solver can use a signal from the output at all before anything better is built.
    /// Per-triangle rows and a mesh without the subdivision are the versions after this one.
    ///
    /// The derivative is by finite differences, since the output goes through the field's blend and
    /// there is nothing analytic to take. What makes that affordable is sparsity: a vertex depends
    /// only on the nodes that influence it, and that set is a fact about the rest position, so each
    /// column re-deforms only the vertices its node reaches and re-measures only the triangles those
    /// vertices touch. A node none of whose triangles is inverted at the base state contributes a
    /// zero column without being measured at all, which is the same one-sided early-out clearance
    /// uses and is what makes the term cheap on a healthy piece.
    ///
    /// The mesh is supplied by the owner through EmbededDeformation.SetOutputMesh, already
    /// subdivided the way the output will be, and a term finding none contributes no rows and says
    /// so once. The simplifier is not applied: it runs on the deformed output, after the fact, and
    /// there is no rest mesh to measure its result against.
    /// </summary>
    [Serializable]
    [PolymorphicName("Output Mesh Quality")]
    public class EDOutputMeshQualityTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("Relative step for the finite-difference derivative. Larger than the 1e-6 the other finite-difference terms use on purpose: the output is deformed in float, so a vertex a few units from its node moves by about one float ulp per 1e-6 of parameter, and a difference taken at that scale is quantisation noise rather than a derivative.")]
        private float finiteDifferenceStep = 1e-3f;

        [SerializeField, Range(0.0f, 1.0f), Tooltip("A triangle is charged once its area falls below this fraction of its rest area. At 0 only inversion is charged, and a triangle squashed flat costs nothing - which is the configuration the solver reaches for once the inversions are gone. Keep it well below 1: a terminal asked to scale by 0.5 legitimately halves the area of its triangles, and a floor above that argues with the terminal-scale energy.")]
        private float minAreaRatio = 0.0f;

        public override string name => "outputMeshQuality";

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new OutputMeshQualityInstance(this, deformation);

        public class OutputMeshQualityInstance : Instance
        {
            private readonly EDOutputMeshQualityTerm qualityTerm;

            // Everything below is derived from the deformation's output mesh by Reset and is null
            // until that has found one.
            private Vector3[]           restVertices;
            private int[]               triangles;
            private DVector3[]          restNormals;
            private double[]            restAreas;
            private EDVertexBinding[]   bindings;
            private int[][]             nodeVertices;
            private int[][]             nodeTriangles;
            private double              restTotalArea;
            private bool                throughField;
            private bool                warnedMissing;

            public OutputMeshQualityInstance(EDOutputMeshQualityTerm term, EmbededDeformation deformation)
                : base(term, deformation)
            {
                qualityTerm = term;
            }

            public int triangleCount => (triangles != null) ? (triangles.Length / 3) : (0);
            public int vertexCount => (restVertices != null) ? (restVertices.Length) : (0);

            /// <summary>The area of the measured mesh at rest, which weights each triangle's share of the row.</summary>
            public double restArea => restTotalArea;

            /// <summary>
            /// Takes the output mesh off the deformation and works out, once, which nodes reach which
            /// vertices. That is what the Jacobian's sparsity rests on, and it is exact rather than
            /// approximate: the field is indexed by rest position and a binding is fixed at build, so
            /// no node outside a vertex's influence set can move it under any state.
            ///
            /// The influence sets are a superset of what the blend actually uses - a node the polar
            /// blend skips for an invalid decomposition is still listed here - which costs a little
            /// work and cannot cost a wrong column.
            /// </summary>
            public override void Reset()
            {
                restVertices = null;
                triangles = null;
                restNormals = null;
                restAreas = null;
                bindings = null;
                nodeVertices = null;
                nodeTriangles = null;
                restTotalArea = 0.0;

                if (!deformation.hasOutputMesh) return;
                if ((deformation.nodes == null) || (deformation.nodes.Count == 0)) return;

                warnedMissing = false;

                restVertices = deformation.outputMeshVertices;
                triangles = deformation.outputMeshTriangles;
                throughField = deformation.usesDeformationFieldForOutput;

                int vertexCount = restVertices.Length;
                int triangleCount = triangles.Length / 3;
                int nodeCount = deformation.nodes.Count;

                restNormals = new DVector3[triangleCount];
                restAreas = new double[triangleCount];

                for (int t = 0; t < triangleCount; t++)
                {
                    DVector3 r0 = restVertices[triangles[3 * t + 0]].ToDVector3();
                    DVector3 r1 = restVertices[triangles[3 * t + 1]].ToDVector3();
                    DVector3 r2 = restVertices[triangles[3 * t + 2]].ToDVector3();

                    DVector3 n = DVector3.Cross(r1 - r0, r2 - r0);

                    // A triangle with no area at rest has no orientation to lose. Left at zero, which
                    // scores nothing whatever the deformation does to it - and contributes nothing to
                    // the rest area either, so the two stay consistent.
                    if (n.sqrMagnitude <= 1e-20)
                    {
                        restNormals[t] = DVector3.zero;
                        continue;
                    }

                    double doubleArea = n.magnitude;

                    restNormals[t] = n / doubleArea;
                    restAreas[t] = 0.5 * doubleArea;
                    restTotalArea += restAreas[t];
                }

                var perNode = new List<int>[nodeCount];

                for (int n = 0; n < nodeCount; n++) perNode[n] = new List<int>();

                if (throughField)
                {
                    FullDeformationField field = deformation.GetDeformationField();

                    for (int v = 0; v < vertexCount; v++)
                    {
                        if (!field.TryGetTrilinearInfluences(restVertices[v], out int[] nodeIds, out float[] _)) continue;

                        for (int i = 0; i < nodeIds.Length; i++)
                        {
                            if ((nodeIds[i] < 0) || (nodeIds[i] >= nodeCount)) continue;

                            perNode[nodeIds[i]].Add(v);
                        }
                    }
                }
                else
                {
                    bindings = new EDVertexBinding[vertexCount];

                    for (int v = 0; v < vertexCount; v++)
                    {
                        bindings[v] = deformation.BindPoint(restVertices[v]);

                        int[] nodeIndices = bindings[v].nodeIndices;
                        double[] weights = bindings[v].weights;

                        if (nodeIndices == null) continue;

                        for (int i = 0; i < nodeIndices.Length; i++)
                        {
                            if ((nodeIndices[i] < 0) || (nodeIndices[i] >= nodeCount)) continue;
                            if ((weights != null) && (i < weights.Length) && (weights[i] == 0.0)) continue;

                            perNode[nodeIndices[i]].Add(v);
                        }
                    }
                }

                // Triangles per vertex, as a compact adjacency, so the triangles a node reaches can
                // be collected by walking its vertices.
                var vertexTriangleCount = new int[vertexCount + 1];

                for (int i = 0; i < triangles.Length; i++) vertexTriangleCount[triangles[i] + 1]++;
                for (int v = 0; v < vertexCount; v++) vertexTriangleCount[v + 1] += vertexTriangleCount[v];

                var vertexTriangles = new int[triangles.Length];
                var fill = new int[vertexCount];

                for (int i = 0; i < triangles.Length; i++)
                {
                    int v = triangles[i];

                    vertexTriangles[vertexTriangleCount[v] + fill[v]] = i / 3;
                    fill[v]++;
                }

                nodeVertices = new int[nodeCount][];
                nodeTriangles = new int[nodeCount][];

                // Stamped rather than cleared between nodes: a triangle is taken the first time a
                // node's walk reaches it, in ascending vertex order, so the list is deterministic.
                var seen = new int[triangleCount];

                for (int t = 0; t < triangleCount; t++) seen[t] = -1;

                for (int n = 0; n < nodeCount; n++)
                {
                    nodeVertices[n] = perNode[n].ToArray();

                    var reached = new List<int>();

                    for (int k = 0; k < nodeVertices[n].Length; k++)
                    {
                        int v = nodeVertices[n][k];

                        for (int j = vertexTriangleCount[v]; j < vertexTriangleCount[v + 1]; j++)
                        {
                            int t = vertexTriangles[j];

                            if (seen[t] == n) continue;

                            seen[t] = n;
                            reached.Add(t);
                        }
                    }

                    nodeTriangles[n] = reached.ToArray();
                }
            }

            /// <summary>
            /// One row when there is a mesh, none otherwise. The warning is for the case that would
            /// otherwise be silent: a weight on the asset, nothing to measure, and a solve that runs
            /// exactly as though the term were not there.
            /// </summary>
            protected override int ComputeRowCount()
            {
                if ((triangles == null) || (triangles.Length < 3))
                {
                    if ((term.conceptualWeight > 0.0f) && (!warnedMissing))
                    {
                        warnedMissing = true;

                        Debug.LogWarning("[ED] The output mesh quality energy has a weight but the deformation carries no output mesh to measure, so it contributes no rows. The owner supplies the mesh before a solve; if this is Run Iteration after a domain reload, press Update Deformation once.");
                    }

                    return 0;
                }

                // A mesh with no area is a mesh with nothing to invert, and dividing by it would
                // make every residual infinite rather than zero.
                if (restTotalArea <= 0.0)
                {
                    if ((term.conceptualWeight > 0.0f) && (!warnedMissing))
                    {
                        warnedMissing = true;

                        Debug.LogWarning("[ED] The output mesh quality energy found a mesh with no rest area, so it contributes no rows.");
                    }

                    return 0;
                }

                return 1;
            }

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                residual[rowOffset] = residualWeight * MeasureInversion(state, out _, out _, out _);
            }

            /// <summary>
            /// The row's unweighted value at a state - the area-weighted RMS of the per-triangle
            /// shortfalls below the floor - together with what it is made of: how many triangles
            /// have actually turned over and how much area that is, and how many sit below the floor
            /// without having turned over. Public for the report after a solve, which wants the
            /// counts beside the number the energy saw: the inversion count is what the thesis
            /// quotes, and the two have to be read off the same triangles.
            /// </summary>
            public double MeasureInversion(EDStateView state, out int invertedTriangles, out double invertedArea, out int squashedTriangles)
            {
                invertedTriangles = 0;
                invertedArea = 0.0;
                squashedTriangles = 0;

                if (triangles == null) return 0.0;

                FullDeformationField.TransformBlender blender = (throughField) ? (deformation.CreateFieldBlender(state)) : (null);

                var positions = new DVector3[restVertices.Length];

                DeformAll(state, blender, positions);

                // Summed serially in triangle order, so the number does not depend on how the
                // deform above was scheduled.
                double meanSquare = 0.0;

                int triangleCount = triangles.Length / 3;

                for (int t = 0; t < triangleCount; t++)
                {
                    double signedArea = SignedArea(t, positions);

                    if (signedArea < 0.0)
                    {
                        invertedArea -= signedArea;
                        invertedTriangles++;
                    }

                    double shortfall = Shortfall(t, signedArea);

                    if (shortfall <= 0.0) continue;

                    if (signedArea >= 0.0) squashedTriangles++;

                    meanSquare += Contribution(t, shortfall);
                }

                return Math.Sqrt(meanSquare);
            }

            /// <summary>
            /// How far a triangle's area sits below its floor, in area units: rho times its rest
            /// area, less its signed area, when that is positive. At rho = 0 this is simply the
            /// inverted area. A triangle with no rest area has no floor and no shortfall.
            /// </summary>
            private double Shortfall(int t, double signedArea)
            {
                double shortfall = (qualityTerm.minAreaRatio * restAreas[t]) - signedArea;

                return (shortfall > 0.0) ? (shortfall) : (0.0);
            }

            /// <summary>
            /// A triangle's share of the mean square: its shortfall as a fraction of its rest area,
            /// squared, weighted by its share of the rest area. (shortfall / A_t)^2 * (A_t / A), with
            /// one A_t cancelled.
            /// </summary>
            private double Contribution(int t, double shortfall)
                => ((shortfall > 0.0) && (restAreas[t] > 0.0)) ? ((shortfall * shortfall) / (restAreas[t] * restTotalArea)) : (0.0);

            /// <summary>
            /// Every output vertex under a state. The blender is shared read-only across the
            /// workers, which is the arrangement it is built for; nothing overrides on it here.
            /// </summary>
            private void DeformAll(EDStateView state, FullDeformationField.TransformBlender blender, DVector3[] positions)
            {
                Parallel.For(0, restVertices.Length, EDDiagnostics.parallelOptions, v =>
                {
                    positions[v] = DeformVertex(v, state, blender);
                });
            }

            private DVector3 DeformVertex(int v, EDStateView state, FullDeformationField.TransformBlender blender)
            {
                EDVertexBinding binding = (bindings != null) ? (bindings[v]) : (default);

                return deformation.DeformOutputPoint(restVertices[v], binding, state, blender).ToDVector3();
            }

            /// <summary>
            /// A triangle's deformed area projected onto its rest normal: its rest area at rest, zero
            /// when squashed flat, negative when it has turned over. A triangle that has merely
            /// tilted keeps a positive value; only one that has turned over reads negative. Zero for
            /// a triangle with no rest area, which has no orientation to measure against.
            /// </summary>
            private double SignedArea(int t, DVector3[] positions)
            {
                DVector3 n = restNormals[t];

                if (n.sqrMagnitude == 0.0) return 0.0;

                DVector3 p0 = positions[triangles[3 * t + 0]];
                DVector3 p1 = positions[triangles[3 * t + 1]];
                DVector3 p2 = positions[triangles[3 * t + 2]];

                return 0.5 * DVector3.Dot(DVector3.Cross(p1 - p0, p2 - p0), n);
            }

            /// <summary>
            /// As above, with the vertices a column has moved read from the worker's scratch and the
            /// rest from the base pass.
            /// </summary>
            private double SignedArea(int t, DVector3[] basePositions, ColumnScratch scratch)
            {
                DVector3 n = restNormals[t];

                if (n.sqrMagnitude == 0.0) return 0.0;

                DVector3 p0 = scratch.Position(triangles[3 * t + 0], basePositions);
                DVector3 p1 = scratch.Position(triangles[3 * t + 1], basePositions);
                DVector3 p2 = scratch.Position(triangles[3 * t + 2], basePositions);

                return 0.5 * DVector3.Dot(DVector3.Cross(p1 - p0, p2 - p0), n);
            }

            /// <summary>
            /// Per-worker state for the column loop: a blender to override on, and the perturbed
            /// positions of whichever vertices the current column moved.
            ///
            /// The positions are kept in a slot array sized to the largest influence set rather than
            /// one the size of the mesh, and looked up through a stamp per vertex, so nothing is
            /// cleared between columns and a worker costs a few megabytes rather than a copy of the
            /// whole mesh.
            /// </summary>
            private sealed class ColumnScratch
            {
                public readonly FullDeformationField.TransformBlender blender;

                private readonly DVector3[] slotPositions;
                private readonly int[]      vertexSlot;
                private readonly int[]      vertexStamp;
                private int                 stamp;

                public ColumnScratch(FullDeformationField.TransformBlender blender, int vertexCount, int largestInfluenceSet)
                {
                    this.blender = blender;

                    slotPositions = new DVector3[Math.Max(1, largestInfluenceSet)];
                    vertexSlot = new int[vertexCount];
                    vertexStamp = new int[vertexCount];
                }

                public void BeginColumn() => stamp++;

                public void Set(int vertex, int slot, DVector3 position)
                {
                    slotPositions[slot] = position;
                    vertexSlot[vertex] = slot;
                    vertexStamp[vertex] = stamp;
                }

                public DVector3 Position(int vertex, DVector3[] basePositions)
                    => (vertexStamp[vertex] == stamp) ? (slotPositions[vertexSlot[vertex]]) : (basePositions[vertex]);
            }

            /// <summary>
            /// The one row, by one finite difference per parameter, a node's twelve columns at a time.
            ///
            /// The base positions and the base contribution of every triangle to the mean square are
            /// computed once. A column then re-deforms only the vertices its node reaches and
            /// re-measures only the triangles those vertices touch, accumulating the *change* in the
            /// mean square rather than re-summing the whole mesh - so the difference is taken between
            /// two small numbers rather than between two large ones that agree to most of their
            /// digits. The root is taken the same way: r1 - r0 is formed as dQ / (sqrt(Q0 + dQ) +
            /// sqrt(Q0)), never as the difference of two roots.
            ///
            /// Nodes are handed to workers whole, one blender each, because the override is what
            /// carries the perturbation on the field path and a blender with an override belongs to
            /// one thread. Every column lands in its own slot and the norm is summed serially in
            /// column order afterwards, for the reason the energy model's parallel fill gives.
            /// </summary>
            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                if (triangles == null) return;

                var baseView = new EDStateView(state);

                FullDeformationField.TransformBlender baseBlender = (throughField) ? (deformation.CreateFieldBlender(baseView)) : (null);

                var basePositions = new DVector3[restVertices.Length];

                DeformAll(baseView, baseBlender, basePositions);

                int triangleCount = triangles.Length / 3;

                var baseContribution = new double[triangleCount];

                double baseMeanSquare = 0.0;

                for (int t = 0; t < triangleCount; t++)
                {
                    baseContribution[t] = Contribution(t, Shortfall(t, SignedArea(t, basePositions)));
                    baseMeanSquare += baseContribution[t];
                }

                // Nothing below the floor: the row is flat and stays at the zeros the Jacobian was
                // allocated with. One-sided, like the determinant floor - and it is also the one
                // point where the root is not differentiable, so there is no gradient to take.
                if (baseMeanSquare <= 0.0) return;

                int nodeCount = deformation.nodes.Count;
                int columnCount = 12 * nodeCount;

                int largestInfluenceSet = 0;

                for (int node = 0; node < nodeCount; node++)
                    largestInfluenceSet = Math.Max(largestInfluenceSet, nodeVertices[node].Length);

                var columns = new double[columnCount];

                double w = residualWeight;
                double step = Math.Max(qualityTerm.finiteDifferenceStep, 1e-9);

                Parallel.For(
                    0,
                    nodeCount,
                    EDDiagnostics.parallelOptions,

                    () => new ColumnScratch((throughField) ? (deformation.CreateFieldBlender(baseView)) : (null), restVertices.Length, largestInfluenceSet),

                    (n, loopState, scratch) =>
                    {
                        FillNodeColumns(n, state, basePositions, baseContribution, baseMeanSquare, scratch, columns, w, step);

                        return scratch;
                    },

                    scratch => { }
                );

                for (int col = 0; col < columnCount; col++)
                {
                    double value = columns[col];

                    jacobian[rowOffset, col] = value;
                    jacobianNormSq += value * value;
                }
            }

            private void FillNodeColumns(int n, EDState state, DVector3[] basePositions, double[] baseContribution, double baseMeanSquare, ColumnScratch scratch, double[] columns, double w, double step)
            {
                int[] vertices = nodeVertices[n];
                int[] reached = nodeTriangles[n];

                if (vertices.Length == 0) return;

                double r0 = Math.Sqrt(baseMeanSquare);

                // A node whose triangles are all above the floor at the base state gets zero
                // columns without being measured. A perturbation could in principle take one of
                // them below by a sliver, but the derivative that would record is a sliver over the
                // step - noise, not a gradient - and skipping is what keeps a clean piece cheap.
                bool anyBelowFloor = false;

                for (int k = 0; k < reached.Length; k++)
                {
                    if (baseContribution[reached[k]] > 0.0)
                    {
                        anyBelowFloor = true;
                        break;
                    }
                }

                if (!anyBelowFloor) return;

                for (int k = 0; k < 12; k++)
                {
                    int col = 12 * n + k;

                    double original = state.Get(col);
                    double eps = step * Math.Max(1.0, Math.Abs(original));

                    var modifiedState = new EDStateView(state, col, eps);

                    scratch.BeginColumn();

                    if (scratch.blender != null)
                    {
                        // Twelve consecutive parameters belong to one node, so only this node's frame
                        // changes for this perturbation - the same arrangement clearance relies on.
                        scratch.blender.SetNodeOverride(n, deformation.GetNodeFrame(n, modifiedState));
                    }

                    try
                    {
                        for (int i = 0; i < vertices.Length; i++)
                            scratch.Set(vertices[i], i, DeformVertex(vertices[i], modifiedState, scratch.blender));

                        double deltaMeanSquare = 0.0;

                        for (int i = 0; i < reached.Length; i++)
                        {
                            int t = reached[i];

                            deltaMeanSquare += Contribution(t, Shortfall(t, SignedArea(t, basePositions, scratch))) - baseContribution[t];
                        }

                        // sqrt(Q0 + dQ) - sqrt(Q0), written as dQ over the sum of the roots. A
                        // perturbation can only take the mean square below zero by rounding, and
                        // there the row is at its floor.
                        double perturbedMeanSquare = Math.Max(0.0, baseMeanSquare + deltaMeanSquare);

                        double deltaRoot = deltaMeanSquare / (Math.Sqrt(perturbedMeanSquare) + r0);

                        columns[col] = w * deltaRoot / eps;
                    }
                    finally
                    {
                        // Total, so it is safe whether or not the Set above ran.
                        scratch.blender?.ClearNodeOverride();
                    }
                }
            }
        }
#endif
    }
}
#endif

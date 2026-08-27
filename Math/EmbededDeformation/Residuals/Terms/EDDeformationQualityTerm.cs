using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Penalises samples of the deformation that have turned over or squashed.
    ///
    /// Every other energy is a statement about the graph - node frames, segment lengths, probe
    /// normals - and the failures this method has are the ones none of those measure: the node
    /// determinants say one thing and the geometry several sampling distances away does another.
    /// The terms built on this base look at the deformation *map itself*, sampled by a set of
    /// simplices with a rest measure - triangles of the output mesh with their rest areas, or
    /// tetrahedra laid over the field's voxels with their rest volumes - and read the signed measure
    /// of every simplex after deformation against what it had at rest. A triangle is a
    /// finite-difference stencil of the map restricted to the surface, a tetrahedron one of the map
    /// in the volume; either way a simplex whose signed measure has gone negative has turned over,
    /// and one whose measure has shrunk towards zero has been squashed.
    ///
    /// Each simplex s is charged how far its measure ratio a_s / A_s has fallen below a floor,
    /// r_s = max(0, rho - a_s / A_s). The rows are the root mean square of those, weighted by each
    /// simplex's share of the rest measure, either over the whole sample set in one row,
    ///
    ///     r = sqrt( sum_s (A_s / A) * r_s^2 ),
    ///
    /// or over the simplices clustered by the node carrying the most weight at their centroid, one
    /// row per node. The energy is the same either way: with normalised weights a per-node row is
    /// scaled by the node count so the sum of r_k^2 / K is the global mean square, and the weight
    /// found on one form carries to the other unchanged. What the per-node form buys is not energy
    /// but Gauss-Newton rank - one row gives a rank-one J^T J, a single direction the solver can
    /// follow, where K rows let it trade one region against another. Per-simplex rows are the
    /// K = T limit of the same thing and are ruled out by the dense Jacobian.
    ///
    /// **Squared per simplex and then averaged, not averaged and then squared.** The first version
    /// of the mesh term summed the inverted area, divided by the rest area, and let the solver square
    /// the result; on a mesh where two percent of the area is inverted that residual is a few
    /// thousandths and its square a few millionths, and it took a weight of five million to matter.
    /// Squaring a sparse mean punishes sparsity quadratically - by Jensen the mean of the squares is
    /// never smaller, and for violations confined to a small region it is larger by roughly the
    /// inverse of that region's share. The RMS form puts the weight in the range every other term
    /// uses, because it is the convention every other term uses: weight times the mean squared
    /// violation of a per-element, dimensionless quantity. Dimensionless and measure-weighted, so it
    /// is also independent of piece size and of sampling density.
    ///
    /// **The floor is what a hinge at zero could not see.** With rho = 0 only inversion is charged,
    /// and a simplex squashed to no measure at all scores exactly as a healthy one does - which is
    /// the configuration the solver reached for once the inversions were gone, seen on the terminals
    /// the first time the mesh term ran at a weight that mattered. A floor at a fraction of the rest
    /// measure charges the squash too, with the same shape the determinant floor and the clearance
    /// ratio have. It is a margin rather than a scale limit, and it has to stay well below one,
    /// because a terminal asked to scale by a half legitimately halves the area of its triangles.
    ///
    /// **The orientation reference turns with the map.** A triangle's signed area needs a direction
    /// to be signed against, and a rest normal frozen in world space reads legitimate rotation as
    /// damage: past ~72 degrees of local rotation a healthy triangle scores squashed, past 90
    /// inverted - confirmed by rotating a branch 120 degrees by hand and watching the walls turn
    /// red while the floors did not. So each triangle's reference is its rest normal turned by the
    /// rotation of the blend in force at its rest centroid, re-read for every state measured so
    /// the residual stays a pure function of the state - see TransportNormals. The rotation in the
    /// *corotational* convention, never the full affine - the full affine follows the map through
    /// a fold and would detect nothing, and the exact convention matters just as much: see
    /// EmbededDeformation.TryGetOutputRotation for why the blender's own reflected-polar
    /// convention hides precisely the in-plane folds this measure exists to catch. Tetrahedra need
    /// none of this: a signed volume is intrinsic, which is why only the triangle samplers carry a
    /// reference at all.
    ///
    /// The derivative is by finite differences, since the samples go through the field's blend and
    /// there is nothing analytic to take. What makes that affordable is sparsity: a sample vertex
    /// depends only on the nodes that influence it, and that set is a fact about its rest position,
    /// so each column re-deforms only the vertices its node reaches and re-measures only the
    /// simplices those vertices touch - plus, for triangles, the simplices whose centroid the node
    /// influences, since the reference is read there - accumulating the change in each row's mean
    /// square rather than re-summing the whole set. A node none of whose simplices is below the floor at the base state
    /// contributes zero columns without being measured at all - the one-sided early-out clearance
    /// uses, and what makes the term cheap on a healthy piece.
    ///
    /// A subclass supplies the samples and says which way they are carried; this class does the
    /// rest. The sample set is rebuilt in Reset, so it follows the graph and the mesh, and a term
    /// finding nothing to measure contributes no rows and says so once.
    /// </summary>
    [Serializable]
    public abstract class EDDeformationQualityTerm : EDResidualTerm
    {
        [SerializeField, Min(0.0f), Tooltip("Relative step for the finite-difference derivative. Larger than the 1e-6 the other finite-difference terms use on purpose: the samples are deformed in float, so a vertex a few units from its node moves by about one float ulp per 1e-6 of parameter, and a difference taken at that scale is quantisation noise rather than a derivative.")]
        private float finiteDifferenceStep = 1e-3f;

        [SerializeField, Range(0.0f, 1.0f), Tooltip("A sample is charged once its measure - area for a mesh triangle, volume for a field tetrahedron - falls below this fraction of its rest measure. At 0 only inversion is charged, and a sample squashed flat costs nothing, which is the configuration the solver reaches for once the inversions are gone. Keep it well below 1: a terminal asked to scale by 0.5 legitimately halves the area of its triangles, and a floor above that argues with the terminal-scale energy.")]
        private float minRatio = 0.0f;

        /// <summary>
        /// Sets the floor, exactly as editing the field in the inspector would - the companion of
        /// SetConceptualWeight, for experiment drivers and for the export's debug measures, which
        /// host a term of their own and need its floor to match their setting. Read per
        /// measurement, so it takes effect immediately; the sample set does not depend on it.
        /// </summary>
        public void SetMinRatio(float value) => minRatio = Mathf.Clamp01(value);

        /// <summary>
        /// One row over every sample, or one row per deformation node over the samples whose
        /// centroid that node dominates. A fact about the concrete term rather than a setting, so
        /// the two forms are two entries in the energy model and two lines in the dump.
        /// </summary>
        protected abstract bool perNode { get; }

        /// <summary>
        /// What the measure has to say about one sample at a state, matching the residual's three
        /// cases exactly: unchanged, below the floor without turning over, turned over. A sample
        /// dropped at rest - degenerate, or a stencil with an uninfluenced corner - reads as
        /// healthy, because the residual never charges it either.
        /// </summary>
        public enum SampleState : byte
        {
            Healthy  = 0,
            Squashed = 1,
            Inverted = 2,
        }

#if MATH_NET_AVAILABLE
        public abstract class QualityInstance : Instance
        {
            private readonly EDDeformationQualityTerm   qualityTerm;
            private readonly bool                       normalizeWeights;

            // The sample set and everything derived from it, all built by Reset and null until that
            // has found something to measure.
            private Vector3[]           restVertices;
            private int[]               indices;
            private int                 arity;
            private DVector3[]          restNormals;
            private Vector3[]           restCentroids;
            private EDVertexBinding[]   centroidBindings;
            private double[]            restOrientations;
            private double[]            restMeasures;
            private double              restTotalMeasure;
            private int[]               simplexRow;
            private int                 rowCountK;
            private EDVertexBinding[]   bindings;
            private int[][]             nodeVertices;
            private int[][]             nodeSimplices;
            private bool                throughField;
            private bool                warnedMissing;
            private string              missingReason;

            protected QualityInstance(EDDeformationQualityTerm term, EmbededDeformation deformation, bool normalizeWeights)
                : base(term, deformation)
            {
                qualityTerm = term;

                this.normalizeWeights = normalizeWeights;
            }

            /// <summary>
            /// The samples: rest positions in world space and a flat index list, <paramref name="arity"/>
            /// indices per simplex - three for triangles, four for tetrahedra. Triangles are
            /// oriented by the normal they have at rest; tetrahedra by their rest signed volume, so
            /// their vertex order need not be consistent. False with a reason when there is nothing
            /// to measure, which the instance reports once rather than silently contributing no rows.
            /// </summary>
            protected abstract bool TryBuildSamples(out Vector3[] vertices, out int[] indices, out int arity, out string reason);

            /// <summary>Whether the samples are carried through the field rather than through bindings.</summary>
            protected abstract bool samplesThroughField { get; }

            /// <summary>
            /// Whether a simplex with a vertex nothing influences is still measured. The output mesh
            /// really does leave such a vertex at rest, so measuring it is measuring the output; a
            /// stencil laid over the field has no such excuse, and a stencil with one rest corner and
            /// three deformed ones would report a fold the deformation never made.
            /// </summary>
            protected abstract bool measureUninfluenced { get; }

            /// <summary>"triangles" or "tetrahedra", for the readouts.</summary>
            public abstract string sampleLabel { get; }

            /// <summary>
            /// "area" or "volume", for the readouts. A fact about the sampler class rather than
            /// derived from arity, which is data - an instance whose samples have not built yet
            /// still labels its columns correctly.
            /// </summary>
            public abstract string measureLabel { get; }

            /// <summary>Anything a sampler wants appended to its readout.</summary>
            protected virtual string describeNote => string.Empty;

            public int simplexCount => (indices != null) ? (indices.Length / Math.Max(1, arity)) : (0);
            public int vertexCount => (restVertices != null) ? (restVertices.Length) : (0);

            /// <summary>The measure of the whole sample set at rest, which weights each simplex's share of a row.</summary>
            public double restMeasure => restTotalMeasure;

            /// <summary>How many rows this form puts: one, or one per node.</summary>
            public int rows => rowCountK;

            /// <summary>
            /// Takes the samples from the subclass and works out, once, everything the residual and
            /// the Jacobian read: rest measures and orientations, which nodes reach which vertices,
            /// which row each simplex belongs to, and the per-node vertex and simplex lists the
            /// sparse columns walk.
            ///
            /// The influence sets are exact rather than approximate: the field is indexed by rest
            /// position and a binding is fixed at build, so no node outside a vertex's set can move
            /// it under any state. They are a superset of what the blend actually uses - a node the
            /// polar blend skips for an invalid decomposition is still listed here - which costs a
            /// little work and cannot cost a wrong column.
            /// </summary>
            public override void Reset()
            {
                restVertices = null;
                indices = null;
                arity = 0;
                restNormals = null;
                restCentroids = null;
                centroidBindings = null;
                restOrientations = null;
                restMeasures = null;
                restTotalMeasure = 0.0;
                simplexRow = null;
                rowCountK = 0;
                bindings = null;
                nodeVertices = null;
                nodeSimplices = null;
                missingReason = null;

                if ((deformation.nodes == null) || (deformation.nodes.Count == 0))
                {
                    missingReason = "the deformation graph has not been built";
                    return;
                }

                if (!TryBuildSamples(out Vector3[] vertices, out int[] sampleIndices, out int sampleArity, out string reason))
                {
                    missingReason = reason;
                    return;
                }

                if ((vertices == null) || (sampleIndices == null) || (vertices.Length == 0) || (sampleIndices.Length < sampleArity) || ((sampleArity != 3) && (sampleArity != 4)))
                {
                    missingReason = "the sampler produced no simplices";
                    return;
                }

                throughField = samplesThroughField;

                if ((throughField) && (deformation.GetDeformationField() == null))
                {
                    missingReason = "the samples are carried through the deformation field and there is none - Build() has not run since the last reload";
                    return;
                }

                warnedMissing = false;

                restVertices = vertices;
                indices = sampleIndices;
                arity = sampleArity;

                int vertexCount = restVertices.Length;
                int simplexCount = indices.Length / arity;
                int nodeCount = deformation.nodes.Count;

                // Which nodes reach each vertex - and whether any do at all.
                var perNode = new List<int>[nodeCount];

                for (int n = 0; n < nodeCount; n++) perNode[n] = new List<int>();

                var influenced = new bool[vertexCount];

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
                            influenced[v] = true;
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
                            influenced[v] = true;
                        }
                    }
                }

                // Rest measures and orientations. A simplex with no measure at rest has no
                // orientation to lose and contributes nothing, to the rows or to the total; one with
                // an uninfluenced vertex is dropped the same way when the sampler says so.
                restMeasures = new double[simplexCount];
                restNormals = (arity == 3) ? (new DVector3[simplexCount]) : (null);
                restOrientations = (arity == 4) ? (new double[simplexCount]) : (null);

                int droppedUninfluenced = 0;
                int droppedDegenerate = 0;

                for (int s = 0; s < simplexCount; s++)
                {
                    bool allInfluenced = true;

                    for (int i = 0; i < arity; i++)
                        allInfluenced &= influenced[indices[arity * s + i]];

                    if ((!allInfluenced) && (!measureUninfluenced))
                    {
                        droppedUninfluenced++;
                        continue;
                    }

                    if (arity == 3)
                    {
                        DVector3 r0 = restVertices[indices[3 * s + 0]].ToDVector3();
                        DVector3 r1 = restVertices[indices[3 * s + 1]].ToDVector3();
                        DVector3 r2 = restVertices[indices[3 * s + 2]].ToDVector3();

                        DVector3 n = DVector3.Cross(r1 - r0, r2 - r0);

                        if (n.sqrMagnitude <= 1e-20)
                        {
                            droppedDegenerate++;
                            continue;
                        }

                        double doubleArea = n.magnitude;

                        restNormals[s] = n / doubleArea;
                        restMeasures[s] = 0.5 * doubleArea;
                    }
                    else
                    {
                        DVector3 r0 = restVertices[indices[4 * s + 0]].ToDVector3();
                        DVector3 r1 = restVertices[indices[4 * s + 1]].ToDVector3();
                        DVector3 r2 = restVertices[indices[4 * s + 2]].ToDVector3();
                        DVector3 r3 = restVertices[indices[4 * s + 3]].ToDVector3();

                        double signedVolume = DVector3.Dot(r1 - r0, DVector3.Cross(r2 - r0, r3 - r0)) / 6.0;

                        if (Math.Abs(signedVolume) <= 1e-30)
                        {
                            droppedDegenerate++;
                            continue;
                        }

                        // The rest orientation is whatever the vertex order gave; it is folded into
                        // the sign rather than into the order, so the sampler's arrays are not
                        // rewritten under it.
                        restOrientations[s] = (signedVolume < 0.0) ? (-1.0) : (1.0);
                        restMeasures[s] = Math.Abs(signedVolume);
                    }

                    restTotalMeasure += restMeasures[s];
                }

                // Where the per-node rows ask for their dominant node - and, for triangles, where
                // the orientation reference is read. Rest positions, because everything the blend
                // is indexed by is.
                restCentroids = new Vector3[simplexCount];

                for (int s = 0; s < simplexCount; s++) restCentroids[s] = Centroid(s);

                // On the binding path the centroid's binding is a fact about the mesh, so it is
                // bound once here rather than on every residual evaluation.
                if ((arity == 3) && (!throughField))
                {
                    centroidBindings = new EDVertexBinding[simplexCount];

                    for (int s = 0; s < simplexCount; s++)
                        centroidBindings[s] = deformation.BindPoint(restCentroids[s]);
                }

                // Which simplices each node moves through the orientation reference alone. The
                // reference rotation is read at the rest centroid, and a triangle spanning a cell
                // corner can put its centroid in a cell none of its vertices is in - so a node can
                // turn a triangle's reference while reaching none of its vertices, and its columns
                // have to re-measure that triangle or the Jacobian silently drops the reference's
                // own sensitivity. Triangles only; a tetrahedron has no reference to transport.
                List<int>[] centroidInfluences = null;

                if (arity == 3)
                {
                    centroidInfluences = new List<int>[nodeCount];

                    for (int n = 0; n < nodeCount; n++) centroidInfluences[n] = new List<int>();

                    for (int s = 0; s < simplexCount; s++)
                    {
                        if (restMeasures[s] <= 0.0) continue;

                        if (throughField)
                        {
                            if (!deformation.GetDeformationField().TryGetTrilinearInfluences(restCentroids[s], out int[] nodeIds, out float[] _)) continue;

                            for (int i = 0; i < nodeIds.Length; i++)
                            {
                                if ((nodeIds[i] < 0) || (nodeIds[i] >= nodeCount)) continue;

                                if ((centroidInfluences[nodeIds[i]].Count == 0) || (centroidInfluences[nodeIds[i]][centroidInfluences[nodeIds[i]].Count - 1] != s))
                                    centroidInfluences[nodeIds[i]].Add(s);
                            }
                        }
                        else
                        {
                            int[] nodeIndices = centroidBindings[s].nodeIndices;
                            double[] weights = centroidBindings[s].weights;

                            if (nodeIndices == null) continue;

                            for (int i = 0; i < nodeIndices.Length; i++)
                            {
                                if ((nodeIndices[i] < 0) || (nodeIndices[i] >= nodeCount)) continue;
                                if ((weights != null) && (i < weights.Length) && (weights[i] == 0.0)) continue;

                                if ((centroidInfluences[nodeIndices[i]].Count == 0) || (centroidInfluences[nodeIndices[i]][centroidInfluences[nodeIndices[i]].Count - 1] != s))
                                    centroidInfluences[nodeIndices[i]].Add(s);
                            }
                        }
                    }
                }

                // Which row each simplex belongs to.
                simplexRow = new int[simplexCount];

                if (qualityTerm.perNode)
                {
                    rowCountK = nodeCount;

                    for (int s = 0; s < simplexCount; s++)
                        simplexRow[s] = DominantNodeAt(s, nodeCount);
                }
                else
                {
                    rowCountK = 1;
                }

                // Simplices per vertex, as a compact adjacency, so the simplices a node reaches can
                // be collected by walking its vertices.
                var vertexSimplexCount = new int[vertexCount + 1];

                for (int i = 0; i < indices.Length; i++) vertexSimplexCount[indices[i] + 1]++;
                for (int v = 0; v < vertexCount; v++) vertexSimplexCount[v + 1] += vertexSimplexCount[v];

                var vertexSimplices = new int[indices.Length];
                var fill = new int[vertexCount];

                for (int i = 0; i < indices.Length; i++)
                {
                    int v = indices[i];

                    vertexSimplices[vertexSimplexCount[v] + fill[v]] = i / arity;
                    fill[v]++;
                }

                nodeVertices = new int[nodeCount][];
                nodeSimplices = new int[nodeCount][];

                // Stamped rather than cleared between nodes: a simplex is taken the first time a
                // node's walk reaches it, in ascending vertex order, so the list is deterministic.
                var seen = new int[simplexCount];

                for (int s = 0; s < simplexCount; s++) seen[s] = -1;

                for (int n = 0; n < nodeCount; n++)
                {
                    nodeVertices[n] = perNode[n].ToArray();

                    var reached = new List<int>();

                    for (int k = 0; k < nodeVertices[n].Length; k++)
                    {
                        int v = nodeVertices[n][k];

                        for (int j = vertexSimplexCount[v]; j < vertexSimplexCount[v + 1]; j++)
                        {
                            int s = vertexSimplices[j];

                            if (seen[s] == n) continue;

                            seen[s] = n;
                            reached.Add(s);
                        }
                    }

                    // The simplices this node moves only through the orientation reference,
                    // appended after the vertex-derived ones in ascending simplex order, so the
                    // list stays deterministic.
                    if (centroidInfluences != null)
                    {
                        List<int> throughCentroid = centroidInfluences[n];

                        for (int k = 0; k < throughCentroid.Count; k++)
                        {
                            int s = throughCentroid[k];

                            if (seen[s] == n) continue;

                            seen[s] = n;
                            reached.Add(s);
                        }
                    }

                    nodeSimplices[n] = reached.ToArray();
                }

                Debug.Log($"[ED] {term.name}: {simplexCount} {sampleLabel} over {vertexCount} vertices, rest {measureLabel} {restTotalMeasure:F3}, {rowCountK} row(s)" +
                          ((droppedUninfluenced > 0) ? ($", {droppedUninfluenced} dropped for a vertex nothing influences") : ("")) +
                          ((droppedDegenerate > 0) ? ($", {droppedDegenerate} degenerate at rest") : ("")) + ".");
            }


            private Vector3 Centroid(int s)
            {
                Vector3 sum = Vector3.zero;

                for (int i = 0; i < arity; i++) sum += restVertices[indices[arity * s + i]];

                return sum / arity;
            }

            /// <summary>
            /// The node carrying the most weight at a simplex's rest centroid, ties to the lower
            /// index - the same rule the polar blend and SortInfluences use, so a cluster boundary
            /// is a fact about the weights and not about array order. A centroid nothing influences
            /// goes to the nearest node by rest position, so every simplex has a row and the rows
            /// sum to the global mean square exactly.
            /// </summary>
            private int DominantNodeAt(int s, int nodeCount)
            {
                int best = -1;
                double bestWeight = 0.0;

                if (throughField)
                {
                    if (deformation.GetDeformationField().TryGetTrilinearInfluences(restCentroids[s], out int[] nodeIds, out float[] weights))
                    {
                        for (int i = 0; i < nodeIds.Length; i++)
                        {
                            if ((nodeIds[i] < 0) || (nodeIds[i] >= nodeCount)) continue;

                            if ((best < 0) || (weights[i] > bestWeight) || ((weights[i] == bestWeight) && (nodeIds[i] < best)))
                            {
                                best = nodeIds[i];
                                bestWeight = weights[i];
                            }
                        }
                    }
                }
                else
                {
                    EDVertexBinding binding = (centroidBindings != null) ? (centroidBindings[s]) : (deformation.BindPoint(restCentroids[s]));

                    if (binding.nodeIndices != null)
                    {
                        for (int i = 0; i < binding.nodeIndices.Length; i++)
                        {
                            int node = binding.nodeIndices[i];
                            double weight = ((binding.weights != null) && (i < binding.weights.Length)) ? (binding.weights[i]) : (0.0);

                            if ((node < 0) || (node >= nodeCount) || (weight <= 0.0)) continue;

                            if ((best < 0) || (weight > bestWeight) || ((weight == bestWeight) && (node < best)))
                            {
                                best = node;
                                bestWeight = weight;
                            }
                        }
                    }
                }

                if (best < 0) best = Math.Max(0, deformation.GetClosestDebugNodeIndex(restCentroids[s]));

                return best;
            }

            /// <summary>
            /// The rows this form puts, or none. The warning is for the case that would otherwise be
            /// silent: a weight on the asset, nothing to measure, and a solve that runs exactly as
            /// though the term were not there.
            /// </summary>
            protected override int ComputeRowCount()
            {
                if ((indices == null) || (rowCountK == 0))
                {
                    WarnMissing(missingReason ?? "there is nothing to measure");

                    return 0;
                }

                // A sample set with no measure is one with nothing to invert, and dividing by it
                // would make every residual infinite rather than zero.
                if (restTotalMeasure <= 0.0)
                {
                    WarnMissing($"every {sampleLabel.TrimEnd('s')} is degenerate at rest");

                    return 0;
                }

                return rowCountK;
            }

            private void WarnMissing(string why)
            {
                if ((term.conceptualWeight <= 0.0f) || (warnedMissing)) return;

                warnedMissing = true;

                Debug.LogWarning($"[ED] The {term.name} energy has a weight but contributes no rows: {why}.");
            }

            /// <summary>
            /// sqrt(K) on a per-node row under normalised weights, so that sum_k r_k^2 / K is the
            /// global mean square and the energy is the same number the single-row form would give.
            /// One otherwise - the unnormalised energy sums the rows as they are.
            /// </summary>
            private double rowScale => ((qualityTerm.perNode) && (normalizeWeights)) ? (rowCountK) : (1.0);

            public override void EvaluateResidual(EDStateView state, Vector<double> residual, int rowOffset)
            {
                double[] rowMeanSquare = MeasureRows(state, out _, out _, out _);

                if (rowMeanSquare == null) return;

                double scale = rowScale;

                for (int k = 0; k < rowCountK; k++)
                    residual[rowOffset + k] = residualWeight * Math.Sqrt(scale * rowMeanSquare[k]);
            }

            /// <summary>
            /// The mean square of every row at a state, with what it is made of: how many simplices
            /// have actually turned over and how much measure that is, and how many sit below the
            /// floor without having turned over. Null when there is nothing to measure. A caller
            /// that also wants each simplex's own share hands in an array sized to the simplex
            /// count - entries the loop skips stay at the zero the allocation gave them.
            /// </summary>
            private double[] MeasureRows(EDStateView state, out int invertedSimplices, out double invertedMeasure, out int squashedSimplices, double[] perSimplexContribution = null)
            {
                invertedSimplices = 0;
                invertedMeasure = 0.0;
                squashedSimplices = 0;

                if (indices == null) return null;

                FullDeformationField.TransformBlender blender = (throughField) ? (deformation.CreateFieldBlender(state)) : (null);

                var positions = new DVector3[restVertices.Length];

                DeformAll(state, blender, positions);

                DVector3[] referenceNormals = TransportNormals(state, blender);

                // Summed serially in simplex order, so the numbers do not depend on how the deform
                // above was scheduled.
                var rowMeanSquare = new double[rowCountK];

                int simplexCount = indices.Length / arity;

                for (int s = 0; s < simplexCount; s++)
                {
                    if (restMeasures[s] <= 0.0) continue;

                    double signed = SignedMeasure(s, positions, referenceNormals);

                    if (signed < 0.0)
                    {
                        invertedMeasure -= signed;
                        invertedSimplices++;
                    }

                    double shortfall = Shortfall(s, signed);

                    if (shortfall <= 0.0) continue;

                    if (signed >= 0.0) squashedSimplices++;

                    double contribution = Contribution(s, shortfall);

                    if (perSimplexContribution != null) perSimplexContribution[s] = contribution;

                    rowMeanSquare[simplexRow[s]] += contribution;
                }

                return rowMeanSquare;
            }

            /// <summary>
            /// The global RMS at a state - the single-row residual before weighting, whichever form
            /// this is - together with the counts behind it. Public for the report after a solve,
            /// which wants the counts beside the number the energy saw: the inversion count is what
            /// the thesis quotes, and the two have to be read off the same simplices.
            /// </summary>
            public double MeasureInversion(EDStateView state, out int invertedSimplices, out double invertedMeasure, out int squashedSimplices, double[] perSimplexContribution = null)
            {
                double[] rowMeanSquare = MeasureRows(state, out invertedSimplices, out invertedMeasure, out squashedSimplices, perSimplexContribution);

                if (rowMeanSquare == null) return 0.0;

                double total = 0.0;

                for (int k = 0; k < rowMeanSquare.Length; k++) total += rowMeanSquare[k];

                return Math.Sqrt(total);
            }

            /// <summary>
            /// The columns this term adds to the breakdown, labelled in its own vocabulary -
            /// invertedTriangles against invertedTetrahedra, restArea against restVolume - so a CSV
            /// of one term reads in the units the term measures. The trailing notes column carries
            /// describeNote, the caveat about how to read the numbers, when the form has one.
            /// </summary>
            public override string[] DescribeHeader()
            {
                string sample = Capitalize(sampleLabel);
                string measure = Capitalize(measureLabel);

                return new[]
                {
                    $"inverted{sample}",
                    $"total{sample}",
                    $"inverted{measure}",
                    $"rest{measure}",
                    "squashed",
                    "globalRMS",
                    "worst1%share",
                    "notes"
                };
            }

            /// <summary>
            /// What the energy measured at a state, one value per DescribeHeader column.
            /// Re-measures the samples, so it costs a residual evaluation.
            /// </summary>
            public override string[] Describe(EDStateView state)
            {
                int count = simplexCount;

                var contributions = (count > 0) ? (new double[count]) : (null);

                double rms = MeasureInversion(state, out int inverted, out double measure, out int squashed, contributions);

                return new[]
                {
                    inverted.ToString(CultureInfo.InvariantCulture),
                    count.ToString(CultureInfo.InvariantCulture),
                    measure.ToString("F4", CultureInfo.InvariantCulture),
                    restTotalMeasure.ToString("F2", CultureInfo.InvariantCulture),
                    squashed.ToString(CultureInfo.InvariantCulture),
                    rms.ToString("E3", CultureInfo.InvariantCulture),
                    WorstOnePercentShare(contributions),
                    describeNote
                };
            }

            private static string Capitalize(string label)
                => (string.IsNullOrEmpty(label)) ? (label) : (char.ToUpperInvariant(label[0]) + label.Substring(1));

            /// <summary>
            /// How much of the mean square the worst one percent of samples carry - the number that
            /// says whether the energy is measuring the mesh or a handful of catastrophic samples.
            /// The squared depth has no bound below the floor, so a few simplices turned over and
            /// stretched can hold nearly all of it, and a solve paid almost entirely to shrink those
            /// can raise the count of mild ones without the energy noticing - which is what the
            /// 2026-08-24 tables showed, a four-fold energy drop beside a rising inversion count.
            /// Read it before and after any change to the loss shape; it is the number that says the
            /// tail is tamed. Empty when nothing was charged, which the breakdown renders as an
            /// absent column and a CSV as an empty cell. The array arrives as each simplex's own
            /// share of the mean square and leaves sorted, which is fine for the one caller, who
            /// allocated it for this.
            /// </summary>
            private static string WorstOnePercentShare(double[] contributions)
            {
                if (contributions == null) return string.Empty;

                double total = 0.0;

                for (int i = 0; i < contributions.Length; i++) total += contributions[i];

                if (total <= 0.0) return string.Empty;

                Array.Sort(contributions);

                int worst = Math.Max(1, (int)Math.Ceiling(0.01 * contributions.Length));

                double carried = 0.0;

                for (int i = contributions.Length - worst; i < contributions.Length; i++) carried += contributions[i];

                return (100.0 * carried / total).ToString("F1", CultureInfo.InvariantCulture) + "%";
            }

            /// <summary>
            /// The samples at a state, classified exactly as the residual charges them, with the
            /// deformed positions they were measured at - what the debug displays draw, so the
            /// picture is the energy's own answer rather than a re-derivation that could drift from
            /// it. The arity says what came back - three for triangles, four for tetrahedra - and
            /// the caller decides what it can draw with that. The index array is the instance's own;
            /// read it, do not write to it.
            /// </summary>
            public bool TryClassifySamples(EDStateView state, out Vector3[] deformedPositions, out int[] sampleIndices, out int sampleArity, out SampleState[] states)
            {
                deformedPositions = null;
                sampleIndices = null;
                sampleArity = 0;
                states = null;

                if (indices == null) return false;

                FullDeformationField.TransformBlender blender = (throughField) ? (deformation.CreateFieldBlender(state)) : (null);

                var positions = new DVector3[restVertices.Length];

                DeformAll(state, blender, positions);

                DVector3[] referenceNormals = TransportNormals(state, blender);

                int count = indices.Length / arity;

                states = new SampleState[count];

                for (int s = 0; s < count; s++)
                {
                    if (restMeasures[s] <= 0.0) continue;

                    double signed = SignedMeasure(s, positions, referenceNormals);

                    if (signed < 0.0)                    states[s] = SampleState.Inverted;
                    else if (Shortfall(s, signed) > 0.0) states[s] = SampleState.Squashed;
                }

                deformedPositions = new Vector3[positions.Length];

                for (int v = 0; v < positions.Length; v++) deformedPositions[v] = positions[v].ToVector3();

                sampleIndices = indices;
                sampleArity = arity;

                return true;
            }

            /// <summary>
            /// Every sample vertex under a state. The blender is shared read-only across the
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
            /// The orientation references for a state: each triangle's rest normal turned by the
            /// corotational rotation of the blend in force at its rest centroid. Null for tetrahedra,
            /// whose signed volume needs no reference - a volume is intrinsic where a surface
            /// orientation is not.
            ///
            /// Recomputed for whatever state is being measured, never lagged: the residual stays a
            /// pure function of the state, which is what lets two states' energies be compared -
            /// and the finite difference then carries the reference's own sensitivity without any
            /// special-casing.
            /// </summary>
            private DVector3[] TransportNormals(EDStateView state, FullDeformationField.TransformBlender blender)
            {
                if (arity != 3) return null;

                var normals = new DVector3[restNormals.Length];

                Parallel.For(0, restNormals.Length, EDDiagnostics.parallelOptions, s =>
                {
                    normals[s] = TransportedNormal(s, state, blender);
                });

                return normals;
            }

            /// <summary>
            /// One triangle's reference normal under a state, normalised in double - which forgives
            /// the rotation being orthonormal only to float precision. A triangle dropped at rest,
            /// or one whose centroid nothing influences, keeps its rest normal; the latter is
            /// geometry the output leaves at rest. In a Jacobian column this runs against the
            /// perturbed state: for a centroid the perturbed node influences, the reference's
            /// sensitivity belongs in the column, and for any other the blend is unchanged and this
            /// recomputes the base value bit for bit - spent work, never a wrong number.
            /// </summary>
            private DVector3 TransportedNormal(int s, EDStateView state, FullDeformationField.TransformBlender blender)
            {
                if (restMeasures[s] <= 0.0) return restNormals[s];

                EDVertexBinding binding = (centroidBindings != null) ? (centroidBindings[s]) : (default);

                if (!deformation.TryGetOutputRotation(restCentroids[s], in binding, state, blender, out Matrix3x3 rotation))
                    return restNormals[s];

                DVector3 n = restNormals[s];

                return new DVector3((rotation.m00 * n.x) + (rotation.m01 * n.y) + (rotation.m02 * n.z),
                                    (rotation.m10 * n.x) + (rotation.m11 * n.y) + (rotation.m12 * n.z),
                                    (rotation.m20 * n.x) + (rotation.m21 * n.y) + (rotation.m22 * n.z)).normalized;
            }

            /// <summary>
            /// How far a simplex's measure sits below its floor, in measure units: rho times its rest
            /// measure, less its signed measure, when that is positive. At rho = 0 this is simply the
            /// inverted measure.
            /// </summary>
            private double Shortfall(int s, double signedMeasure)
            {
                double shortfall = (qualityTerm.minRatio * restMeasures[s]) - signedMeasure;

                return (shortfall > 0.0) ? (shortfall) : (0.0);
            }

            /// <summary>
            /// A simplex's share of its row's mean square: its shortfall as a fraction of its rest
            /// measure, squared, weighted by its share of the total rest measure.
            /// (shortfall / A_s)^2 * (A_s / A), with one A_s cancelled.
            /// </summary>
            private double Contribution(int s, double shortfall)
                => ((shortfall > 0.0) && (restMeasures[s] > 0.0)) ? ((shortfall * shortfall) / (restMeasures[s] * restTotalMeasure)) : (0.0);

            /// <summary>
            /// A simplex's deformed measure, signed against its rest orientation: the rest measure
            /// at rest, zero when squashed flat, negative when it has turned over. A triangle's
            /// deformed area vector is projected onto its *transported* reference - its rest normal
            /// turned by the corotational rotation of the blend at its centroid, per state, see
            /// TransportNormals - so legitimate local rotation of any angle reads clean and only a
            /// genuine fold goes negative. A tetrahedron's signed volume is taken in its own vertex
            /// order and corrected by the sign that order had at rest, and needs no reference at
            /// all, which is why the parameter is unused there.
            /// </summary>
            private double SignedMeasure(int s, DVector3[] positions, DVector3[] referenceNormals)
            {
                if (arity == 3)
                {
                    DVector3 p0 = positions[indices[3 * s + 0]];
                    DVector3 p1 = positions[indices[3 * s + 1]];
                    DVector3 p2 = positions[indices[3 * s + 2]];

                    return 0.5 * DVector3.Dot(DVector3.Cross(p1 - p0, p2 - p0), referenceNormals[s]);
                }
                else
                {
                    DVector3 p0 = positions[indices[4 * s + 0]];
                    DVector3 p1 = positions[indices[4 * s + 1]];
                    DVector3 p2 = positions[indices[4 * s + 2]];
                    DVector3 p3 = positions[indices[4 * s + 3]];

                    return restOrientations[s] * DVector3.Dot(p1 - p0, DVector3.Cross(p2 - p0, p3 - p0)) / 6.0;
                }
            }

            /// <summary>
            /// As above, with the vertices a column has moved read from the worker's scratch, the
            /// rest from the base pass, and the reference handed in - a column derives it under the
            /// perturbed state, one triangle at a time.
            /// </summary>
            private double SignedMeasure(int s, DVector3[] basePositions, ColumnScratch scratch, in DVector3 referenceNormal)
            {
                if (arity == 3)
                {
                    DVector3 p0 = scratch.Position(indices[3 * s + 0], basePositions);
                    DVector3 p1 = scratch.Position(indices[3 * s + 1], basePositions);
                    DVector3 p2 = scratch.Position(indices[3 * s + 2], basePositions);

                    return 0.5 * DVector3.Dot(DVector3.Cross(p1 - p0, p2 - p0), referenceNormal);
                }
                else
                {
                    DVector3 p0 = scratch.Position(indices[4 * s + 0], basePositions);
                    DVector3 p1 = scratch.Position(indices[4 * s + 1], basePositions);
                    DVector3 p2 = scratch.Position(indices[4 * s + 2], basePositions);
                    DVector3 p3 = scratch.Position(indices[4 * s + 3], basePositions);

                    return restOrientations[s] * DVector3.Dot(p1 - p0, DVector3.Cross(p2 - p0, p3 - p0)) / 6.0;
                }
            }

            /// <summary>
            /// Per-worker state for the column loop: a blender to override on, the perturbed
            /// positions of whichever vertices the current column moved, and the change each row's
            /// mean square has accumulated from them.
            ///
            /// The positions are kept in a slot array sized to the largest influence set rather than
            /// one the size of the sample set, and looked up through a stamp per vertex, so nothing
            /// is cleared between columns and a worker costs a few megabytes rather than a copy of the
            /// whole set. The row deltas are stamped the same way.
            /// </summary>
            private sealed class ColumnScratch
            {
                public readonly FullDeformationField.TransformBlender blender;
                public readonly List<int>   touchedRows = new List<int>();

                private readonly DVector3[] slotPositions;
                private readonly int[]      vertexSlot;
                private readonly int[]      vertexStamp;
                private readonly double[]   rowDelta;
                private readonly int[]      rowStamp;
                private int                 stamp;

                public ColumnScratch(FullDeformationField.TransformBlender blender, int vertexCount, int largestInfluenceSet, int rowCount)
                {
                    this.blender = blender;

                    slotPositions = new DVector3[Math.Max(1, largestInfluenceSet)];
                    vertexSlot = new int[vertexCount];
                    vertexStamp = new int[vertexCount];
                    rowDelta = new double[rowCount];
                    rowStamp = new int[rowCount];
                }

                public void BeginColumn()
                {
                    stamp++;
                    touchedRows.Clear();
                }

                public void Set(int vertex, int slot, DVector3 position)
                {
                    slotPositions[slot] = position;
                    vertexSlot[vertex] = slot;
                    vertexStamp[vertex] = stamp;
                }

                public DVector3 Position(int vertex, DVector3[] basePositions)
                    => (vertexStamp[vertex] == stamp) ? (slotPositions[vertexSlot[vertex]]) : (basePositions[vertex]);

                public void AddRowDelta(int row, double delta)
                {
                    if (rowStamp[row] != stamp)
                    {
                        rowStamp[row] = stamp;
                        rowDelta[row] = 0.0;
                        touchedRows.Add(row);
                    }

                    rowDelta[row] += delta;
                }

                public double RowDelta(int row) => rowDelta[row];
            }

            /// <summary>
            /// The rows, by one finite difference per parameter, a node's twelve columns at a time.
            ///
            /// The base positions and the base contribution of every simplex are computed once. A
            /// column then re-deforms only the vertices its node reaches and re-measures only the
            /// simplices those vertices touch - re-deriving each measured triangle's orientation
            /// reference under the perturbed state on the way - accumulating the *change* in each
            /// row's mean square rather than re-summing the whole set, so the difference is taken
            /// between two small numbers rather than between two large ones that agree to most of
            /// their digits. The
            /// root is taken the same way: r1 - r0 is formed as dQ / (sqrt(Q0 + dQ) + sqrt(Q0)),
            /// never as the difference of two roots.
            ///
            /// Nodes are handed to workers whole, one blender each, because the override is what
            /// carries the perturbation on the field path and a blender with an override belongs to
            /// one thread. Every entry lands in its own slot - a column belongs to exactly one node,
            /// so two workers never write the same one - and the norm is summed serially in row and
            /// column order afterwards, for the reason the energy model's parallel fill gives.
            /// </summary>
            public override void FillJacobian(EDState state, DenseMatrix jacobian, int rowOffset, ref double jacobianNormSq)
            {
                if (indices == null) return;

                var baseView = new EDStateView(state);

                FullDeformationField.TransformBlender baseBlender = (throughField) ? (deformation.CreateFieldBlender(baseView)) : (null);

                var basePositions = new DVector3[restVertices.Length];

                DeformAll(baseView, baseBlender, basePositions);

                DVector3[] baseNormals = TransportNormals(baseView, baseBlender);

                int simplexCount = indices.Length / arity;

                var baseContribution = new double[simplexCount];
                var baseRowMeanSquare = new double[rowCountK];

                double baseTotal = 0.0;

                for (int s = 0; s < simplexCount; s++)
                {
                    if (restMeasures[s] <= 0.0) continue;

                    baseContribution[s] = Contribution(s, Shortfall(s, SignedMeasure(s, basePositions, baseNormals)));
                    baseRowMeanSquare[simplexRow[s]] += baseContribution[s];
                    baseTotal += baseContribution[s];
                }

                // Nothing below the floor anywhere: every row is flat and stays at the zeros the
                // Jacobian was allocated with. One-sided, like the determinant floor - and it is
                // also the one point where the root is not differentiable, so there is no gradient
                // to take.
                if (baseTotal <= 0.0) return;

                int nodeCount = deformation.nodes.Count;
                int columnCount = 12 * nodeCount;

                int largestInfluenceSet = 0;

                for (int node = 0; node < nodeCount; node++)
                    largestInfluenceSet = Math.Max(largestInfluenceSet, nodeVertices[node].Length);

                var entries = new double[rowCountK * columnCount];

                double w = residualWeight;
                double scale = rowScale;
                double step = Math.Max(qualityTerm.finiteDifferenceStep, 1e-9);

                Parallel.For(
                    0,
                    nodeCount,
                    EDDiagnostics.parallelOptions,

                    () => new ColumnScratch((throughField) ? (deformation.CreateFieldBlender(baseView)) : (null), restVertices.Length, largestInfluenceSet, rowCountK),

                    (n, loopState, scratch) =>
                    {
                        FillNodeColumns(n, state, basePositions, baseContribution, baseRowMeanSquare, scratch, entries, columnCount, w, scale, step);

                        return scratch;
                    },

                    scratch => { }
                );

                for (int k = 0; k < rowCountK; k++)
                {
                    int rowBase = k * columnCount;

                    for (int col = 0; col < columnCount; col++)
                    {
                        double value = entries[rowBase + col];

                        if (value == 0.0) continue;

                        jacobian[rowOffset + k, col] = value;
                        jacobianNormSq += value * value;
                    }
                }
            }

            private void FillNodeColumns(int n, EDState state, DVector3[] basePositions, double[] baseContribution, double[] baseRowMeanSquare,
                                         ColumnScratch scratch, double[] entries, int columnCount, double w, double scale, double step)
            {
                int[] vertices = nodeVertices[n];
                int[] reached = nodeSimplices[n];

                // On the reached simplices rather than the vertices: a node can move a triangle
                // through its centroid's orientation reference while influencing none of its
                // vertices, and such a node still owes columns.
                if (reached.Length == 0) return;

                // A node whose simplices are all above the floor at the base state gets zero columns
                // without being measured. A perturbation could in principle take one of them below
                // by a sliver, but the derivative that would record is a sliver over the step -
                // noise, not a gradient - and skipping is what keeps a clean piece cheap.
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

                double sqrtScale = Math.Sqrt(scale);

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

                        for (int i = 0; i < reached.Length; i++)
                        {
                            int s = reached[i];

                            if (restMeasures[s] <= 0.0) continue;

                            // The reference derived under the perturbed state, not reused from the
                            // base, so the column carries the reference's own sensitivity - see
                            // TransportedNormal for why that is safe for a node the centroid does
                            // not listen to.
                            DVector3 referenceNormal = (arity == 3) ? (TransportedNormal(s, modifiedState, scratch.blender)) : (default);

                            double delta = Contribution(s, Shortfall(s, SignedMeasure(s, basePositions, scratch, in referenceNormal))) - baseContribution[s];

                            if (delta != 0.0) scratch.AddRowDelta(simplexRow[s], delta);
                        }

                        for (int i = 0; i < scratch.touchedRows.Count; i++)
                        {
                            int row = scratch.touchedRows[i];

                            double baseMeanSquare = baseRowMeanSquare[row];

                            // A row at its floor has no gradient, as the whole term has none when
                            // every row is - the root is not differentiable there, and the number
                            // a one-sided difference would record is the sliver argument above.
                            if (baseMeanSquare <= 0.0) continue;

                            double deltaMeanSquare = scratch.RowDelta(row);

                            // sqrt(Q0 + dQ) - sqrt(Q0), written as dQ over the sum of the roots. A
                            // perturbation can only take the mean square below zero by rounding, and
                            // there the row is at its floor.
                            double perturbedMeanSquare = Math.Max(0.0, baseMeanSquare + deltaMeanSquare);

                            double deltaRoot = sqrtScale * deltaMeanSquare / (Math.Sqrt(perturbedMeanSquare) + Math.Sqrt(baseMeanSquare));

                            entries[(row * columnCount) + col] = w * deltaRoot / eps;
                        }
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

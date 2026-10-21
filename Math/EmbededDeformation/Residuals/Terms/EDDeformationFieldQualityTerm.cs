using System;
using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

#if UC_ENABLE_ED
namespace UC.ED
{
    /// <summary>
    /// Which empty cells a field quality term lays samples over when it accounts for empty space.
    /// </summary>
    public enum EDEmptySpaceRegion
    {
        /// <summary>
        /// The empty cells whose centre lies inside the convex hull of the rest mesh: the space in
        /// the piece's concavities - between a cactus's arms and its trunk - which is where two of
        /// its parts can meet, the segment between any two points of the solid lying inside the
        /// hull. A flat rest mesh has no inside and lays nothing.
        /// </summary>
        InsideRestConvexHull,
        /// <summary>Every empty cell of the field's grid, out to its padding.</summary>
        FullVolume,
    }

    /// <summary>
    /// The deformation-quality measure sampled on the deformation field itself: every occupied
    /// voxel split into six tetrahedra on its corners, with their rest volumes. See
    /// EDDeformationQualityTerm for the measure; this only says where the samples come from.
    ///
    /// A tetrahedron across four deformed corners is a finite-difference stencil of the deformation
    /// map in the volume, so its signed volume against rest is the map's local orientation there -
    /// the volumetric counterpart of a mesh triangle's signed area. That makes this a test of the
    /// deformation rather than of any one mesh: it needs no output geometry, no subdivision setting
    /// and carries no simplifier caveat, and it sees a fold in the interior that no surface shows.
    /// It is not EDDeterminantTerm again: that reads each node's own determinant, and the folds this
    /// method suffers are in the *blend* between nodes, which only a stencil across deformed points
    /// can see.
    ///
    /// Two reservations, both consequences of sampling on the field's own grid. A feature thinner
    /// than a voxel is invisible to it. And it ties the measurement resolution to the weight
    /// resolution, which the output subdivision scale exists to keep apart - so it is a second
    /// sampler beside the mesh terms, not a replacement for them. sampleRefinement loosens the
    /// first: at r each occupied voxel is split into r^3 sub-cells on a finer lattice, every
    /// sub-corner carried through the same interpolant, so the stencils sample *inside* a
    /// trilinear region rather than only at its corners. The map is a trilinear blend of affines
    /// inside a region and can fold there while the corners stay in order; only a stencil inside
    /// sees that. At r = 1 the sample set is the field's own corners, exactly as before.
    ///
    /// Only the occupied cells - the voxelized solid, not the cells GrowInfluence merely reached -
    /// and only tetrahedra every corner of which something influences. A stencil with one rest
    /// corner and three deformed ones would report a fold the deformation never made.
    ///
    /// A structure graph, or a mesh-sampled graph whose builder builds a field; elsewhere it
    /// contributes no rows and says so. The corners are carried by whatever carries the geometry -
    /// the field, or on a sampled graph under the bindings carrier, ED's bindings.
    ///
    /// **Account For Empty Space** (off by default; added 2026-10-21, Diogo's design) lays the same
    /// tetrahedra over empty cells too - every one, or those inside the rest mesh's convex hull -
    /// after the solid's, so the solid's samples and rows are as they were. An empty cell's
    /// tetrahedron turns over when the map folds across the gap it sits in, which is what two
    /// parts of the piece passing through each other do to the space between them: the idea of
    /// Mueller, Chentanez, Kim and Macklin's air meshes (2015), on the field's lattice. It is
    /// charged on rows of its own with a floor of its own, no ceiling - a bend squeezes the space
    /// inside it and opens the space outside it, legitimately - and a weight of its own; its
    /// corners are carried exactly as the solid's are, by the carrier in force. A fold of the map
    /// is what it reads, which two surfaces crossing must cause and which a fold in the gap away
    /// from either surface causes too.
    /// </summary>
    [Serializable]
    public abstract class EDDeformationFieldQualityTerm : EDDeformationQualityTerm
    {
        [SerializeField, Min(1), Tooltip("Each occupied voxel is sampled on this many sub-cells per axis - six tetrahedra per sub-cell, r^3 sub-cells per voxel - so that the stencils sample inside a trilinear region of the field and not only at its corners. 1 is the field's own corners. The cost is r^3 in tetrahedra, on every measurement and on every Jacobian column. Counts are not comparable across values; the RMS and the inverted volume fraction are.")]
        private int sampleRefinement = 1;

        [SerializeField, Tooltip("Also sample the field's empty cells, so a fold of the map across the space between two parts of the piece - the parts passing through each other - is charged. The empty space gets rows of its own after the solid's, its own floor, no ceiling and its own weight, and its corners are carried by whatever carries the geometry. Off, the term is exactly what it was. Takes effect on the next Build.")]
        private bool accountForEmptySpace = false;

        [SerializeField, ShowIf(nameof(accountForEmptySpace)), AllowNesting, Tooltip("Which empty cells are sampled: those whose centre lies inside the convex hull of the rest mesh - the space in the piece's concavities, where two of its parts can meet - or every empty cell of the field's grid. A flat rest mesh has no inside to its hull and lays nothing under the first.")]
        private EDEmptySpaceRegion emptySpaceRegion = EDEmptySpaceRegion.InsideRestConvexHull;

        [SerializeField, ShowIf(nameof(accountForEmptySpace)), AllowNesting, Range(0.0f, 1.0f), Tooltip("An empty-space tetrahedron is charged once its volume falls below this fraction of its rest volume; there is no ceiling. At 0 only a turned-over tetrahedron is charged - the space between two parts may be squeezed to nothing, not through. Keep it low: a bend legitimately squeezes the space inside it.")]
        private float emptySpaceFloor = 0.0f;

        [SerializeField, ShowIf(nameof(accountForEmptySpace)), AllowNesting, Min(0.0f), Tooltip("The empty space's weight: its rows contribute this times the mean squared shortfall over the empty-space tetrahedra, as the term's own weight does for the solid's. The term's own weight switches the whole term: at zero neither is solved for.")]
        private float emptySpaceWeight = 1.0f;

        /// <summary>
        /// Sets the refinement exactly as editing the field in the inspector would - for the
        /// export's debug measures, which host a term of their own. Read when the samples are
        /// built, so it takes effect on the next Reset (a Build).
        /// </summary>
        public void SetSampleRefinement(int value) => sampleRefinement = Mathf.Max(1, value);

        /// <summary>
        /// Sets the empty-space option exactly as editing the four fields in the inspector would -
        /// for a driver or an instrument hosting a term of its own. The samples are built in
        /// Reset, so the switch and the region take effect on the next Build; the floor and the
        /// weight are read per measurement.
        /// </summary>
        public void SetEmptySpace(bool enabled, EDEmptySpaceRegion region, float floor, float weight)
        {
            accountForEmptySpace = enabled;
            emptySpaceRegion = region;
            emptySpaceFloor = Mathf.Clamp01(floor);
            emptySpaceWeight = Mathf.Max(0.0f, weight);
        }

        protected override bool samplesEmptySpace => accountForEmptySpace;

        protected override float emptySpaceFloorRatio => Mathf.Clamp01(emptySpaceFloor);

        protected override float emptySpaceRowWeight => Mathf.Max(0.0f, emptySpaceWeight);

#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new FieldQualityInstance(this, deformation, normalizeWeights);

        public sealed class FieldQualityInstance : QualityInstance
        {
            private readonly EDDeformationFieldQualityTerm fieldTerm;

            public FieldQualityInstance(EDDeformationFieldQualityTerm term, EmbededDeformation deformation, bool normalizeWeights)
                : base(term, deformation, normalizeWeights)
            {
                fieldTerm = term;
            }

            /// <summary>
            /// The Kuhn split of a cube into six tetrahedra, each running from corner (0,0,0) to
            /// corner (1,1,1) along one ordering of the three axes. Corners are indexed by bits,
            /// x + 2y + 4z. The orientation is left to the rest signed volume, which the base term
            /// folds into a sign per simplex.
            /// </summary>
            private static readonly int[,] KuhnTetrahedra =
            {
                { 0, 1, 3, 7 },   // x, y, z
                { 0, 1, 5, 7 },   // x, z, y
                { 0, 2, 3, 7 },   // y, x, z
                { 0, 2, 6, 7 },   // y, z, x
                { 0, 4, 5, 7 },   // z, x, y
                { 0, 4, 6, 7 },   // z, y, x
            };

            protected override bool TryBuildSamples(out Vector3[] vertices, out int[] indices, out int arity, out string reason)
            {
                vertices = null;
                indices = null;
                arity = 4;
                reason = null;
                emptyFlags = null;

                FullDeformationField field = deformation.GetDeformationField();

                if (field == null)
                {
                    reason = "there is no deformation field to sample - a structure graph builds one, a mesh-sampled graph when its builder's Build Field is on, and it is rebuilt by Build() after a reload";

                    return false;
                }

                Vector3Int grid = field.gridSize;

                if ((grid.x <= 0) || (grid.y <= 0) || (grid.z <= 0))
                {
                    reason = "the deformation field has no voxel grid";

                    return false;
                }

                // The refined lattice: r sub-cells per voxel per axis. Corners are shared by up to
                // eight sub-cells, so each is deformed once: indexed by its refined integer
                // coordinates, one past the grid in every axis for the far corners.
                int r = Mathf.Max(1, fieldTerm.sampleRefinement);

                Vector3 cellSize = field.cellSize;

                var cornerIndex = new Dictionary<long, int>();
                var cornerPositions = new List<Vector3>();
                var tetrahedra = new List<int>();

                long strideY = ((long)grid.x * r) + 1;
                long strideZ = strideY * (((long)grid.y * r) + 1);

                // A sub-corner at refined coordinate (rx, ry, rz). Its position is the field's own
                // corner at (rx / r, ...) plus the remainder's fraction of a cell, so that at r = 1
                // every corner is field.CellCorner(x, y, z) exactly as before and the sample set -
                // and every column measured over it - is byte-identical to the unrefined one.
                int CornerOf(long rx, long ry, long rz)
                {
                    long key = rx + (ry * strideY) + (rz * strideZ);

                    if (!cornerIndex.TryGetValue(key, out int index))
                    {
                        index = cornerPositions.Count;

                        cornerIndex.Add(key, index);

                        Vector3 corner = field.CellCorner((int)(rx / r), (int)(ry / r), (int)(rz / r));

                        if (r > 1)
                        {
                            corner.x += (cellSize.x * (rx % r)) / r;
                            corner.y += (cellSize.y * (ry % r)) / r;
                            corner.z += (cellSize.z * (rz % r)) / r;
                        }

                        cornerPositions.Add(corner);
                    }

                    return index;
                }

                var cellCorners = new int[8];

                // One voxel's six tetrahedra, or r^3 sub-cells' worth, in sub-cell order.
                void AddCell(int x, int y, int z)
                {
                    for (int k = 0; k < r; k++)
                    {
                        for (int j = 0; j < r; j++)
                        {
                            for (int i = 0; i < r; i++)
                            {
                                long rx = ((long)x * r) + i;
                                long ry = ((long)y * r) + j;
                                long rz = ((long)z * r) + k;

                                for (int corner = 0; corner < 8; corner++)
                                    cellCorners[corner] = CornerOf(rx + (corner & 1), ry + ((corner >> 1) & 1), rz + ((corner >> 2) & 1));

                                for (int t = 0; t < 6; t++)
                                {
                                    tetrahedra.Add(cellCorners[KuhnTetrahedra[t, 0]]);
                                    tetrahedra.Add(cellCorners[KuhnTetrahedra[t, 1]]);
                                    tetrahedra.Add(cellCorners[KuhnTetrahedra[t, 2]]);
                                    tetrahedra.Add(cellCorners[KuhnTetrahedra[t, 3]]);
                                }
                            }
                        }
                    }
                }

                int occupiedCells = 0;

                // z outermost, matching the field's own index order, so the sample order - and with
                // it every serial sum over it - is a fact about the grid rather than about this loop.
                // Sub-cells follow the same order inside a voxel.
                for (int z = 0; z < grid.z; z++)
                {
                    for (int y = 0; y < grid.y; y++)
                    {
                        for (int x = 0; x < grid.x; x++)
                        {
                            if (!field.IsCellOccupied(x, y, z)) continue;

                            occupiedCells++;

                            AddCell(x, y, z);
                        }
                    }
                }

                if (occupiedCells == 0)
                {
                    reason = "the deformation field has no occupied cells";

                    return false;
                }

                // The empty space, after every solid sample, so the solid's samples, corners and
                // rows are the ones they are without it - the same loop over the cells the solid's
                // left out, the region permitting.
                emptyFlags = null;

                if (fieldTerm.accountForEmptySpace)
                {
                    int solidTetrahedra = tetrahedra.Count / 4;
                    int emptyCells = 0;

                    double[] hullPlanes = null;
                    double hullTolerance = 0.0;
                    string region = "every empty cell of the grid";
                    bool regionAvailable = true;

                    if (fieldTerm.emptySpaceRegion == EDEmptySpaceRegion.InsideRestConvexHull)
                    {
                        regionAvailable = TryRestHull(out hullPlanes, out hullTolerance, out region);
                    }

                    if (regionAvailable)
                    {
                        Vector3 half = 0.5f * cellSize;

                        for (int z = 0; z < grid.z; z++)
                        {
                            for (int y = 0; y < grid.y; y++)
                            {
                                for (int x = 0; x < grid.x; x++)
                                {
                                    if (field.IsCellOccupied(x, y, z)) continue;

                                    if ((hullPlanes != null) && (!ConvexHull3D.Contains(hullPlanes, field.CellCorner(x, y, z) + half, hullTolerance))) continue;

                                    emptyCells++;

                                    AddCell(x, y, z);
                                }
                            }
                        }
                    }

                    int total = tetrahedra.Count / 4;

                    if (total > solidTetrahedra)
                    {
                        emptyFlags = new bool[total];

                        for (int s = solidTetrahedra; s < total; s++) emptyFlags[s] = true;
                    }

                    Debug.Log($"[ED] {term.name}: empty space over {region} - {emptyCells} of {(grid.x * grid.y * grid.z) - occupiedCells} empty cells, {total - solidTetrahedra} tetrahedra.");
                }

                vertices = cornerPositions.ToArray();
                indices = tetrahedra.ToArray();

                return true;
            }

            // The empty-space flags of the last TryBuildSamples - see emptySpaceSimplices.
            private bool[] emptyFlags;

            protected override bool[] emptySpaceSimplices => emptyFlags;

            /// <summary>
            /// The face planes of the rest mesh's convex hull: every vertex of the source geometry
            /// the field was voxelized from, in world space as the field is. False, with the reason
            /// as the region's description, when there is no rest mesh or its hull has no inside -
            /// which lays no empty space rather than all of it. The tolerance is a millionth of the
            /// hull's largest extent, so a cell centre on a face counts as inside.
            /// </summary>
            private bool TryRestHull(out double[] planes, out double tolerance, out string region)
            {
                planes = null;
                tolerance = 0.0;

                List<Mesh> meshes = deformation.GetSourceGeometry();

                var points = new List<Vector3>();

                if (meshes != null)
                {
                    foreach (Mesh mesh in meshes)
                        if ((mesh != null) && (mesh.isReadable)) points.AddRange(mesh.vertices);
                }

                if (points.Count == 0)
                {
                    region = "the rest mesh's convex hull, but there is no readable rest mesh to take it of - no empty space laid";
                    return false;
                }

                Vector3[] array = points.ToArray();

                if (!ConvexHull3D.TryBuild(array, out double volume, out int[] triangles, out bool degenerate, out double joggle, out string why))
                {
                    region = $"the rest mesh's convex hull, which could not be built ({why}) - no empty space laid";
                    return false;
                }

                if (degenerate)
                {
                    region = "the rest mesh's convex hull, which is flat - no empty space laid";
                    return false;
                }

                planes = ConvexHull3D.FacePlanes(array, triangles);

                Bounds bounds = new Bounds(array[0], Vector3.zero);

                for (int i = 1; i < array.Length; i++) bounds.Encapsulate(array[i]);

                tolerance = 1e-6 * Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

                region = $"the rest mesh's convex hull ({triangles.Length / 3} faces over {array.Length} vertices, volume {volume:G5}" + ((joggle > 0.0) ? ($", joggled by {joggle:G2}") : ("")) + ")";

                return true;
            }

            // The cells are the field's; what carries their corners is whatever carries the geometry
            // - the rule the mesh term follows - so the volume measured is the volume the geometry
            // has. Through the field on a structure graph, where this was always true, and on a
            // sampled graph under the field carrier; through the bindings, bound as ED binds a
            // vertex, on a sampled graph under the bindings carrier.
            protected override bool samplesThroughField => deformation.usesDeformationFieldForOutput;

            protected override bool measureUninfluenced => false;

            public override string sampleLabel => "tetrahedra";

            public override string measureLabel => "volume";
        }
#endif
    }

    /// <summary>
    /// One row over every occupied voxel of the field.
    /// </summary>
    [Serializable]
    [PolymorphicName("Global Deformation Field Quality")]
    public class EDGlobalDeformationFieldQualityTerm : EDDeformationFieldQualityTerm
    {
        // Renamed from "globalDeformationFieldQuality" on 2026-08-27 - see the mesh term's note.
        public override string name => "glFieldQuality";

        protected override bool perNode => false;
    }

    /// <summary>
    /// One row per deformation node, over the tetrahedra whose centroid that node carries the most
    /// weight at. Same energy as the global form, more Gauss-Newton rank - see the base.
    /// </summary>
    [Serializable]
    [PolymorphicName("Per-Node Deformation Field Quality")]
    public class EDPerNodeDeformationFieldQualityTerm : EDDeformationFieldQualityTerm
    {
        public override string name => "pnFieldQuality";

        protected override bool perNode => true;
    }
}
#endif

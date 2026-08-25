using System;
using System.Collections.Generic;
using UnityEngine;

#if UC_ENABLE_ED
namespace UC.ED
{
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
    /// sampler beside the mesh terms, not a replacement for them.
    ///
    /// Only the occupied cells - the voxelized solid, not the cells GrowInfluence merely reached -
    /// and only tetrahedra every corner of which something influences. A stencil with one rest
    /// corner and three deformed ones would report a fold the deformation never made.
    ///
    /// Structure graphs only, since only they carry a field; elsewhere it contributes no rows and
    /// says so.
    /// </summary>
    [Serializable]
    public abstract class EDDeformationFieldQualityTerm : EDDeformationQualityTerm
    {
#if MATH_NET_AVAILABLE
        public override Instance NewInstance(EmbededDeformation deformation, bool normalizeWeights)
            => new FieldQualityInstance(this, deformation, normalizeWeights);

        public sealed class FieldQualityInstance : QualityInstance
        {
            public FieldQualityInstance(EDDeformationFieldQualityTerm term, EmbededDeformation deformation, bool normalizeWeights)
                : base(term, deformation, normalizeWeights)
            {
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

                FullDeformationField field = deformation.GetDeformationField();

                if (field == null)
                {
                    reason = "there is no deformation field to sample - only a structure graph builds one, and it is rebuilt by Build() after a reload";

                    return false;
                }

                Vector3Int grid = field.gridSize;

                if ((grid.x <= 0) || (grid.y <= 0) || (grid.z <= 0))
                {
                    reason = "the deformation field has no voxel grid";

                    return false;
                }

                // Corners are shared by up to eight cells, so each is deformed once: indexed by its
                // own integer coordinates, one past the grid in every axis for the far corners.
                var cornerIndex = new Dictionary<long, int>();
                var cornerPositions = new List<Vector3>();
                var tetrahedra = new List<int>();

                long strideY = grid.x + 1;
                long strideZ = strideY * (grid.y + 1);

                int CornerOf(int x, int y, int z)
                {
                    long key = x + (y * strideY) + (z * strideZ);

                    if (!cornerIndex.TryGetValue(key, out int index))
                    {
                        index = cornerPositions.Count;

                        cornerIndex.Add(key, index);
                        cornerPositions.Add(field.CellCorner(x, y, z));
                    }

                    return index;
                }

                var cellCorners = new int[8];

                int occupiedCells = 0;

                // z outermost, matching the field's own index order, so the sample order - and with
                // it every serial sum over it - is a fact about the grid rather than about this loop.
                for (int z = 0; z < grid.z; z++)
                {
                    for (int y = 0; y < grid.y; y++)
                    {
                        for (int x = 0; x < grid.x; x++)
                        {
                            if (!field.IsCellOccupied(x, y, z)) continue;

                            occupiedCells++;

                            for (int corner = 0; corner < 8; corner++)
                                cellCorners[corner] = CornerOf(x + (corner & 1), y + ((corner >> 1) & 1), z + ((corner >> 2) & 1));

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

                if (occupiedCells == 0)
                {
                    reason = "the deformation field has no occupied cells";

                    return false;
                }

                vertices = cornerPositions.ToArray();
                indices = tetrahedra.ToArray();

                return true;
            }

            protected override bool samplesThroughField => true;

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
        public override string name => "globalDeformationFieldQuality";

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
        public override string name => "perNodeDeformationFieldQuality";

        protected override bool perNode => true;
    }
}
#endif

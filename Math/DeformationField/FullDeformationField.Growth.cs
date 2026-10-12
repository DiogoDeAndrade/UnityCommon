using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The influence growth, one wavefront per node and the nodes in parallel.
///
/// GrowInfluence used to run one heap over every node's entries at once: thirty wavefronts
/// interleaved, each filling every empty cell of the grid. Under slot-per-node storage the nodes
/// never interact - node i's wavefront reads and writes slot i of a cell and nothing else, and the
/// occupancy it steps through is fixed once FillWithMesh has run - so the interleaving was an
/// accident of sharing one heap, and a heap per node gives every cell the same distances.
///
/// **Why the same distances, to the bit.** Dijkstra's final values depend on the order entries are
/// popped only where two entries carry exactly the same float distance, and a tie between two
/// parents either proposes the same float to a shared neighbour (same step cost, same addition) or
/// proposals that differ by at least a tenth of a voxel - the gap between a face step and an edge
/// step - which is far above DistanceEpsilon, so the smaller one wins whichever arrives first. The
/// relaxation itself is unchanged: the same tests in the same order, the same step costs, the same
/// comparison against the stored value less epsilon. What differs is only which thread does it.
///
/// **What a worker owns.** A flat distance array of its own and a heap of its own; nothing it writes
/// during propagation is visible to another worker. The distances are scattered into the cells'
/// slots afterwards, on one thread, so the parallel section has no shared write at all - not one
/// protected by a lock, none. A worker writing straight into slot i would have been correct too
/// (distinct array elements), but thirty threads writing adjacent floats of the same small arrays
/// would spend the time bouncing cache lines, and the flat array is also what makes the inner loop
/// a contiguous read rather than a cell struct, an array reference and then an element.
///
/// **The one shared write that had to be removed.** TryStoreDistance sizes a cell's arrays on its
/// first store, through EnsureWeights, and SetupWeights replaces all three arrays of the cell - a
/// write every other worker would be reading through. It is a no-op after FillWithMesh, which sizes
/// every cell, but the parallel path does not rely on that: every cell is sized before any worker
/// starts, and the workers never call it.
///
/// **Capped storage keeps the shared heap.** With fewer slots than nodes the nodes do interact,
/// through eviction, and the sequential order is part of the result there. That path is the legacy
/// one and is left as it was.
/// </summary>
public partial class FullDeformationField
{
    /// <summary>
    /// What the last GrowInfluence found and did, for the build log. Not serialized: they describe
    /// a pass, not the field.
    /// </summary>
    [NonSerialized] int    growthOccupiedCells;
    [NonSerialized] int    growthBoundaryCells;
    [NonSerialized] string growthPath;

    /// <summary>Occupied cells at the last growth - the cells the wavefronts were seeded from.</summary>
    public int lastGrowthOccupiedCells => growthOccupiedCells;
    /// <summary>
    /// Occupied cells with at least one empty neighbour a step may be taken to - the only seeds that
    /// relax anything. The rest were popped once each per node, tested 26 neighbours and relaxed none.
    /// </summary>
    public int lastGrowthBoundaryCells => growthBoundaryCells;
    /// <summary>"per-node" or "shared heap", or "none" before the first growth.</summary>
    public string lastGrowthPath => (string.IsNullOrEmpty(growthPath)) ? ("none") : (growthPath);

    /// <summary>
    /// The step cost of every neighbour offset, in the offsets' own order, so the propagation
    /// indexes a table instead of taking a square root per neighbour test - some 35 million of them
    /// on a 50,000-cell grid with thirty nodes. Computed by StepCost itself, once per offset, so each
    /// entry is the float the per-test call produced, bit for bit.
    /// </summary>
    float[] StepCosts(Vector3Int[] offsets)
    {
        float[] costs = new float[offsets.Length];

        for (int i = 0; i < offsets.Length; i++)
        {
            costs[i] = StepCost(offsets[i]);
        }

        return costs;
    }

    /// <summary>
    /// Which cells are inside the solid, as a flat array the workers can read without touching the
    /// cell structs. Occupancy does not change after FillWithMesh, so a snapshot taken at the start
    /// of growth is the occupancy throughout it.
    /// </summary>
    bool[] OccupancySnapshot()
    {
        bool[] occupied = new bool[voxelData.data.Length];

        for (int i = 0; i < occupied.Length; i++)
        {
            occupied[i] = voxelData.data[i].IsOccupied();
        }

        return occupied;
    }

    /// <summary>
    /// The occupied cells a growth wavefront can relax something from: those with at least one
    /// neighbour that is inside the grid, empty, and reachable by a step whose shoulders are clear.
    /// That is the relaxation's own precondition, test for test, so seeding only these cells is the
    /// same computation with the pops that did nothing removed - an interior cell, every neighbour
    /// of it occupied, fails the second test on all 26 offsets and relaxes none of them.
    /// </summary>
    bool[] BoundaryCells(bool[] occupied, Vector3Int[] offsets)
    {
        bool[] boundary = new bool[occupied.Length];
        Vector3Int gridSize = voxelData.gridSize;

        for (int index = 0; index < occupied.Length; index++)
        {
            if (!occupied[index]) continue;

            Vector3Int position = PositionOf(index);

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3Int offset = offsets[i];
                int nx = position.x + offset.x;
                int ny = position.y + offset.y;
                int nz = position.z + offset.z;

                if (!IsInside(nx, ny, nz)) continue;
                if (occupied[IndexOf(nx, ny, nz)]) continue;
                if (!StepIsClear(position, offset, occupied, gridSize, false)) continue;

                boundary[index] = true;
                break;
            }
        }

        return boundary;
    }

    /// <summary>
    /// StepIsClear over an occupancy snapshot rather than the cell structs: the same predicate as the
    /// instance method of that name, which the in-solid wavefronts and the shared-heap growth still
    /// use, written against a flat array so that a worker reads nothing but its own distances and
    /// read-only tables. The two must stay the same test; the goldens are what says they are.
    /// </summary>
    static bool StepIsClear(Vector3Int from, Vector3Int offset, bool[] occupied, Vector3Int gridSize, bool wantOccupied)
    {
        int axes = ((offset.x != 0) ? 1 : 0) + ((offset.y != 0) ? 1 : 0) + ((offset.z != 0) ? 1 : 0);

        if (axes < 2) return true;

        if ((offset.x != 0) && (!ShoulderIsClear(from, new Vector3Int(offset.x, 0, 0), occupied, gridSize, wantOccupied))) return false;
        if ((offset.y != 0) && (!ShoulderIsClear(from, new Vector3Int(0, offset.y, 0), occupied, gridSize, wantOccupied))) return false;
        if ((offset.z != 0) && (!ShoulderIsClear(from, new Vector3Int(0, 0, offset.z), occupied, gridSize, wantOccupied))) return false;

        return true;
    }

    static bool ShoulderIsClear(Vector3Int from, Vector3Int offset, bool[] occupied, Vector3Int gridSize, bool wantOccupied)
    {
        int nx = from.x + offset.x;
        int ny = from.y + offset.y;
        int nz = from.z + offset.z;

        // Outside the grid counts as blocked, as in the instance version.
        if ((nx < 0) || (ny < 0) || (nz < 0) || (nx >= gridSize.x) || (ny >= gridSize.y) || (nz >= gridSize.z)) return false;

        return (occupied[nx + ny * gridSize.x + nz * gridSize.x * gridSize.y] == wantOccupied);
    }

    /// <summary>
    /// One Dijkstra per node over the empty cells, the nodes in parallel, then one pass scattering
    /// every node's distances into its slot. Requires slot-per-node storage covering every node;
    /// GrowInfluence checks that before coming here.
    /// </summary>
    void GrowInfluencePerNode(ParallelOptions parallelOptions, bool[] occupied, bool[] boundary, Vector3Int[] offsets, float[] costs)
    {
        int cellCount = voxelData.data.Length;
        int nodeCount = deformationNodes.Count;
        Vector3Int gridSize = voxelData.gridSize;

        // Every cell sized before any worker starts - see the class comment. After this nothing in
        // the parallel section allocates into a cell.
        for (int index = 0; index < cellCount; index++)
        {
            EnsureWeights(ref voxelData.data[index]);
        }

        // One distance array per node, kept until the scatter so that the parallel section writes
        // nothing shared. Transient: nodes x cells floats, 6 MB on the 50,000-cell grid with thirty
        // nodes, released with the pass.
        float[][] grown = new float[nodeCount][];

        Parallel.For(0, nodeCount, parallelOptions, node =>
        {
            float[] distance = new float[cellCount];

            for (int index = 0; index < cellCount; index++)
            {
                distance[index] = float.MaxValue;
            }

            MinHeap heap = new();

            // Seed from the occupied cells, at the distances the in-solid wavefront left in this
            // node's slot. Every occupied cell's distance is copied, because a popped seed checks
            // itself against it; only the boundary cells are pushed, because only they relax.
            for (int index = 0; index < cellCount; index++)
            {
                if (!occupied[index]) continue;

                DeformationFieldWeights element = voxelData.data[index];

                if (element.nodeId[node] != node) continue;

                float stored = element.distances[node];

                if (stored >= float.MaxValue) continue;

                distance[index] = stored;

                if (boundary[index])
                {
                    heap.Enqueue(new QueueItem(index, node, stored));
                }
            }

            while (heap.TryDequeue(out QueueItem current))
            {
                // Stale entries left behind by a later improvement, as in the shared heap.
                if (Mathf.Abs(distance[current.voxelIndex] - current.distance) > DistanceEpsilon)
                {
                    continue;
                }

                Vector3Int currentPos = PositionOf(current.voxelIndex);

                for (int i = 0; i < offsets.Length; i++)
                {
                    Vector3Int offset = offsets[i];
                    int nx = currentPos.x + offset.x;
                    int ny = currentPos.y + offset.y;
                    int nz = currentPos.z + offset.z;

                    if (!IsInside(nx, ny, nz)) continue;

                    int neighbourIndex = IndexOf(nx, ny, nz);

                    // Growth never enters the solid, and a diagonal may not round its corner - the
                    // two rules of the shared heap, in its order.
                    if (occupied[neighbourIndex]) continue;
                    if (!StepIsClear(currentPos, offset, occupied, gridSize, false)) continue;

                    float newDistance = current.distance + costs[i];

                    // TryStoreDistance's comparison, against this node's own array.
                    if (newDistance >= distance[neighbourIndex] - DistanceEpsilon) continue;

                    distance[neighbourIndex] = newDistance;
                    heap.Enqueue(new QueueItem(neighbourIndex, node, newDistance));
                }
            }

            grown[node] = distance;
        });

        // The scatter: what TryStoreDistance wrote on a successful store, written once per reached
        // cell and node. Occupied cells are left alone - growth never stores into them.
        for (int index = 0; index < cellCount; index++)
        {
            if (occupied[index]) continue;

            ref DeformationFieldWeights element = ref voxelData.data[index];

            for (int node = 0; node < nodeCount; node++)
            {
                float d = grown[node][index];

                if (d >= float.MaxValue) continue;

                element.distances[node] = d;
                element.nodeId[node] = node;
                element.weights[node] = 0f;
            }
        }
    }
}

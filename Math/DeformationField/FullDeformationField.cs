using UC;
using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Which neighbours a wavefront may step to.
///
/// Faces6 makes every step cost the same, so a distance is a hop count and the metric is L1: the
/// isolines are diamonds, and a 45-degree path is overestimated by about 41%. The richer stencils
/// add the diagonal steps at their true Euclidean cost, which rounds the isolines out - 18 removes
/// most of the error in the axis planes, 26 the rest of it.
///
/// They are not free, and not only in time. A diagonal step passes between cells it never enters, so
/// each one has to be checked against the volume it is crossing or the wavefront rounds the corner
/// of a wall. That check is what StepIsClear does.
/// </summary>
public enum EDFieldConnectivity
{
    /// <summary>Faces only. Cheapest, and the metric is L1.</summary>
    Faces6,
    /// <summary>Faces and edges. Removes most of the diamond in the axis planes.</summary>
    FacesEdges18,
    /// <summary>Faces, edges and corners. Closest to Euclidean of the three.</summary>
    FacesEdgesCorners26
}

[Serializable]
public partial class FullDeformationField
{
    [Serializable]
    public struct DeformationFieldWeights : IEquatable<DeformationFieldWeights>, IOccupancyState
    {
        public bool filled;
        public float[] distances;
        public int[] nodeId;
        public float[] weights;

        public void ClearOccupancy()
        {
            filled = false;
        }

        public bool Equals(DeformationFieldWeights other)
        {
            if (filled != other.filled) return false;

            if ((distances == null) || (weights == null) || (nodeId == null))
            {
                return ((other.distances == null) && (other.weights == null) && (other.nodeId == null));
            }

            if ((other.distances == null) || (other.weights == null) || (other.nodeId == null)) return false;
            if (distances.Length != other.distances.Length) return false;
            if (weights.Length != other.weights.Length) return false;
            if (nodeId.Length != other.nodeId.Length) return false;

            for (int i = 0; i < weights.Length; i++)
            {
                if (distances[i] != other.distances[i]) return false;
                if (weights[i] != other.weights[i]) return false;
                if (nodeId[i] != other.nodeId[i]) return false;
            }

            return true;
        }

        public bool IsEmpty()
        {
            return !filled;
        }

        public bool IsOccupied()
        {
            return filled;
        }

        public void Occupy()
        {
            filled = true;
        }

        public void SetupWeights(int maxWeights)
        {
            distances = new float[maxWeights];
            weights = new float[maxWeights];
            nodeId = new int[maxWeights];
            for (int i = 0; i < maxWeights; i++)
            {
                distances[i] = float.MaxValue;
                weights[i] = 0f;
                nodeId[i] = -1;
            }
        }
    }

    [Serializable]
    public struct Frame
    {
        public Vector3 position;
        public Vector3 right;
        public Vector3 up;
        public Vector3 forward;

        public Frame(Vector3 position)
        {
            this.position = position;
            right = Vector3.right;
            up = Vector3.up;
            forward = Vector3.forward;
        }

        public Frame(Vector3 position, Vector3 right, Vector3 up, Vector3 forward)
        {
            this.position = position;
            this.right = right;
            this.up = up;
            this.forward = forward;
        }

        public Frame(Vector3 position, Quaternion rotation)
        {
            this.position = position;
            right = rotation * Vector3.right;
            up = rotation * Vector3.up;
            forward = rotation * Vector3.forward;
        }

        public static Frame identity => new Frame(Vector3.zero);

        // Affine transform mapping local (x, y, z) to
        // position + x * right + y * up + z * forward.
        public Matrix4x4 ToMatrix()
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(right.x, right.y, right.z, 0f));
            m.SetColumn(1, new Vector4(up.x, up.y, up.z, 0f));
            m.SetColumn(2, new Vector4(forward.x, forward.y, forward.z, 0f));
            m.SetColumn(3, new Vector4(position.x, position.y, position.z, 1f));
            return m;
        }
    }

    [Serializable]
    public struct DeformationNode
    {
        public Frame frame;
        public float sourceLength;

        public DeformationNode(Frame frame, float sourceLength = 0.0f)
        {
            this.frame = frame;
            this.sourceLength = sourceLength;
        }
    }

    // Internal rather than private only so that InfluenceEnumerator, which hands these out one at a
    // time, can name them in its own constructors without tripping the accessibility rule.
    [Serializable]
    internal struct TrilinearRegion
    {
        public int influenceStart;
        public int influenceCount;
    }

    [Serializable]
    internal struct TrilinearInfluence
    {
        public int nodeId;

        // Corner order:
        // lowZ  = (000, 100, 010, 110)
        // highZ = (001, 101, 011, 111)
        public Vector4 lowZ;
        public Vector4 highZ;
    }

    [SerializeField, HideInInspector]
    VoxelData<DeformationFieldWeights>  voxelData;
    [SerializeField, HideInInspector]
    float                               voxelSize;
    /// <summary>
    /// The density voxelSize was derived from. Recorded, never used - see the constructor.
    /// </summary>
    [SerializeField, HideInInspector]
    float                               voxelDensity;
    [SerializeField, HideInInspector]
    int                                 maxWeights;
    /// <summary>
    /// How many nodes a cell records, as opposed to how many it weights.
    ///
    /// Normally equal to maxWeights, and then eviction is what bounds the whole thing. Raised above
    /// it for inspection: a cell keeps every node that reaches it, ComputeWeights still weights only
    /// the nearest maxWeights, and the rest sit there at weight zero so a tool can read the distance
    /// that would otherwise have been thrown away.
    ///
    /// That is not free, and the cost is time rather than memory. Eviction is also what terminates
    /// the wavefront - a node dropped from a cell stops propagating out of it - so keeping
    /// everything makes every node's front cross the whole volume.
    /// </summary>
    [SerializeField, HideInInspector]
    int                                 storageSlots;
    /// <summary>
    /// Slot i holds node i, so a cell needs neither a search nor a sort while distances propagate.
    ///
    /// Only valid when storageSlots is at least the node count, which is exactly the case where no
    /// eviction can occur - and eviction is the only reason the per-cell array was ever kept sorted.
    /// The k-nearest ordering the weights need is established once, in ComputeWeights, instead of
    /// being maintained on every relaxation.
    /// </summary>
    [SerializeField, HideInInspector]
    bool                                slotPerNode;
    [SerializeField, HideInInspector]
    EDFieldConnectivity                 connectivity;
    /// <summary>
    /// Which nodes were seeded along a bar rather than from a single voxel. Recorded rather than
    /// derived: with every seeding mode off the source lengths are all zero, and so are they when
    /// corridor seeding is on and every measurement failed - two states that must not be mistaken
    /// for each other by whatever later asks what this field is.
    /// </summary>
    [SerializeField, HideInInspector]
    bool                                seedTerminals;
    [SerializeField, HideInInspector]
    bool                                seedCorridors;
    /// <summary>
    /// Which distance-to-weight mapping produced the weights currently stored, as the resolver's own
    /// description of itself.
    ///
    /// A string rather than a mode and a set of parameters, because the parameters differ per mapping
    /// and a field carrying every mapping's parameters would carry mostly meaningless ones. It is
    /// generated by the resolver from its own state, so it cannot describe something the resolver
    /// does not do. Empty means ComputeWeights has not run.
    /// </summary>
    [SerializeField, HideInInspector]
    string                              weightDescriptor;
    /// <summary>
    /// How the transforms of the nodes influencing a point get combined.
    ///
    /// Unlike everything above it, this is not baked into anything the build produces - the weights
    /// are stored per cell, the blend is performed per query. It is recorded here anyway because this
    /// is the object that performs it, and because a setting that changes every deformed position
    /// while leaving no trace in the field would be invisible to the golden harness. Stored as the
    /// mode rather than as a descriptor because the descriptor is generated from these three by
    /// DescribeBlend, which is also what the blenders use - so there is one formatter and nothing to
    /// drift out of step.
    /// </summary>
    [SerializeField, HideInInspector]
    EDFieldBlendMode                    blendMode;
    /// <summary>
    /// The two halves of a decomposed blend: how the rotations are averaged, and what happens to the
    /// stretch. Meaningless for LinearAffine, which is why DescribeBlend does not print them for it.
    /// </summary>
    [SerializeField, HideInInspector]
    EDFieldRotationBlend                rotationBlend;
    [SerializeField, HideInInspector]
    EDFieldScaleBlend                   scaleBlend;
    [SerializeField, HideInInspector]
    List<DeformationNode>               deformationNodes = new List<DeformationNode>();
    [SerializeField, HideInInspector]
    List<Matrix4x4>                     restInverses = new List<Matrix4x4>();


    // Runtime-only cache. It is rebuilt lazily after a Unity domain reload, or explicitly through BuildTrilinearRegions().
    [NonSerialized] TrilinearRegion[]       trilinearRegions;
    [NonSerialized] TrilinearInfluence[]    trilinearInfluences;
    [NonSerialized] Vector3Int              trilinearRegionGridSize;
    [NonSerialized] volatile bool           trilinearRegionsDirty = true;
    [NonSerialized] object                  trilinearBuildLock;

    public Vector3Int gridSize => voxelData?.gridSize ?? Vector3Int.zero;
    public Vector3 cellSize => Vector3.one * voxelSize;
    public Vector3 minBound => voxelData?.minBound ?? Vector3.zero;
    public int maxInfluencesPerCell => maxWeights;
    public int storedInfluencesPerCell => storageSlots;
    public float builtVoxelDensity => voxelDensity;
    public EDFieldConnectivity builtConnectivity => connectivity;
    public bool builtSeedTerminals => seedTerminals;
    public bool builtSeedCorridors => seedCorridors;
    public string builtWeightDescriptor => (string.IsNullOrEmpty(weightDescriptor)) ? ("unbuilt") : (weightDescriptor);
    public EDFieldBlendMode builtBlendMode => blendMode;
    public EDFieldRotationBlend builtRotationBlend => rotationBlend;
    public EDFieldScaleBlend builtScaleBlend => scaleBlend;
    public string builtBlendDescriptor => DescribeBlend(blendMode, rotationBlend, scaleBlend);

    /// <summary>
    /// The seeded nodes, for drawing. The rest frame and the bar length are what the field was
    /// actually built from, so a gizmo reading them shows the seeding rather than a reconstruction
    /// of it that could disagree.
    /// </summary>
    public int deformationNodeCount => (deformationNodes != null) ? (deformationNodes.Count) : (0);

    public DeformationNode GetDeformationNode(int index) => deformationNodes[index];

    /// <summary>
    /// Whether a cell is inside the voxelized source geometry - filled by FillWithMesh, as opposed
    /// to merely reached by GrowInfluence. For a sampler that wants to measure the deformation of
    /// the solid and nothing else. False for anything outside the grid.
    /// </summary>
    public bool IsCellOccupied(int x, int y, int z)
    {
        if (!HasVoxelData()) return false;
        if (!IsInside(x, y, z)) return false;

        return voxelData.data[IndexOf(x, y, z)].IsOccupied();
    }

    /// <summary>
    /// The world position of a cell's minimum corner. Corner (x, y, z) is shared by the eight cells
    /// around it, so a sampler laying stencils over the grid can index its vertices by these
    /// coordinates and deform each corner once. Valid one past the grid in every axis, which is
    /// where the last cell's far corners are.
    /// </summary>
    public Vector3 CellCorner(int x, int y, int z)
    {
        if (!HasVoxelData()) return Vector3.zero;

        return voxelData.minBound + new Vector3(x * voxelData.voxelSize.x, y * voxelData.voxelSize.y, z * voxelData.voxelSize.z);
    }

    const float DistanceEpsilon = 1e-5f;

    static readonly Vector3Int[] FaceNeighbourOffsets =
    {
        new Vector3Int(-1,  0,  0),
        new Vector3Int( 1,  0,  0),
        new Vector3Int( 0, -1,  0),
        new Vector3Int( 0,  1,  0),
        new Vector3Int( 0,  0, -1),
        new Vector3Int( 0,  0,  1),
    };

    // Built rather than typed out: 26 literals is 26 chances to transpose a sign, and the rule that
    // generates them is shorter than the list. Deterministic order, so the arrays are the same on
    // every run.
    static readonly Vector3Int[] FaceEdgeNeighbourOffsets;
    static readonly Vector3Int[] FaceEdgeCornerNeighbourOffsets;

    static FullDeformationField()
    {
        List<Vector3Int> faceEdge = new();
        List<Vector3Int> all = new();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    if ((x == 0) && (y == 0) && (z == 0)) continue;

                    Vector3Int offset = new Vector3Int(x, y, z);

                    all.Add(offset);

                    if ((Mathf.Abs(x) + Mathf.Abs(y) + Mathf.Abs(z)) <= 2) faceEdge.Add(offset);
                }
            }
        }

        FaceEdgeNeighbourOffsets = faceEdge.ToArray();
        FaceEdgeCornerNeighbourOffsets = all.ToArray();
    }

    Vector3Int[] NeighbourOffsets()
    {
        switch (connectivity)
        {
            case EDFieldConnectivity.FacesEdges18:        return FaceEdgeNeighbourOffsets;
            case EDFieldConnectivity.FacesEdgesCorners26: return FaceEdgeCornerNeighbourOffsets;
            default:                                      return FaceNeighbourOffsets;
        }
    }

    /// <summary>
    /// Whether a step may be taken, given that a diagonal one passes between cells it never enters.
    ///
    /// A face step touches nothing but its endpoints, which is why 6-connectivity needs no such
    /// check. A diagonal step slides past the cells that share its axes: allowing it unconditionally
    /// lets a wavefront round the corner of a wall - through the solid when growing outside it, or
    /// across a doorway gap when measuring inside it. Either way the path does not exist in the
    /// volume being measured, and the distance that comes back is shorter than any real route.
    ///
    /// The shoulders are tested in the same sense as the step itself, so this serves both phases:
    /// wantOccupied is true while distances propagate through the solid, false while influence grows
    /// outside it.
    /// </summary>
    bool StepIsClear(Vector3Int from, Vector3Int offset, bool wantOccupied)
    {
        int axes = ((offset.x != 0) ? 1 : 0) + ((offset.y != 0) ? 1 : 0) + ((offset.z != 0) ? 1 : 0);

        if (axes < 2) return true;

        if ((offset.x != 0) && (!ShoulderIsClear(from, new Vector3Int(offset.x, 0, 0), wantOccupied))) return false;
        if ((offset.y != 0) && (!ShoulderIsClear(from, new Vector3Int(0, offset.y, 0), wantOccupied))) return false;
        if ((offset.z != 0) && (!ShoulderIsClear(from, new Vector3Int(0, 0, offset.z), wantOccupied))) return false;

        return true;
    }

    bool ShoulderIsClear(Vector3Int from, Vector3Int offset, bool wantOccupied)
    {
        int nx = from.x + offset.x;
        int ny = from.y + offset.y;
        int nz = from.z + offset.z;

        // Outside the grid counts as blocked: a step that leaves the volume and comes back is the
        // shortcut this is here to refuse.
        if (!IsInside(nx, ny, nz)) return false;

        return (voxelData.data[IndexOf(nx, ny, nz)].IsOccupied() == wantOccupied);
    }

    struct QueueItem
    {
        public int voxelIndex;
        public int nodeIndex;
        public float distance;

        public QueueItem(int voxelIndex, int nodeIndex, float distance)
        {
            this.voxelIndex = voxelIndex;
            this.nodeIndex = nodeIndex;
            this.distance = distance;
        }
    }

    class MinHeap
    {
        readonly List<QueueItem> data = new();

        public int Count => data.Count;

        public void Clear()
        {
            data.Clear();
        }

        public void Enqueue(QueueItem item)
        {
            data.Add(item);

            int child = data.Count - 1;
            while (child > 0)
            {
                int parent = (child - 1) >> 1;
                if (data[parent].distance <= item.distance) break;

                data[child] = data[parent];
                child = parent;
            }

            data[child] = item;
        }

        public bool TryDequeue(out QueueItem item)
        {
            if (data.Count == 0)
            {
                item = default;
                return false;
            }

            item = data[0];

            int lastIndex = data.Count - 1;
            QueueItem last = data[lastIndex];
            data.RemoveAt(lastIndex);

            if (data.Count > 0)
            {
                int parent = 0;
                while (true)
                {
                    int left = parent * 2 + 1;
                    if (left >= data.Count) break;

                    int right = left + 1;
                    int child = (right < data.Count && data[right].distance < data[left].distance) ? right : left;

                    if (data[child].distance >= last.distance) break;

                    data[parent] = data[child];
                    parent = child;
                }

                data[parent] = last;
            }

            return true;
        }
    }

    int IndexOf(int x, int y, int z) => voxelData.IndexOf(x, y, z);

    Vector3Int PositionOf(int index)
    {
        int slice = voxelData.gridSize.x * voxelData.gridSize.y;
        int z = index / slice;
        int rem = index - z * slice;
        int y = rem / voxelData.gridSize.x;
        int x = rem - y * voxelData.gridSize.x;

        return new Vector3Int(x, y, z);
    }

    bool IsInside(int x, int y, int z)
    {
        return (x >= 0) && (y >= 0) && (z >= 0) &&
               (x < voxelData.gridSize.x) &&
               (y < voxelData.gridSize.y) &&
               (z < voxelData.gridSize.z);
    }

    bool HasVoxelData()
    {
        return (voxelData != null) && (voxelData.data != null) && (voxelData.gridSize.x > 0) && (voxelData.gridSize.y > 0) && (voxelData.gridSize.z > 0);
    }

    Vector3 VoxelCenter(int x, int y, int z)
    {
        return voxelData.minBound + new Vector3((x + 0.5f) * voxelData.voxelSize.x, (y + 0.5f) * voxelData.voxelSize.y, (z + 0.5f) * voxelData.voxelSize.z);
    }

    Vector3Int WorldToVoxel(Vector3 position)
    {
        Vector3 local = position - voxelData.minBound;

        return new Vector3Int(Mathf.FloorToInt(local.x / voxelData.voxelSize.x), Mathf.FloorToInt(local.y / voxelData.voxelSize.y), Mathf.FloorToInt(local.z / voxelData.voxelSize.z));
    }

    float StepCost(Vector3Int offset)
    {
        return new Vector3(offset.x * voxelData.voxelSize.x, offset.y * voxelData.voxelSize.y, offset.z * voxelData.voxelSize.z).magnitude;
    }

    void EnsureWeights(ref DeformationFieldWeights element)
    {
        // Against storageSlots, not maxWeights. This is the one place where widening the arrays
        // would otherwise be silently undone: a cell sized to hold everything would fail the length
        // check on the very next touch and be reset back to empty.
        if ((element.distances == null) ||
            (element.weights == null) ||
            (element.nodeId == null) ||
            (element.distances.Length != storageSlots) ||
            (element.weights.Length != storageSlots) ||
            (element.nodeId.Length != storageSlots))
        {
            element.SetupWeights(storageSlots);
        }
    }

    bool HasInfluence(DeformationFieldWeights element)
    {
        if (element.nodeId == null) return false;

        for (int i = 0; i < element.nodeId.Length; i++)
        {
            if ((element.nodeId[i] >= 0) && (element.distances[i] < float.MaxValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Orders a cell's influences by distance, breaking ties by node index.
    ///
    /// The tie-break is not a tidiness measure, it is what makes the k-nearest set well defined.
    /// Every step of the wavefront costs the same float - the neighbourhood is 6-connected and the
    /// cell is a cube - so a distance is a hop count times the voxel size, and two nodes the same
    /// number of hops away are exactly, bitwise equal. Ties are the normal case here, not a rarity.
    ///
    /// Ordering equal distances by arrival made the choice of which node the k-th slot holds a
    /// property of the storage layout: change how slots are filled and a different node is weighted,
    /// with nothing about the geometry having moved. Ordering by node index makes the result a
    /// function of (distance, node) alone, so it survives any future change to how cells are stored.
    /// </summary>
    void SortInfluences(ref DeformationFieldWeights element)
    {
        EnsureWeights(ref element);

        for (int i = 0; i < storageSlots - 1; i++)
        {
            int best = i;
            float bestDistance = (element.nodeId[i] >= 0) ? element.distances[i] : float.MaxValue;
            int bestNode = (element.nodeId[i] >= 0) ? element.nodeId[i] : int.MaxValue;

            for (int j = i + 1; j < storageSlots; j++)
            {
                float d = (element.nodeId[j] >= 0) ? element.distances[j] : float.MaxValue;
                int n = (element.nodeId[j] >= 0) ? element.nodeId[j] : int.MaxValue;

                if ((d < bestDistance) || ((d == bestDistance) && (n < bestNode)))
                {
                    best = j;
                    bestDistance = d;
                    bestNode = n;
                }
            }

            if (best == i) continue;

            float distance = element.distances[i];
            element.distances[i] = element.distances[best];
            element.distances[best] = distance;

            float weight = element.weights[i];
            element.weights[i] = element.weights[best];
            element.weights[best] = weight;

            int node = element.nodeId[i];
            element.nodeId[i] = element.nodeId[best];
            element.nodeId[best] = node;
        }
    }

    float GetDistanceForNode(DeformationFieldWeights element, int nodeIndex)
    {
        if (element.nodeId == null) return float.MaxValue;

        // Called on every heap pop, so the linear scan below is the inner loop of the whole build.
        if ((slotPerNode) && (nodeIndex >= 0) && (nodeIndex < element.nodeId.Length))
        {
            return (element.nodeId[nodeIndex] == nodeIndex) ? (element.distances[nodeIndex]) : (float.MaxValue);
        }

        for (int i = 0; i < element.nodeId.Length; i++)
        {
            if (element.nodeId[i] == nodeIndex)
            {
                return element.distances[i];
            }
        }

        return float.MaxValue;
    }

    bool TryStoreDistance(ref DeformationFieldWeights element, int nodeIndex, float distance)
    {
        EnsureWeights(ref element);

        // The whole point of the restructure. With a slot per node there is nothing to search for
        // and nothing to evict, so the O(n) scan and the O(n^2) sort below both disappear from the
        // relaxation loop - and they were the reason keeping every distance was expensive.
        //
        // Equivalent to the general path rather than a shortcut through it: an unset slot holds
        // MaxValue, so a first arrival takes the same comparison an improvement does, and reaches
        // the same store.
        //
        // Deliberately does not sort. Nothing in the propagation reads slot order - GetDistanceForNode
        // indexes by node - and the ordering the weights need is established once in ComputeWeights.
        if ((slotPerNode) && (nodeIndex >= 0) && (nodeIndex < storageSlots))
        {
            if (distance >= element.distances[nodeIndex] - DistanceEpsilon) return false;

            element.distances[nodeIndex] = distance;
            element.nodeId[nodeIndex] = nodeIndex;
            element.weights[nodeIndex] = 0f;

            return true;
        }

        int existingSlot = -1;
        int emptySlot = -1;

        for (int i = 0; i < storageSlots; i++)
        {
            if (element.nodeId[i] == nodeIndex)
            {
                existingSlot = i;
                break;
            }

            if ((emptySlot < 0) && (element.nodeId[i] < 0))
            {
                emptySlot = i;
            }
        }

        if (existingSlot >= 0)
        {
            if (distance >= element.distances[existingSlot] - DistanceEpsilon)
            {
                return false;
            }

            element.distances[existingSlot] = distance;
            element.weights[existingSlot] = 0f;
            SortInfluences(ref element);
            return true;
        }

        // With storageSlots above maxWeights there is always an empty slot until every node has
        // reached this cell, so the eviction below simply stops happening - which is the whole point,
        // since eviction is also what stops a node's wavefront.
        int targetSlot = emptySlot >= 0 ? emptySlot : storageSlots - 1;

        if (emptySlot < 0 && distance >= element.distances[targetSlot] - DistanceEpsilon)
        {
            return false;
        }

        element.distances[targetSlot] = distance;
        element.nodeId[targetSlot] = nodeIndex;
        element.weights[targetSlot] = 0f;
        SortInfluences(ref element);
        return true;
    }

    int FindClosestFilledVoxel(Vector3 position)
    {
        if (!HasVoxelData()) return -1;

        Vector3Int p = WorldToVoxel(position);

        if (IsInside(p.x, p.y, p.z))
        {
            int directIndex = IndexOf(p.x, p.y, p.z);
            if (voxelData.data[directIndex].IsOccupied())
            {
                return directIndex;
            }
        }

        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        for (int z = 0; z < voxelData.gridSize.z; z++)
        {
            for (int y = 0; y < voxelData.gridSize.y; y++)
            {
                for (int x = 0; x < voxelData.gridSize.x; x++)
                {
                    int index = IndexOf(x, y, z);
                    if (!voxelData.data[index].IsOccupied()) continue;

                    float distance = (VoxelCenter(x, y, z) - position).sqrMagnitude;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = index;
                    }
                }
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// <paramref name="storageSlots"/> defaults to maxWeights, which is the ordinary case and keeps
    /// eviction as the bound on everything. Pass more to record nodes the cell will not weight - see
    /// the field's own note for what that costs.
    ///
    /// <paramref name="voxelDensity"/> is carried rather than used: voxelSize is derived from it and
    /// from bounds this type never sees, so a caller holding only the field cannot work backwards to
    /// the setting that produced it. Required rather than optional because a default would be a
    /// plausible zero, and the one thing asking is the golden harness deciding whether a built field
    /// still matches its settings - which must not be answered with a guess.
    /// </summary>
    /// <param name="slotPerNode">
    /// Only pass true when storageSlots is at least the number of nodes that will be added, so that
    /// every node index is a valid slot index and no cell can ever evict. Wrong here would be quiet:
    /// a node index past the end simply falls back to the searching path rather than throwing.
    /// </param>
    /// <param name="seedTerminals">
    /// Carried, not used, for the same reason as voxelDensity. The two seeding flags decide the
    /// source lengths the caller passes to AddDeformationNode, so they change every distance in the
    /// field - and a field built under one of them is not comparable with a field built under the
    /// other. The golden harness reads them back to refuse a comparison across that change.
    /// </param>
    /// <param name="blendMode">
    /// How CreateBlender combines the node transforms, with rotationBlend and scaleBlend the two
    /// halves of that for a decomposed mode. Carried rather than used during the build - the blend
    /// happens per query - but they change every position this field produces, so they are recorded
    /// alongside the settings that do shape the build.
    /// </param>
    public FullDeformationField(float voxelSize, float voxelDensity, int maxWeights, int storageSlots = -1, bool slotPerNode = false, EDFieldConnectivity connectivity = EDFieldConnectivity.Faces6,
                                bool seedTerminals = false, bool seedCorridors = false, EDFieldBlendMode blendMode = EDFieldBlendMode.LinearAffine,
                                EDFieldRotationBlend rotationBlend = EDFieldRotationBlend.Chordal, EDFieldScaleBlend scaleBlend = EDFieldScaleBlend.Full)
    {
        this.voxelSize = Mathf.Max(voxelSize, DistanceEpsilon);
        this.voxelDensity = voxelDensity;
        this.maxWeights = Mathf.Max(1, maxWeights);
        this.storageSlots = Mathf.Max(this.maxWeights, storageSlots);
        this.slotPerNode = slotPerNode;
        this.connectivity = connectivity;
        this.seedTerminals = seedTerminals;
        this.seedCorridors = seedCorridors;
        this.blendMode = blendMode;
        this.rotationBlend = rotationBlend;
        this.scaleBlend = scaleBlend;

        voxelData = new VoxelData<DeformationFieldWeights>();
        deformationNodes = new();
        restInverses = new();
    }

    public void FillWithMesh(List<Mesh> meshes, List<Matrix4x4> transformMatrices)
    {
        InvalidateTrilinearRegions();

        // Repaired here rather than trusted, because a zero would not throw: it would size every
        // cell to zero slots and the field would come out uniformly empty, which reads as a field
        // that reaches nothing rather than as a broken one. The constructor sets it correctly - this
        // covers an instance that arrived any other way.
        if (storageSlots < maxWeights) storageSlots = Mathf.Max(1, maxWeights);

        VoxelizerIntersectionCPU.Voxelize(voxelData, meshes, transformMatrices, voxelSize, fillEmpty: true);

        for (int i = 0; i < voxelData.data.Length; i++)
        {
            voxelData.data[i].SetupWeights(storageSlots);
        }
    }

    static Matrix4x4 ComputeRestInverse(Frame frame)
    {
        Matrix4x4 m = frame.ToMatrix();

        // A degenerate frame (zero or collinear axes) can't be inverted; fall
        // back to a translation-only frame so the node still contributes.
        if (Mathf.Abs(m.determinant) < DistanceEpsilon)
        {
            m = new Frame(frame.position).ToMatrix();
        }

        return m.inverse;
    }

    public int AddDeformationNode(Vector3 position, float sourceLength)
    {
        return AddDeformationNode(new Frame(position), sourceLength);
    }

    public int AddDeformationNode(Vector3 position, Vector3 right, Vector3 up, Vector3 forward, float sourceLength)
    {
        return AddDeformationNode(new Frame(position, right, up, forward), sourceLength);
    }

    public int AddDeformationNode(Frame frame, float sourceLength)
    {
        InvalidateTrilinearRegions();

        var defNode = new DeformationNode(frame, sourceLength);
        deformationNodes.Add(defNode);
        restInverses.Add(ComputeRestInverse(frame));

        int nodeIndex = deformationNodes.Count - 1;

        if (!HasVoxelData())
            return nodeIndex;

        MinHeap heap = new();

        if (sourceLength <= DistanceEpsilon)
        {
            AddSeed(frame.position, frame.position, frame.position, nodeIndex, heap);
        }
        else
        {
            Vector3 direction = frame.right.normalized;
            Vector3 start = frame.position - direction * (sourceLength * 0.5f);
            Vector3 end = frame.position + direction * (sourceLength * 0.5f);

            // The sampling decides *which* cells are seeds and nothing else. The distance each one
            // carries is computed from the bar itself, in AddSeed - so the sample spacing cannot get
            // into the distances, which is the whole reason it is safe to sample here at all.
            //
            // We're using samples every voxelSize * 0.5, but to be really correct, we could use a 3D DDA, but the advantages don't seem really great for this.
            float stepLength = voxelSize * 0.5f;

            int stepCount = Mathf.Max(1, Mathf.CeilToInt(sourceLength / stepLength));

            for (int i = 0; i <= stepCount; i++)
            {
                float t = i / (float)stepCount;
                Vector3 samplePosition = Vector3.Lerp(start, end, t);

                AddSeed(samplePosition, start, end, nodeIndex, heap);
            }
        }

        while (heap.TryDequeue(out QueueItem current))
        {
            DeformationFieldWeights currentElement = voxelData.data[current.voxelIndex];
            float storedDistance = GetDistanceForNode(currentElement, nodeIndex);

            // Ignore stale entries left behind by previous improvements.
            if (Mathf.Abs(storedDistance - current.distance) > DistanceEpsilon)
            {
                continue;
            }

            Vector3Int currentPos = PositionOf(current.voxelIndex);

            Vector3Int[] offsets = NeighbourOffsets();

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3Int offset = offsets[i];
                int nx = currentPos.x + offset.x;
                int ny = currentPos.y + offset.y;
                int nz = currentPos.z + offset.z;

                if (!IsInside(nx, ny, nz)) continue;

                int neighbourIndex = IndexOf(nx, ny, nz);
                if (!voxelData.data[neighbourIndex].IsOccupied()) continue;

                // Measuring through the solid, so a diagonal may not slide across a gap in it.
                if (!StepIsClear(currentPos, offset, true)) continue;

                float newDistance = current.distance + StepCost(offset);

                ref DeformationFieldWeights neighbour = ref voxelData.data[neighbourIndex];
                if (TryStoreDistance(ref neighbour, nodeIndex, newDistance))
                {
                    heap.Enqueue(new QueueItem(neighbourIndex, nodeIndex, newDistance));
                }
            }
        }

        return nodeIndex;
    }

    /// <summary>
    /// Starts <paramref name="nodeIndex"/>'s wavefront in the cell nearest <paramref name="position"/>,
    /// labelled with its true distance to the source rather than with zero.
    ///
    /// **The label is the distance to the source segment, not to the sample that found the cell.**
    /// A seed used to store 0f, which is not a fact about the geometry - it is where the seed happened
    /// to snap. A cell's real distance to the source is anywhere in [0, voxelSize*sqrt(3)/2], and
    /// discarding that was what made exact zeros, and therefore exact ties, the normal case: the
    /// even-split branch in ComputeWeights then fires wherever two sources reach the same cell, and
    /// there it is a step function of how many did. Storing the real distance means almost no cell is
    /// exactly zero and that branch stops firing on its own, without touching the mapping.
    ///
    /// Distance to the *segment* and not to the sample, because the bar is the source set S and the
    /// quantity being propagated is min over q in S of d(x, q) - point-to-segment is that minimum,
    /// exactly, while distance-to-nearest-sample would ripple along the bar with the sample period at
    /// an amplitude comparable to the sub-voxel distances themselves. That would be an artifact as
    /// large as the thing it was added to measure.
    ///
    /// Measured against the voxel centre because that is the point FindClosestFilledVoxel compares
    /// and the point the relaxation's step costs run between. A point source passes start == end, and
    /// the segment distance degenerates to the point distance on its own.
    ///
    /// *No dedup.* Several samples along the bar do land in the same cell, but they now all compute
    /// the same distance from the same segment, so TryStoreDistance rejects the repeats as
    /// non-improvements. The HashSet that used to sit here was harmless only because every seed was
    /// zero; the moment seeds carry a real distance it silently becomes first-sample-wins instead of
    /// nearest-wins.
    /// </summary>
    void AddSeed(Vector3 position, Vector3 sourceStart, Vector3 sourceEnd, int nodeIndex, MinHeap heap)
    {
        int startIndex = FindClosestFilledVoxel(position);
        if (startIndex < 0) return;

        Vector3Int voxel = PositionOf(startIndex);

        float distance = LineHelpers.Distance(sourceStart, sourceEnd, VoxelCenter(voxel.x, voxel.y, voxel.z), out _);

        ref DeformationFieldWeights startVoxel = ref voxelData.data[startIndex];

        if (TryStoreDistance(ref startVoxel, nodeIndex, distance))
        {
            heap.Enqueue(new QueueItem(startIndex, nodeIndex, distance));
        }
    }

    public void GrowInfluence()
    {
        if (!HasVoxelData()) return;

        InvalidateTrilinearRegions();

        MinHeap heap = new();

        // Seed the diffusion from the occupied volume. Empty cells are allowed
        // to receive influences, but they are not marked as occupied.
        for (int index = 0; index < voxelData.data.Length; index++)
        {
            DeformationFieldWeights element = voxelData.data[index];
            if (!element.IsOccupied()) continue;
            if (!HasInfluence(element)) continue;

            for (int i = 0; i < storageSlots; i++)
            {
                if (element.nodeId[i] < 0) continue;
                if (element.distances[i] >= float.MaxValue) continue;

                heap.Enqueue(new QueueItem(index, element.nodeId[i], element.distances[i]));
            }
        }

        while (heap.TryDequeue(out QueueItem current))
        {
            DeformationFieldWeights currentElement = voxelData.data[current.voxelIndex];
            float storedDistance = GetDistanceForNode(currentElement, current.nodeIndex);

            if (Mathf.Abs(storedDistance - current.distance) > DistanceEpsilon)
            {
                continue;
            }

            Vector3Int currentPos = PositionOf(current.voxelIndex);

            Vector3Int[] offsets = NeighbourOffsets();

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3Int offset = offsets[i];
                int nx = currentPos.x + offset.x;
                int ny = currentPos.y + offset.y;
                int nz = currentPos.z + offset.z;

                if (!IsInside(nx, ny, nz)) continue;

                int neighbourIndex = IndexOf(nx, ny, nz);

                // Do not allow the outside diffusion to create shortcuts through
                // the volume. Occupied cells already have their geodesic distances
                // computed by AddDeformationNode().
                if (voxelData.data[neighbourIndex].IsOccupied()) continue;

                // The same rule for diagonals: growing outside the solid, a corner step must not
                // round the corner of it.
                if (!StepIsClear(currentPos, offset, false)) continue;

                float newDistance = current.distance + StepCost(offset);

                ref DeformationFieldWeights neighbour = ref voxelData.data[neighbourIndex];
                if (TryStoreDistance(ref neighbour, current.nodeIndex, newDistance))
                {
                    heap.Enqueue(new QueueItem(neighbourIndex, current.nodeIndex, newDistance));
                }
            }
        }
    }

    /// <summary>
    /// Turns the stored per-cell distances into blend weights, through <paramref name="resolver"/>.
    ///
    /// A null resolver means the legacy inverse-distance mapping, and is recorded as that rather than
    /// as the absence of a choice - the descriptor written below is what the field resolved to, not
    /// what it was asked for, so a builder that failed to produce a resolver shows up in the dump
    /// instead of quietly producing legacy weights.
    /// </summary>
    public void ComputeWeights(int maxWeights = -1, WeightResolver resolver = null)
    {
        if (!HasVoxelData()) return;

        InvalidateTrilinearRegions();

        if (resolver == null) resolver = new InverseDistanceWeights();

        weightDescriptor = resolver.Describe();

        // Still clamped to maxWeights, never to storageSlots. This is what keeps extra stored nodes
        // inert: the sort above puts the nearest first, so weighting only the leading weightCount
        // produces exactly the weights a cell that had evicted the rest would have produced.
        int weightCount = (maxWeights < 0) ? (this.maxWeights) : (maxWeights);
        weightCount = Mathf.Clamp(weightCount, 0, this.maxWeights);

        // One set of buffers for the whole pass rather than one per cell. This loop is
        // single-threaded, so there is nothing to share them with, and a resolver returning a fresh
        // array instead would allocate once for every cell in the grid.
        int[] gatheredSlots = new int[weightCount];
        float[] gatheredDistances = new float[weightCount];
        float[] gatheredWeights = new float[weightCount];

        for (int index = 0; index < voxelData.data.Length; index++)
        {
            ref DeformationFieldWeights element = ref voxelData.data[index];
            EnsureWeights(ref element);
            SortInfluences(ref element);

            // Every slot, so the ones past weightCount are left at zero rather than holding whatever
            // a previous ComputeWeights put there.
            for (int i = 0; i < storageSlots; i++)
            {
                element.weights[i] = 0f;
            }

            if (weightCount == 0) continue;

            // Gathered into a dense array rather than walked in place, because a resolver is a
            // function of the whole set and cannot be given holes to skip. Empty slots and MaxValue
            // are dropped here so no resolver has to know they exist; slot order is preserved, so the
            // distances stay sorted nearest-first and the summation order inside a resolver is the
            // order the original code summed in.
            int validCount = 0;

            for (int i = 0; i < weightCount; i++)
            {
                if (element.nodeId[i] < 0) continue;
                if (element.distances[i] >= float.MaxValue) continue;

                gatheredSlots[validCount] = i;
                gatheredDistances[validCount] = element.distances[i];

                validCount++;
            }

            if (validCount == 0) continue;

            resolver.ComputeCellWeights(gatheredDistances, validCount, gatheredWeights);

            for (int i = 0; i < validCount; i++)
            {
                element.weights[gatheredSlots[i]] = gatheredWeights[i];
            }
        }
    }

    Vector3Int ClampVoxelPosition(Vector3Int p)
    {
        if (!HasVoxelData())
            return Vector3Int.zero;

        p.x = Mathf.Clamp(p.x, 0, voxelData.gridSize.x - 1);
        p.y = Mathf.Clamp(p.y, 0, voxelData.gridSize.y - 1);
        p.z = Mathf.Clamp(p.z, 0, voxelData.gridSize.z - 1);

        return p;
    }

    public DeformationFieldWeights GetWeights(Vector3 position)
    {
        if (!HasVoxelData())
        {
            DeformationFieldWeights empty = default;
            empty.SetupWeights(storageSlots);
            return empty;
        }

        Vector3Int localPos = WorldToVoxel(position);
        localPos = ClampVoxelPosition(localPos);

        int index = IndexOf(localPos.x, localPos.y, localPos.z);

        ref DeformationFieldWeights weights = ref voxelData.data[index];

        // Defensive: useful if GetWeights is called before ComputeWeights(), or if some cells were not initialized for any reason.
        EnsureWeights(ref weights);

        return weights;
    }

    // Per-node deformation: maps a point from the node's rest frame to its current frame (affine, so scale/shear in the frames is carried through).
    Matrix4x4 NodeDeformMatrix(int nodeIndex, Frame currentFrame)
    {
        return currentFrame.ToMatrix() * restInverses[nodeIndex];
    }

    static void AccumulateMatrix(ref Matrix4x4 accumulator, Matrix4x4 m, float weight)
    {
        for (int i = 0; i < 16; i++)
        {
            accumulator[i] += m[i] * weight;
        }
    }

    public Vector3 DeformPositionFromNodePositions(Vector3 position, List<Vector3> currentNodePositions)
    {
        if (currentNodePositions == null)
            return position;

        DeformationFieldWeights fieldWeights = GetWeights(position);

        Vector3 displacement = Vector3.zero;
        float weightSum = 0.0f;

        if ((fieldWeights.weights == null) || (fieldWeights.nodeId == null))
            return position;

        for (int i = 0; i < fieldWeights.weights.Length; i++)
        {
            float weight = fieldWeights.weights[i];
            int nodeIndex = fieldWeights.nodeId[i];

            if (weight <= 0.0f) continue;
            if (nodeIndex < 0) continue;
            if (nodeIndex >= deformationNodes.Count) continue;
            if (nodeIndex >= currentNodePositions.Count) continue;

            Vector3 restNodePosition = deformationNodes[nodeIndex].frame.position;
            Vector3 currentNodePosition = currentNodePositions[nodeIndex];

            displacement += weight * (currentNodePosition - restNodePosition);
            weightSum += weight;
        }

        if (weightSum <= DistanceEpsilon) return position;

        // Defensive normalization in case the cell has partial/invalid weights.
        displacement /= weightSum;

        return position + displacement;
    }

    // Override uses a function instead of a vector with the positions (helpful in some cases)
    public Vector3 DeformPositionFromNodePositions(Vector3 position, Func<int, Vector3> getCurrentNodePosition)
    {
        if (getCurrentNodePosition == null) return position;

        DeformationFieldWeights fieldWeights = GetWeights(position);

        Vector3 displacement = Vector3.zero;
        float weightSum = 0.0f;

        if ((fieldWeights.weights == null) || (fieldWeights.nodeId == null)) return position;

        for (int i = 0; i < fieldWeights.weights.Length; i++)
        {
            float weight = fieldWeights.weights[i];
            int nodeIndex = fieldWeights.nodeId[i];

            if (weight <= 0.0f) continue;
            if (nodeIndex < 0) continue;
            if (nodeIndex >= deformationNodes.Count) continue;

            Vector3 restNodePosition = deformationNodes[nodeIndex].frame.position;
            Vector3 currentNodePosition = getCurrentNodePosition(nodeIndex);

            displacement += weight * (currentNodePosition - restNodePosition);
            weightSum += weight;
        }

        if (weightSum <= DistanceEpsilon) return position;

        displacement /= weightSum;

        return position + displacement;
    }

    // The blends that used to live here - position and matrix, cell and trilinear - are now
    // TransformBlender.DeformPosition and TransformBlender.TryGetMatrix. They moved rather than being
    // wrapped: each of them recomputed every node's transform per vertex, and a wrapper would have
    // had to build a blender per call to hide that, which is the opposite of the point.
    //
    // DeformPositionFromNodePositions above stays as it is. It blends displacements between rest and
    // current node *positions*, with no transform to combine, so there is nothing here for a blender
    // to decide.

    public Vector3 DeformPositionFromSingleNodeFrame(Vector3 position, int nodeIndex, Func<int, Frame> getCurrentNodeFrame)
    {
        if (getCurrentNodeFrame == null) return position;

        if (nodeIndex < 0) return position;

        if (nodeIndex >= deformationNodes.Count) return position;

        Matrix4x4 m = NodeDeformMatrix(nodeIndex, getCurrentNodeFrame(nodeIndex));

        return m.MultiplyPoint3x4(position);
    }

    void InvalidateTrilinearRegions()
    {
        trilinearRegionsDirty = true;
        trilinearRegions = null;
        trilinearInfluences = null;
        trilinearRegionGridSize = Vector3Int.zero;
    }

    object GetTrilinearBuildLock()
    {
        if (trilinearBuildLock != null)
            return trilinearBuildLock;

        object newLock = new object();
        object existing = System.Threading.Interlocked.CompareExchange(
            ref trilinearBuildLock,
            newLock,
            null);

        return existing ?? newLock;
    }

    bool HasValidTrilinearRegions()
    {
        if (trilinearRegionsDirty) return false;
        if (trilinearRegions == null) return false;
        if (trilinearInfluences == null) return false;

        if (!HasVoxelData())
        {
            return trilinearRegions.Length == 0 &&
                   trilinearInfluences.Length == 0;
        }

        Vector3Int expectedSize = voxelData.gridSize + Vector3Int.one;
        if (trilinearRegionGridSize != expectedSize) return false;

        long expectedCount =
            (long)expectedSize.x *
            expectedSize.y *
            expectedSize.z;

        return expectedCount <= int.MaxValue &&
               trilinearRegions.Length == (int)expectedCount;
    }

    public bool trilinearRegionsBuilt => HasValidTrilinearRegions();
    public int trilinearRegionCount => trilinearRegions?.Length ?? 0;
    public int trilinearInfluenceCount => trilinearInfluences?.Length ?? 0;

    static void AddTrilinearCornerWeight(ref TrilinearInfluence influence, int cornerIndex, float weight)
    {
        if (cornerIndex < 4)
        {
            Vector4 values = influence.lowZ;
            values[cornerIndex] += weight;
            influence.lowZ = values;
        }
        else
        {
            Vector4 values = influence.highZ;
            values[cornerIndex - 4] += weight;
            influence.highZ = values;
        }
    }

    int TrilinearRegionIndex(int x, int y, int z)
    {
        return x + trilinearRegionGridSize.x * (y + trilinearRegionGridSize.y * z);
    }

    // Explicit build entry point. This can be called once on the main thread
    // before launching a parallel deformation pass. Trilinear sampling also
    // calls EnsureTrilinearRegionsBuilt(), so explicit construction is optional.
    public void BuildTrilinearRegions()
    {
        lock (GetTrilinearBuildLock())
        {
            BuildTrilinearRegionsInternal();
        }
    }

    void EnsureTrilinearRegionsBuilt()
    {
        if (HasValidTrilinearRegions())
            return;

        lock (GetTrilinearBuildLock())
        {
            if (!HasValidTrilinearRegions())
            {
                BuildTrilinearRegionsInternal();
            }
        }
    }

    void BuildTrilinearRegionsInternal()
    {
        if (!HasVoxelData())
        {
            trilinearRegionGridSize = Vector3Int.zero;
            trilinearRegions = Array.Empty<TrilinearRegion>();
            trilinearInfluences = Array.Empty<TrilinearInfluence>();
            trilinearRegionsDirty = false;
            return;
        }

        Vector3Int regionSize = voxelData.gridSize + Vector3Int.one;

        long regionCountLong = (long)regionSize.x * regionSize.y * regionSize.z;

        if (regionCountLong > int.MaxValue)
        {
            throw new InvalidOperationException($"Trilinear region grid is too large: {regionSize}.");
        }

        TrilinearRegion[] newRegions = new TrilinearRegion[(int)regionCountLong];

        List<TrilinearInfluence> allInfluences = new();
        List<TrilinearInfluence> regionInfluences = new(Mathf.Max(1, 8 * maxWeights));

        Dictionary<int, int> influenceSlotByNode = new(Mathf.Max(1, 8 * maxWeights));

        int regionIndex = 0;

        // Region coordinate r corresponds to base cell coordinate b = r - 1.
        // This includes one clamped interpolation region on each low border.
        for (int rz = 0; rz < regionSize.z; rz++)
        {
            int bz = rz - 1;

            for (int ry = 0; ry < regionSize.y; ry++)
            {
                int by = ry - 1;

                for (int rx = 0; rx < regionSize.x; rx++, regionIndex++)
                {
                    int bx = rx - 1;

                    influenceSlotByNode.Clear();
                    regionInfluences.Clear();

                    for (int dz = 0; dz <= 1; dz++)
                    {
                        int z = Mathf.Clamp(bz + dz, 0, voxelData.gridSize.z - 1);

                        for (int dy = 0; dy <= 1; dy++)
                        {
                            int y = Mathf.Clamp(by + dy, 0, voxelData.gridSize.y - 1);

                            for (int dx = 0; dx <= 1; dx++)
                            {
                                int x = Mathf.Clamp(bx + dx, 0, voxelData.gridSize.x - 1);

                                int cornerIndex = dx + 2 * dy + 4 * dz;

                                DeformationFieldWeights element = voxelData.data[IndexOf(x, y, z)];

                                if ((element.nodeId == null) || (element.weights == null)) continue;

                                int influenceCount = Mathf.Min(element.nodeId.Length, element.weights.Length);

                                for (int i = 0; i < influenceCount; i++)
                                {
                                    int nodeIndex = element.nodeId[i];
                                    float weight = element.weights[i];

                                    if (nodeIndex < 0) continue;
                                    if (weight <= 0f) continue;

                                    if (!influenceSlotByNode.TryGetValue(nodeIndex, out int slot))
                                    {
                                        slot = regionInfluences.Count;
                                        influenceSlotByNode.Add(nodeIndex, slot);

                                        regionInfluences.Add(new TrilinearInfluence 
                                        {
                                            nodeId = nodeIndex,
                                            lowZ = Vector4.zero,
                                            highZ = Vector4.zero,
                                        });
                                    }

                                    TrilinearInfluence influence = regionInfluences[slot];

                                    AddTrilinearCornerWeight(ref influence, cornerIndex, weight);

                                    regionInfluences[slot] = influence;
                                }
                            }
                        }
                    }

                    newRegions[regionIndex] = new TrilinearRegion
                    {
                        influenceStart = allInfluences.Count,
                        influenceCount = regionInfluences.Count,
                    };

                    allInfluences.AddRange(regionInfluences);
                }
            }
        }

        // Publish complete immutable arrays only after the build has finished.
        trilinearRegionGridSize = regionSize;
        trilinearRegions = newRegions;
        trilinearInfluences = allInfluences.ToArray();
        trilinearRegionsDirty = false;
    }

    static float InterpolateTrilinearWeight(TrilinearInfluence influence, float fx, float fy, float fz)
    {
        float lowZ = Mathf.Lerp(Mathf.Lerp(influence.lowZ.x, influence.lowZ.y, fx), Mathf.Lerp(influence.lowZ.z, influence.lowZ.w, fx), fy);

        float highZ = Mathf.Lerp(Mathf.Lerp(influence.highZ.x, influence.highZ.y, fx), Mathf.Lerp(influence.highZ.z, influence.highZ.w, fx), fy);

        return Mathf.Lerp(lowZ, highZ, fz);
    }

    bool TryGetTrilinearRegion(Vector3 position, out TrilinearRegion region, out float fx, out float fy, out float fz)
    {
        region = default;
        fx = fy = fz = 0f;

        if (!HasVoxelData()) return false;

        Vector3 local = position - voxelData.minBound;

        float cx = local.x / voxelData.voxelSize.x - 0.5f;
        float cy = local.y / voxelData.voxelSize.y - 0.5f;
        float cz = local.z / voxelData.voxelSize.z - 0.5f;

        int rawBx = Mathf.FloorToInt(cx);
        int rawBy = Mathf.FloorToInt(cy);
        int rawBz = Mathf.FloorToInt(cz);

        fx = cx - rawBx;
        fy = cy - rawBy;
        fz = cz - rawBz;

        int bx = Mathf.Clamp(rawBx, -1, voxelData.gridSize.x - 1);
        int by = Mathf.Clamp(rawBy, -1, voxelData.gridSize.y - 1);
        int bz = Mathf.Clamp(rawBz, -1, voxelData.gridSize.z - 1);

        int rx = bx + 1;
        int ry = by + 1;
        int rz = bz + 1;

        region = trilinearRegions[TrilinearRegionIndex(rx, ry, rz)];

        return true;
    }

    /// <summary>
    /// Which cell a position reads its weights from - the same lookup GetWeights makes, clamped the
    /// same way.
    ///
    /// For diagnostics only, and specifically so a tool can tell "the numbers changed" apart from
    /// "I crossed a cell boundary". The per-cell weights are piecewise constant by construction, so
    /// without the cell coordinate those two are indistinguishable in a readout.
    /// </summary>
    public Vector3Int GetVoxelCoordinate(Vector3 position)
    {
        if (!HasVoxelData()) return new Vector3Int(-1, -1, -1);

        return ClampVoxelPosition(WorldToVoxel(position));
    }

    /// <summary>
    /// The weights trilinear sampling actually applies at a position, rather than the containing
    /// cell's own weights.
    ///
    /// This is what deforms geometry - a TransformBlender asked for the trilinear blend combines node transforms
    /// with exactly these numbers, normalized by exactly this sum - so a tool reporting them is
    /// reporting the deformation rather than an ingredient of it. The per-cell weights that
    /// GetWeights returns are a step function across cell boundaries and will jump for a movement of
    /// a fraction of a voxel; these will not.
    ///
    /// There is deliberately no interpolated *distance* to go with them. A distance is a per-cell
    /// quantity that the weights were computed from, and inventing a blended one would put a number
    /// in front of a reader that nothing in the field ever computed.
    /// </summary>
    public bool TryGetTrilinearInfluences(Vector3 position, out int[] nodeIds, out float[] blendedWeights)
    {
        nodeIds = null;
        blendedWeights = null;

        EnsureTrilinearRegionsBuilt();

        if (!TryGetTrilinearRegion(position, out TrilinearRegion region, out float fx, out float fy, out float fz))
        {
            return false;
        }

        int influenceEnd = region.influenceStart + region.influenceCount;

        var ids = new List<int>(region.influenceCount);
        var values = new List<float>(region.influenceCount);

        float weightSum = 0f;

        for (int i = region.influenceStart; i < influenceEnd; i++)
        {
            TrilinearInfluence influence = trilinearInfluences[i];

            float weight = InterpolateTrilinearWeight(influence, fx, fy, fz);

            if (weight <= 0f) continue;
            if (influence.nodeId < 0) continue;

            ids.Add(influence.nodeId);
            values.Add(weight);

            weightSum += weight;
        }

        if (weightSum <= DistanceEpsilon) return false;

        for (int i = 0; i < values.Count; i++)
        {
            values[i] /= weightSum;
        }

        nodeIds = ids.ToArray();
        blendedWeights = values.ToArray();

        return true;
    }

}

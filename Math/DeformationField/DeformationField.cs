using UC;
using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class DeformationField
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
                return (other.distances == null) && (other.weights == null) && (other.nodeId == null);
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

    VoxelData<DeformationFieldWeights> voxelData;
    float                voxelSize;
    int                  maxWeights;
    List<Vector3>        deformationNodes = new List<Vector3>();

    public Vector3Int gridSize => voxelData?.gridSize ?? Vector3Int.zero;
    public Vector3 cellSize => Vector3.one * voxelSize;
    public Vector3 minBound => voxelData?.minBound ?? Vector3.zero;

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

    struct QueueItem
    {
        public int   voxelIndex;
        public int   nodeIndex;
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

    Vector3 VoxelCenter(int index)
    {
        var p = PositionOf(index);
        return VoxelCenter(p.x, p.y, p.z);
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
        if ((element.distances == null) ||
            (element.weights == null) ||
            (element.nodeId == null) ||
            (element.distances.Length != maxWeights) ||
            (element.weights.Length != maxWeights) ||
            (element.nodeId.Length != maxWeights))
        {
            element.SetupWeights(maxWeights);
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

    void SortInfluences(ref DeformationFieldWeights element)
    {
        EnsureWeights(ref element);

        for (int i = 0; i < maxWeights - 1; i++)
        {
            int best = i;
            float bestDistance = (element.nodeId[i] >= 0) ? element.distances[i] : float.MaxValue;

            for (int j = i + 1; j < maxWeights; j++)
            {
                float d = (element.nodeId[j] >= 0) ? element.distances[j] : float.MaxValue;
                if (d < bestDistance)
                {
                    best = j;
                    bestDistance = d;
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

        int existingSlot = -1;
        int emptySlot = -1;

        for (int i = 0; i < maxWeights; i++)
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

        int targetSlot = emptySlot >= 0 ? emptySlot : maxWeights - 1;

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

    public DeformationField(float voxelSize, int maxWeights)
    {
        this.voxelSize = Mathf.Max(voxelSize, DistanceEpsilon);
        this.maxWeights = Mathf.Max(1, maxWeights);

        voxelData = new VoxelData<DeformationFieldWeights>();
        deformationNodes = new();
    }

    public void FillWithMesh(List<Mesh> meshes, List<Matrix4x4> transformMatrices)
    {
        VoxelizerIntersectionCPU.Voxelize(voxelData, meshes, transformMatrices, voxelSize, fillEmpty: true);

        for (int i = 0; i < voxelData.data.Length; i++)
        {
            voxelData.data[i].SetupWeights(maxWeights);
        }
    }

    public int AddDeformationNode(Vector3 position)
    {
        deformationNodes.Add(position);
        int nodeIndex = deformationNodes.Count - 1;

        int startIndex = FindClosestFilledVoxel(position);
        if (startIndex < 0)
        {
            return nodeIndex;
        }

        MinHeap heap = new();

        ref DeformationFieldWeights startVoxel = ref voxelData.data[startIndex];
        if (TryStoreDistance(ref startVoxel, nodeIndex, 0f))
        {
            heap.Enqueue(new QueueItem(startIndex, nodeIndex, 0f));
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

            for (int i = 0; i < FaceNeighbourOffsets.Length; i++)
            {
                Vector3Int offset = FaceNeighbourOffsets[i];
                int nx = currentPos.x + offset.x;
                int ny = currentPos.y + offset.y;
                int nz = currentPos.z + offset.z;

                if (!IsInside(nx, ny, nz)) continue;

                int neighbourIndex = IndexOf(nx, ny, nz);
                if (!voxelData.data[neighbourIndex].IsOccupied()) continue;

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

    public void GrowInfluence()
    {
        if (!HasVoxelData()) return;

        MinHeap heap = new();

        // Seed the diffusion from the occupied volume. Empty cells are allowed
        // to receive influences, but they are not marked as occupied.
        for (int index = 0; index < voxelData.data.Length; index++)
        {
            DeformationFieldWeights element = voxelData.data[index];
            if (!element.IsOccupied()) continue;
            if (!HasInfluence(element)) continue;

            for (int i = 0; i < maxWeights; i++)
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

            for (int i = 0; i < FaceNeighbourOffsets.Length; i++)
            {
                Vector3Int offset = FaceNeighbourOffsets[i];
                int nx = currentPos.x + offset.x;
                int ny = currentPos.y + offset.y;
                int nz = currentPos.z + offset.z;

                if (!IsInside(nx, ny, nz)) continue;

                int neighbourIndex = IndexOf(nx, ny, nz);

                // Do not allow the outside diffusion to create shortcuts through
                // the volume. Occupied cells already have their geodesic distances
                // computed by AddDeformationNode().
                if (voxelData.data[neighbourIndex].IsOccupied()) continue;

                float newDistance = current.distance + StepCost(offset);

                ref DeformationFieldWeights neighbour = ref voxelData.data[neighbourIndex];
                if (TryStoreDistance(ref neighbour, current.nodeIndex, newDistance))
                {
                    heap.Enqueue(new QueueItem(neighbourIndex, current.nodeIndex, newDistance));
                }
            }
        }
    }

    public void ComputeWeights(int maxWeights = -1)
    {
        if (!HasVoxelData()) return;

        int weightCount = maxWeights < 0 ? this.maxWeights : maxWeights;
        weightCount = Mathf.Clamp(weightCount, 0, this.maxWeights);

        for (int index = 0; index < voxelData.data.Length; index++)
        {
            ref DeformationFieldWeights element = ref voxelData.data[index];
            EnsureWeights(ref element);
            SortInfluences(ref element);

            for (int i = 0; i < this.maxWeights; i++)
            {
                element.weights[i] = 0f;
            }

            if (weightCount == 0) continue;

            int validCount = 0;
            int zeroDistanceCount = 0;

            for (int i = 0; i < weightCount; i++)
            {
                if (element.nodeId[i] < 0) continue;
                if (element.distances[i] >= float.MaxValue) continue;

                validCount++;

                if (element.distances[i] <= DistanceEpsilon)
                {
                    zeroDistanceCount++;
                }
            }

            if (validCount == 0) continue;

            if (zeroDistanceCount > 0)
            {
                float weight = 1f / zeroDistanceCount;

                for (int i = 0; i < weightCount; i++)
                {
                    if (element.nodeId[i] >= 0 && element.distances[i] <= DistanceEpsilon)
                    {
                        element.weights[i] = weight;
                    }
                }

                continue;
            }

            float invDistanceSum = 0f;

            for (int i = 0; i < weightCount; i++)
            {
                if (element.nodeId[i] < 0) continue;
                if (element.distances[i] >= float.MaxValue) continue;

                invDistanceSum += 1f / Mathf.Max(element.distances[i], DistanceEpsilon);
            }

            if (invDistanceSum <= 0f) continue;

            for (int i = 0; i < weightCount; i++)
            {
                if (element.nodeId[i] < 0) continue;
                if (element.distances[i] >= float.MaxValue) continue;

                element.weights[i] = (1f / Mathf.Max(element.distances[i], DistanceEpsilon)) / invDistanceSum;
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
            empty.SetupWeights(maxWeights);
            return empty;
        }

        Vector3Int localPos = WorldToVoxel(position);
        localPos = ClampVoxelPosition(localPos);

        int index = IndexOf(localPos.x, localPos.y, localPos.z);

        ref DeformationFieldWeights weights = ref voxelData.data[index];

        // Defensive: useful if GetWeights is called before ComputeWeights(),
        // or if some cells were not initialized for any reason.
        EnsureWeights(ref weights);

        return weights;
    }
}

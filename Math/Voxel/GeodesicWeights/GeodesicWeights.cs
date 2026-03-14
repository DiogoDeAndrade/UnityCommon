using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using UC;
using UnityEngine;

[Serializable]
public class GeodesicWeights
{
    [Serializable]
    private class BoneSegment
    {
        public int boneId;
        public int parentId;
        public Vector3 start;
        public Vector3 end;
        public VoxelData<float> distances;
    }

    private struct QueueNode : IComparable<QueueNode>
    {
        public int      x, y, z;
        public float    dist;
        public int      serial;

        public int CompareTo(QueueNode other)
        {
            int c = dist.CompareTo(other.dist);
            if (c != 0) return c;
            c = x.CompareTo(other.x);
            if (c != 0) return c;
            c = y.CompareTo(other.y);
            if (c != 0) return c;
            c = z.CompareTo(other.z);
            if (c != 0) return c;
            return serial.CompareTo(other.serial);
        }
    }


    [SerializeField] private List<BoneSegment>  bones;
    [SerializeField] private VoxelData<byte>    baseVoxelData;

    private List<Vector3Int> validVoxelCoords = new List<Vector3Int>();
    private List<Vector3> validVoxelCenters = new List<Vector3>();
    private Dictionary<Vector3Int, Vector3Int> nearestValidVoxelCache = new Dictionary<Vector3Int, Vector3Int>();

    public GeodesicWeights(VoxelData<byte> voxelData)
    {
        baseVoxelData = voxelData;
        bones = new List<BoneSegment>();

        CacheValidVoxels();
    }

    private void CacheValidVoxels()
    {
        validVoxelCoords.Clear();
        validVoxelCenters.Clear();

        Vector3Int size = baseVoxelData.gridSize;

        for (int z = 0; z < size.z; z++)
        {
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    if (!IsValidVoxel(x, y, z))
                        continue;

                    validVoxelCoords.Add(new Vector3Int(x, y, z));
                    validVoxelCenters.Add(baseVoxelData.GetVoxelCenter(x, y, z));
                }
            }
        }
    }

    public int AddBone(int parentId, int boneId, Vector3 start, Vector3 end)
    {
        bones.Add(new BoneSegment() { parentId = parentId, boneId = boneId, start = start, end = end });

        return bones.Count - 1;
    }

    public VoxelData<float> GetDistancesByIndex(int boneIndex)
    {
        if (bones == null) return null;

        if ((boneIndex >= 0) && (boneIndex < bones.Count))
             return bones[boneIndex].distances;

        return null;
    }

    public bool GetBoneByIndex(int boneIndex, out Vector3 start, out Vector3 end)
    {
        start = Vector3.zero;
        end = Vector3.zero;

        if (bones == null) return false;

        if ((boneIndex >= 0) && (boneIndex < bones.Count))
        {
            start = bones[boneIndex].start;
            end = bones[boneIndex].end;
            return true;
        }

        return false;
    }

    public void ComputeDistanceFields()
    {
        foreach (var bone in bones)
        {
            ComputeGeodesicDistance(bone);
        }
    }

    void ComputeGeodesicDistance(BoneSegment bone)
    {
        bone.distances = new VoxelData<float>();
        bone.distances.Init(baseVoxelData.gridSize, baseVoxelData.voxelSize);
        bone.distances.minBound = baseVoxelData.minBound;

        bone.distances.Clear(float.MaxValue);

        Vector3Int size = baseVoxelData.gridSize;

        // Dijkstra priority queue via SortedSet.
        // We allow duplicates and discard stale entries when popped.
        SortedSet<QueueNode> queue = new SortedSet<QueueNode>();
        int serial = 0;

        // 1) Find seed voxels for this bone.
        // We sample the segment densely enough that it crosses voxels reliably.
        HashSet<Vector3Int> seeds = FindBoneSeedVoxels(bone.start, bone.end);

        // If still no seed exists, the volume likely has no valid voxels.
        if (seeds.Count == 0)
            return;

        // 2) Initialize seed distances.
        foreach (var s in seeds)
        {
            bone.distances[s.x, s.y, s.z] = 0f;
            queue.Add(new QueueNode
            {
                x = s.x,
                y = s.y,
                z = s.z,
                dist = 0f,
                serial = serial++
            });
        }

        // 3) Dijkstra over valid voxels only.
        while (queue.Count > 0)
        {
            QueueNode current = queue.Min;
            queue.Remove(current);

            float currentStored = bone.distances[current.x, current.y, current.z];
            if (current.dist > currentStored)
                continue; // stale entry

            Vector3 currentPos = baseVoxelData.GetVoxelCenter(current.x, current.y, current.z);

            foreach (var n in EnumerateValidNeighbors(current.x, current.y, current.z))
            {
                Vector3 neighborPos = baseVoxelData.GetVoxelCenter(n.x, n.y, n.z);
                float stepCost = Vector3.Distance(currentPos, neighborPos);
                float newDist = currentStored + stepCost;

                if (newDist < bone.distances[n.x, n.y, n.z])
                {
                    bone.distances[n.x, n.y, n.z] = newDist;
                    queue.Add(new QueueNode
                    {
                        x = n.x,
                        y = n.y,
                        z = n.z,
                        dist = newDist,
                        serial = serial++
                    });
                }
            }
        }
    }

    private HashSet<Vector3Int> FindBoneSeedVoxels(Vector3 start, Vector3 end)
    {
        HashSet<Vector3Int> result = new HashSet<Vector3Int>();

        float segmentLength = Vector3.Distance(start, end);
        float minVoxel = Mathf.Min(baseVoxelData.voxelSize.x, Mathf.Min(baseVoxelData.voxelSize.y, baseVoxelData.voxelSize.z));
        int steps = Mathf.Max(1, Mathf.CeilToInt(segmentLength / Mathf.Max(minVoxel * 0.5f, 1e-5f)));

        for (int i = 0; i <= steps; i++)
        {
            float t = steps > 0 ? (float)i / steps : 0f;
            Vector3 p = Vector3.Lerp(start, end, t);
            Vector3Int v = baseVoxelData.WorldToVoxel(p);

            if (IsInside(v) && IsValidVoxel(v.x, v.y, v.z))
            {
                result.Add(v);
            }
            else
            {
                Vector3Int closest = FindClosestValidVoxelToPointCached(p);
                if (closest.x >= 0)
                    result.Add(closest);
            }
        }

        return result;
    }

    private Vector3Int FindClosestValidVoxelToPointCached(Vector3 p)
    {
        Vector3Int key = baseVoxelData.WorldToVoxel(p);

        if (nearestValidVoxelCache.TryGetValue(key, out Vector3Int cached))
            return cached;

        Vector3Int best = FindClosestValidVoxelToPoint(p);
        nearestValidVoxelCache[key] = best;
        return best;
    }

    private Vector3Int FindClosestValidVoxelToPoint(Vector3 p)
    {
        Vector3Int best = new Vector3Int(-1, -1, -1);
        float bestSqrDist = float.MaxValue;

        for (int i = 0; i < validVoxelCoords.Count; i++)
        {
            float sqrDist = (validVoxelCenters[i] - p).sqrMagnitude;
            if (sqrDist < bestSqrDist)
            {
                bestSqrDist = sqrDist;
                best = validVoxelCoords[i];
            }
        }

        return best;
    }

    private IEnumerable<Vector3Int> EnumerateValidNeighbors(int x, int y, int z)
    {
        // 26-neighborhood, matching the paper’s spirit better than pure von Neumann.
        // Because the step cost is center-to-center distance, diagonals naturally cost more.
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0 && dz == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    int nz = z + dz;

                    if ((nx < 0) || (ny < 0) || (nz < 0) || (nx >= baseVoxelData.gridSize.x) || (ny >= baseVoxelData.gridSize.y) || (nz >= baseVoxelData.gridSize.z))
                        continue;

                    if (!IsValidVoxel(nx, ny, nz))
                        continue;

                    yield return new Vector3Int(nx, ny, nz);
                }
            }
        }
    }

    private bool IsValidVoxel(int x, int y, int z)
    {
        return baseVoxelData[x, y, z] != 0;
    }

    private bool IsInside(Vector3Int v)
    {
        return (v.x >= 0) && (v.y >= 0) && (v.z >= 0) && (v.x < baseVoxelData.gridSize.x) && (v.y < baseVoxelData.gridSize.y) && (v.z < baseVoxelData.gridSize.z);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class GeodesicDistance
{
    [SerializeField] private TopologyStatic _topology;
    [SerializeField] private int _sourcePointId;
    [SerializeField] private List<float> _distances;
    [SerializeField] private float _maxDistance;

    public TopologyStatic topology
    {
        get => _topology;
        set => _topology = value;
    }

    public int sourcePointId
    {
        get => _sourcePointId;
        set => _sourcePointId = value;
    }

    public bool isComputed => (_topology != null) && (_distances != null) && (_distances.Count > 0);

    public void Build()
    {
        // Start the array at infinity
        _distances = Enumerable.Repeat(float.MaxValue, _topology.vertexCount).ToList();
        _distances[_sourcePointId] = 0.0f;
        _maxDistance = 0.0f;

        List<int> sortedVertex = new() { _sourcePointId };

        while (sortedVertex.Count > 0)
        {
            // Sort list by distance
            sortedVertex.Sort((a, b) => _distances[a].CompareTo(_distances[b]));

            // Get current
            var currentId = sortedVertex.PopFirst();
            var currentPos = _topology.GetPosition(currentId);
            var currentDist = _distances[currentId];

            // Get neighbours
            foreach (var neiId in _topology.GetVertexNeighbours(currentId))
            {
                var neiPos = _topology.GetPosition(neiId);
                float newDistance = currentDist + Vector3.Distance(currentPos, neiPos);

                if (newDistance < _distances[neiId])
                {
                    sortedVertex.Remove(neiId);
                    sortedVertex.Add(neiId);
                    _distances[neiId] = newDistance;
                }
                if (newDistance > _maxDistance)
                {
                    _maxDistance = newDistance;
                }
            }
        }
    }

    public float GetDistance(int index) => _distances[index];
    public float GetMaxDistance() => _maxDistance;

    public float ComputeDistance(int index, float u, float v, float w)
    {
        var triangle = topology.triangles[index];
        var d1 = _distances[triangle.vertices.i1];
        var d2 = _distances[triangle.vertices.i2];
        var d3 = _distances[triangle.vertices.i3];

        return d1 * u + d2 * v + d3 * w;
    }

    public float ComputeDistance(Vector3 position)
    {
        int index = topology.GetClosestTriangle(position, out float u, out float v, out float w);
        if (index != -1)
        {
            return ComputeDistance(index, u, v, w);
        }
        return float.MaxValue;
    }

    public List<Polyline> ComputeContour(float refDistance)
    {
        List<Vector3> vertices = new();
        List<int> indices = new();

        Vector3 ComputeContourPoint(Vector3 p1, Vector3 p2, float l1, float l2, float lRef)
        {
            float delta = l2 - l1;
            float t1 = (lRef - l1) / delta;
            float t2 = (l2 - lRef) / delta;

            return p2 * t1 + p1 * t2;
        }

        int GetOrAddVertex(Vector3 p)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (Vector3.SqrMagnitude(vertices[i] - p) < 1e-3) return i;
            }

            vertices.Add(p);
            return vertices.Count - 1;
        }

        int triIndex = 0;
        foreach (var tri in topology.triangles)
        {
            float l1 = _distances[tri.vertices.i1];
            float l2 = _distances[tri.vertices.i2];
            float l3 = _distances[tri.vertices.i3];
            float s1 = l1 - refDistance;
            float s2 = l2 - refDistance;
            float s3 = l3 - refDistance;
            var p1 = topology.GetPosition(tri.vertices.i1);
            var p2 = topology.GetPosition(tri.vertices.i2);
            var p3 = topology.GetPosition(tri.vertices.i3);

            if ((l1 < refDistance) && (l2 < refDistance) && (l3 < refDistance)) { /* Nothing to do, all on one side */ }
            else if ((l1 > refDistance) && (l2 > refDistance) && (l3 > refDistance)) { /* Nothing to do, all on other side */ }
            else if ((l1 == refDistance) || (l2 == refDistance) || (l3 == refDistance)) { /* One of the vertices is on the contour line */ }
            else
            {
                Vector3[] p = new Vector3[2] { Vector3.zero, Vector3.zero };
                int index = 0;

                if ((s1 * s2) < 0.0f)
                {
                    p[index] = ComputeContourPoint(p1, p2, l1, l2, refDistance);
                    index++;
                }

                if ((s2 * s3) < 0.0f)
                {
                    p[index] = ComputeContourPoint(p2, p3, l2, l3, refDistance);
                    index++;
                }

                if ((s3 * s1) < 0)
                {
                    p[index] = ComputeContourPoint(p3, p1, l3, l1, refDistance);
                    index++;
                }

                if (index == 2)
                {
                    int i1 = GetOrAddVertex(p[0]);
                    int i2 = GetOrAddVertex(p[1]);

                    indices.Add(i1); indices.Add(i2);
                }
            }

            triIndex++;
        }

        List<Polyline> ret = new();

        var usedSegments = new HashSet<(int, int)>();

        // Helper function to add a segment to the used set
        void MarkSegmentAsUsed(int a, int b)
        {
            usedSegments.Add((a, b));
            usedSegments.Add((b, a));
        }

        // Build a polyline starting from a specific index
        List<int> BuildPolyline(int start)
        {
            var polyline = new List<int> { start };
            var current = start;

            // Traverse forward
            while (true)
            {
                bool foundNext = false;
                for (int i = 0; i < indices.Count; i += 2)
                {
                    int a = indices[i];
                    int b = indices[i + 1];

                    // Check if this segment is part of the chain and hasn't been used
                    if ((a == current || b == current) && !usedSegments.Contains((a, b)))
                    {
                        int next = (a == current) ? b : a; // Determine the next point
                        polyline.Add(next);
                        MarkSegmentAsUsed(a, b); // Mark the segment as used
                        current = next; // Move forward
                        foundNext = true;
                        break;
                    }
                }

                if (!foundNext) break; // Stop if no more segments connect
            }

            return polyline;
        }

        // Build polylines until all segments are used
        for (int i = 0; i < indices.Count; i += 2)
        {
            int a = indices[i];
            int b = indices[i + 1];

            // Skip if the segment is already used
            if (usedSegments.Contains((a, b))) continue;

            // Start a new polyline
            List<int> polylineIndices = BuildPolyline(a);

            var polyline = new Polyline();
            foreach (var index in polylineIndices) polyline.Add(vertices[index]);

            ret.Add(polyline);
        }

        return ret;
    }
}

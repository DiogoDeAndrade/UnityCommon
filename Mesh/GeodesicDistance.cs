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
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UC
{

    public interface ICompareIndices
    {
        public bool isLess(int index1, int index2);
        public bool isLess(int index1, float value);
        public float GetValue(int index);
        public List<Polyline> ComputeContours(float refValue, bool onlySaddle, List<List<int>> trianglesPerContour);
    }

    [System.Serializable]
    public class LevelSetDiagram
    {
        [SerializeField] private ICompareIndices _comparisonOperator;
        [SerializeField] private TopologyStatic _topology;

        public class SingleContour
        {
            public Vector3 nodePos;
            public int sadleVertex;
            public Polyline polyline;
            public SingleContour parentContour;
            public List<SingleContour> children;

            public void AddChild(SingleContour c)
            {
                if (children == null) children = new();
                children.Add(c);
            }
        }
        private List<SingleContour> _contourList;
        private List<float> _contoursLRef;
        private List<List<Polyline>> _contours;
        [SerializeField, HideInInspector]
        private List<int> vertexContour;
        private SingleContour _rootContour;

        public ICompareIndices comparisonOperator
        {
            get { return _comparisonOperator; }
            set { _comparisonOperator = value; }
        }
        public TopologyStatic topology
        {
            get { return _topology; }
            set { _topology = value; }
        }
        public List<List<Polyline>> contours => _contours;
        public SingleContour rootContour => _rootContour;

        public void Build()
        {
            if (_comparisonOperator == null) return;

            _contourList = new();
            _contoursLRef = new();

            List<int> indices = Enumerable.Range(0, _topology.vertexCount).ToList();

            indices.Sort((index1, index2) =>
                _comparisonOperator.isLess(index1, index2) ? -1 :
                _comparisonOperator.isLess(index2, index1) ? 1 : 0);

            int vertexCount = indices.Count;
            List<bool> processedVertex = new(Enumerable.Repeat(false, vertexCount));
            List<int> Q = new() { indices[0] };
            List<float> d = new(Enumerable.Repeat(float.MaxValue, vertexCount));
            d[indices[0]] = 0;

            Vector3 rootPos = _topology.GetVertexPosition(indices[0]);
            _rootContour = new SingleContour
            {
                sadleVertex = indices[0],
                nodePos = rootPos,
                polyline = new Polyline(rootPos)
            };
            _contourList.Add(_rootContour);
            _contoursLRef.Add(0.0f);

            vertexContour = new(Enumerable.Repeat(-1, vertexCount));
            vertexContour[indices[0]] = 0;

            while (Q.Count > 0)
            {
                // Sort Q to make it a priority list
                Q.Sort((index1, index2) => _comparisonOperator.isLess(index1, index2) ? -1 : _comparisonOperator.isLess(index2, index1) ? 1 : 0);

                var vIndex = Q[0]; Q.RemoveAt(0);
                processedVertex[vIndex] = true;
                // Get neighbours to ensure they will be processed later
                var neighbours = _topology.GetVertexNeighbours(vIndex);
                foreach (var n in neighbours)
                {
                    if (processedVertex[n]) continue;
                    float newDist = d[vIndex] + Vector3.Distance(_topology.GetVertexPosition(vIndex), _topology.GetVertexPosition(n));
                    if (Q.IndexOf(n) == -1)
                    {
                        vertexContour[n] = vertexContour[vIndex];
                        d[n] = newDist;
                        Q.Add(n);
                    }
                    else
                    {
                        if (newDist < d[n])
                        {
                            vertexContour[n] = vertexContour[vIndex];
                            d[n] = newDist;
                        }
                    }
                }
                float vin = ComputeVertexIndexNumber(vIndex);
                if (vin == 1.0f)
                {
                    Debug.Log($"Local minimum (disappear contour) at {d[vIndex]}");
                    // Insert line (CT[c(v)], v) into Ts(P) with v as a leaf
                    // Check if previous is too close
                    if ((_contoursLRef.Count == 0) ||
                        ((d[vIndex] - _contoursLRef[_contoursLRef.Count - 1]) > 1e-3))
                    {
                        _contoursLRef.Add(d[vIndex]);
                    }

                    int vc = vertexContour[vIndex];
                    if ((vc < 0) || (vc >= _contourList.Count))
                    {
                        Debug.LogError("Something went wrong computing the LSD...");
                    }
                    var parent = _contourList[vc];
                    _contourList[vc] = new SingleContour
                    {
                        sadleVertex = vIndex,
                        nodePos = _topology.GetVertexPosition(vIndex),
                        polyline = new Polyline(_topology.GetVertexPosition(vIndex)),
                        parentContour = parent
                    };
                    parent.AddChild(_contourList[vc]);
                }
                else if (vin < 0.0f)
                {
                    Debug.Log($"Split at {d[vIndex]}");

                    // Insert line (CT[c(v)], v) into Ts(P)
                    if ((_contoursLRef.Count == 0) ||
                        ((d[vIndex] - _contoursLRef[_contoursLRef.Count - 1]) > 1e-3))
                    {
                        _contoursLRef.Add(d[vIndex]);
                    }

                    List<List<int>> triangleListsPerContour = new();
                    var contours = _comparisonOperator.ComputeContours(d[vIndex], true, triangleListsPerContour);
                    // There should be (1 - vin) contours which involve the saddle vertex
                    int vc = vertexContour[vIndex];

                    Vector3 averagePos = Vector3.zero;
                    foreach (var contour in contours)
                    {
                        averagePos += contour.GetCenter();
                    }
                    averagePos /= contours.Count;

                    // Create split node - we don't keep track of it, we just use it a parent
                    var splitNode = new SingleContour
                    {
                        sadleVertex = vIndex,
                        nodePos = averagePos,
                        polyline = null,
                        parentContour = _contourList[vc]
                    };
                    _contourList[vc].AddChild(splitNode);

                    _contourList[vc] = new SingleContour
                    {
                        sadleVertex = vIndex,
                        nodePos = contours[0].GetCenter(),
                        polyline = contours[0],
                        parentContour = splitNode
                    };
                    splitNode.AddChild(_contourList[vc]);

                    int nBase = _contourList.Count;
                    int nCountours = contours.Count;

                    _contourList[vc].polyline = contours[0];

                    int currentContourCount = _contourList.Count;
                    for (int j = 1; j < nCountours; ++j)
                    {
                        var newC = new SingleContour
                        {
                            sadleVertex = vIndex,
                            nodePos = contours[j].GetCenter(),
                            polyline = contours[j],
                            parentContour = splitNode
                        };
                        _contourList.Add(newC);
                        splitNode.AddChild(newC);
                    }

                    // Update vertexContour
                    for (int i = 0; i < triangleListsPerContour.Count; i++)
                    {
                        int newC = (i == 0) ? vc : (currentContourCount + i - 1);
                        if ((newC < 0) || (newC >= _contourList.Count))
                        {
                            Debug.LogError("Something went wrong computing the LSD...");
                        }

                        var tl = triangleListsPerContour[i];

                        foreach (var triIndex in tl)
                        {
                            var tri = topology.triangles[triIndex];
                            float d1 = _comparisonOperator.GetValue(tri.vertices.i1);
                            float d2 = _comparisonOperator.GetValue(tri.vertices.i2);
                            float d3 = _comparisonOperator.GetValue(tri.vertices.i3);
                            if (d1 > d[vIndex]) vertexContour[tri.vertices.i1] = newC;
                            if (d2 > d[vIndex]) vertexContour[tri.vertices.i2] = newC;
                            if (d3 > d[vIndex]) vertexContour[tri.vertices.i3] = newC;
                        }
                    }
                }
            }

            _contours = new();
            foreach (var c in _contoursLRef)
            {
                _contours.Add(_comparisonOperator.ComputeContours(c, false, null));
            }
        }

        public float ComputeVertexIndexNumber(int index)
        {
            var neighbours = topology.GetVertexNeighbours(index);
            var centerPos = topology.GetVertexPosition(index);
            var centerNormal = topology.GetVertexNormal(index);

            Vector3 toFirstNeighbor = (topology.GetVertexPosition(neighbours.First()) - centerPos).normalized;
            Vector3 right = Vector3.Cross(centerNormal, toFirstNeighbor).normalized;
            Vector3 forward = Vector3.Cross(right, centerNormal).normalized;

            var sortedNeighbours = neighbours.OrderBy(neighbour =>
            {
                Vector3 direction = (topology.GetVertexPosition(neighbour) - centerPos).normalized;

                // Project direction onto the reference plane
                float x = Vector3.Dot(direction, right);
                float y = Vector3.Dot(direction, forward);

                // Compute angle in radians
                return Mathf.Atan2(y, x);
            }).ToList();

            int signChanges = 0;
            for (int i = 0; i < sortedNeighbours.Count; i++)
            {
                int i1 = sortedNeighbours[i];
                int i2 = sortedNeighbours[(i + 1) % sortedNeighbours.Count];
                int s1 = _comparisonOperator.isLess(i1, index) ? (-1) : (1);
                int s2 = _comparisonOperator.isLess(i2, index) ? (-1) : (1);
                if (s1 != s2) signChanges++;
            }

            return 1.0f - signChanges / 2.0f;
        }

        public int GetVertexContour(int i)
        {
            return vertexContour[i];
        }
    }
}
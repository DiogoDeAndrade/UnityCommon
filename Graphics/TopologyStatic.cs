using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    [System.Serializable]
    public class TopologyStatic
    {
        [System.Serializable]
        public struct IdPair
        {
            public int i1;
            public int i2;

            public IdPair(int i1, int i2)
            {
                this.i1 = i1;
                this.i2 = i2;
            }

            public static implicit operator IdPair((int, int) tuple) => new IdPair(tuple.Item1, tuple.Item2);

            public override string ToString() => $"({i1}, {i2})";
        }

        [System.Serializable]
        public struct IdTriplet
        {
            public int i1;
            public int i2;
            public int i3;

            public IdTriplet(int i1, int i2, int i3)
            {
                this.i1 = i1;
                this.i2 = i2;
                this.i3 = i3;
            }

            public static implicit operator IdTriplet((int, int, int) tuple) => new IdTriplet(tuple.Item1, tuple.Item2, tuple.Item3);

            public override string ToString() => $"({i1}, {i2}, {i3})";
        }

        [System.Serializable]
        public class TVertex
        {
            public TVertex(Vector3 position) { this.position = position; neighbourVertex = new(); edges = new(); triangles = new(); }

            public Vector3 position;
            public Vector3 normal;
            public SerializedHashSet<int> neighbourVertex;
            public SerializedHashSet<int> edges;
            public SerializedHashSet<int> triangles;
        }

        [System.Serializable]
        public class TEdge
        {
            public TEdge(int i1, int i2) { this.vertices = (i1, i2); neighbourEdges = new(); triangles = new(); }

            public IdPair vertices;
            public SerializedHashSet<int> neighbourEdges;
            public SerializedHashSet<int> triangles;

            public Vector3 GetMidpoint(List<TVertex> allVertices) => (allVertices[vertices.i1].position + allVertices[vertices.i2].position) * 0.5f;
            public float GetLength(List<TVertex> allVertices) => Vector3.Distance(allVertices[vertices.i1].position, allVertices[vertices.i2].position);

            internal bool UsesVertex(int index)
            {
                return (vertices.i1 == index) || (vertices.i2 == index);
            }
        }

        [System.Serializable]
        public class TTriangle
        {
            public TTriangle(int i1, int i2, int i3, int e1, int e2, int e3) { vertices = (i1, i2, i3); edges = (e1, e2, e3); neighbourTriangles = new(); }
            public TTriangle(TTriangle src) { vertices = src.vertices; edges = src.edges; neighbourTriangles = new(src.neighbourTriangles); }

            public Vector3 normal;
            public IdTriplet vertices;
            public IdTriplet edges;
            public SerializedHashSet<int> neighbourTriangles;

            internal void ReplaceVertex(int oldVertexId, int newVertexId)
            {
                if (vertices.i1 == oldVertexId) vertices = (newVertexId, vertices.i2, vertices.i3);
                else if (vertices.i2 == oldVertexId) vertices = (vertices.i1, newVertexId, vertices.i3);
                else if (vertices.i3 == oldVertexId) vertices = (vertices.i1, vertices.i2, newVertexId);
            }

            internal void ReplaceEdge(int oldEdgeId, int newEdgeId)
            {
                if (edges.i1 == oldEdgeId) edges = (newEdgeId, edges.i2, edges.i3);
                else if (edges.i2 == oldEdgeId) edges = (edges.i1, newEdgeId, edges.i3);
                else if (edges.i3 == oldEdgeId) edges = (vertices.i1, edges.i2, newEdgeId);
            }

            internal int GetVertexIdExcluding(int i1, int i2)
            {
                if ((vertices.i1 != i1) && (vertices.i1 != i2)) return vertices.i1;
                if ((vertices.i2 != i1) && (vertices.i2 != i2)) return vertices.i2;
                if ((vertices.i3 != i1) && (vertices.i3 != i2)) return vertices.i3;

                // Some sort of degenerate triangle
                Debug.LogWarning("Degenerate triangle found!");
                return -1;
            }
        }

        [SerializeField]
        private List<TVertex> _vertices;
        [SerializeField]
        private List<TEdge> _edges;
        [SerializeField]
        private List<TTriangle> _triangles;

        private Dictionary<IdPair, int> edgeDic;

        public List<TVertex> vertices => _vertices;
        public List<TEdge> edges => _edges;
        public List<TTriangle> triangles => _triangles;

        public TopologyStatic(Mesh mesh, Matrix4x4 matrix, bool weld = true, float epsilon = 1e-3f)
        {
            var verts = mesh.vertices;

            List<int> vertexMatch = new List<int>();

            if (weld)
            {
                _vertices = new List<TVertex>();
                // Transform vertices and weld mesh
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 tvertex = matrix * new Vector4(verts[i].x, verts[i].y, verts[i].z, 1);
                    vertexMatch.Add(GetOrAddVertex(tvertex));
                }
            }
            else
            {
                _vertices = new List<TVertex>(verts.Length);
                for (int i = 0; i < verts.Length; i++) _vertices.Add(new TVertex(verts[i]));
                vertexMatch = new List<int>(verts.Length);
                for (int i = 0; i < verts.Length; i++) vertexMatch.Add(i);
            }

            _triangles = new List<TTriangle>();
            _edges = new List<TEdge>();
            edgeDic = new Dictionary<IdPair, int>();

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var indices = mesh.GetIndices(submesh);
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int index1 = vertexMatch[indices[i]];
                    int index2 = vertexMatch[indices[i + 1]];
                    int index3 = vertexMatch[indices[i + 2]];

                    int e1 = GetOrAddEdge(index1, index2);
                    int e2 = GetOrAddEdge(index2, index3);
                    int e3 = GetOrAddEdge(index3, index1);

                    TTriangle triangle = new TTriangle(index1, index2, index3, e1, e2, e3);
                    triangle.normal = Vector3.Cross(_vertices[index2].position - _vertices[index1].position,
                                                    _vertices[index3].position - _vertices[index1].position);
                    triangle.normal.SafeNormalize();

                    int triIndex = _triangles.Count;
                    _triangles.Add(triangle);

                    _edges[e1].triangles.Add(triIndex);
                    _edges[e2].triangles.Add(triIndex);
                    _edges[e3].triangles.Add(triIndex);

                    _vertices[index1].neighbourVertex.Add(index2);
                    _vertices[index1].neighbourVertex.Add(index3);
                    _vertices[index2].neighbourVertex.Add(index1);
                    _vertices[index2].neighbourVertex.Add(index3);
                    _vertices[index3].neighbourVertex.Add(index2);
                    _vertices[index3].neighbourVertex.Add(index1);
                    _vertices[index1].edges.Add(e1);
                    _vertices[index1].edges.Add(e3);
                    _vertices[index2].edges.Add(e1);
                    _vertices[index2].edges.Add(e2);
                    _vertices[index3].edges.Add(e2);
                    _vertices[index3].edges.Add(e3);
                    _vertices[index1].triangles.Add(triIndex);
                    _vertices[index2].triangles.Add(triIndex);
                    _vertices[index3].triangles.Add(triIndex);
                }
            }

            UpdateNeighbourEdges();
            UpdateNeighbourTriangles();
            UpdateVertexNormals();

        }

        void UpdateNeighbourEdges()
        {
            // Create neighbour edges
            for (int i = 0; i < _edges.Count; i++)
            {
                var edge = _edges[i];
                edge.neighbourEdges = new();

                var neighbours = _vertices[edge.vertices.i1].edges;
                foreach (var edgeId in neighbours) if (edgeId != i) edge.neighbourEdges.Add(edgeId);
                neighbours = _vertices[edge.vertices.i2].edges;
                foreach (var edgeId in neighbours) if (edgeId != i) edge.neighbourEdges.Add(edgeId);
            }
        }

        void UpdateNeighbourTriangles()
        {
            // Create neighbour triangles
            for (int i = 0; i < _triangles.Count; i++)
            {
                var triangle = _triangles[i];
                triangle.neighbourTriangles = new();

                var neighbours = _edges[triangle.edges.i1].triangles;
                foreach (var triangleId in neighbours) if (triangleId != i) triangle.neighbourTriangles.Add(triangleId);
                neighbours = _edges[triangle.edges.i2].triangles;
                foreach (var triangleId in neighbours) if (triangleId != i) triangle.neighbourTriangles.Add(triangleId);
                neighbours = _edges[triangle.edges.i3].triangles;
                foreach (var triangleId in neighbours) if (triangleId != i) triangle.neighbourTriangles.Add(triangleId);
            }
        }

        void UpdateVertexNormals()
        {
            for (int i = 0; i < _vertices.Count; i++)
            {
                _vertices[i].normal = Vector3.zero;
                foreach (var triangle in _vertices[i].triangles)
                {
                    _vertices[i].normal += _triangles[triangle].normal;
                }
                _vertices[i].normal.SafeNormalize();
            }
        }

        int GetOrAddVertex(Vector3 pos, float epsilon = 1e-6f)
        {
            for (int j = 0; j < _vertices.Count; j++)
            {
                if (Vector3.Distance(pos, _vertices[j].position) < epsilon)
                {
                    return j;
                }
            }
            _vertices.Add(new TVertex(pos));

            return _vertices.Count - 1;
        }

        int GetOrAddEdge(int index1, int index2)
        {
            if (edgeDic == null)
            {
                BuildEdgeDictionary();
            }
            if (edgeDic.TryGetValue((index1, index2), out int edgeId))
            {
                return edgeId;
            }

            TEdge edge = new TEdge(index1, index2);

            _edges.Add(edge);
            edgeDic.Add((index1, index2), _edges.Count - 1);
            edgeDic.Add((index2, index1), _edges.Count - 1);

            return _edges.Count - 1;
        }

        int FindEdge(int index1, int index2)
        {
            for (int i = 0; i < _edges.Count; i++)
            {
                if ((_edges[i].UsesVertex(index1)) &&
                    (_edges[i].UsesVertex(index2))) return i;
            }

            return -1;
        }

        void BuildEdgeDictionary()
        {
            edgeDic = new();
            for (int i = 0; i < _edges.Count; i++)
            {
                var edge = _edges[i];
                edgeDic.Add(edge.vertices, i);
                edgeDic.Add((edge.vertices.i2, edge.vertices.i1), i);
            }
        }

        public int vertexCount => (_vertices == null) ? 0 : _vertices.Count;
        public int edgeCount => (_edges == null) ? 0 : _edges.Count;
        public int triangleCount => (_triangles == null) ? 0 : _triangles.Count;

        public List<Vector3> GetVertexPositions()
        {
            List<Vector3> ret = new();
            foreach (var vertex in _vertices) ret.Add(vertex.position);

            return ret;
        }

        public Vector3 GetVertexPosition(int vertexId) => _vertices[vertexId].position;
        public Vector3 GetVertexNormal(int vertexId) => _vertices[vertexId].normal;
        public SerializedHashSet<int> GetVertexNeighbours(int vertexId) => _vertices[vertexId].neighbourVertex;
        public SerializedHashSet<int> GetVertexEdges(int vertexId) => _vertices[vertexId].edges;
        public SerializedHashSet<int> GetVertexTriangles(int vertexId) => _vertices[vertexId].triangles;

        public SerializedHashSet<int> GetTrianglesByEdge(int index1, int index2) => _edges[GetEdgeId(index1, index2)].triangles;
        public Vector3 GetTriangleNormal(int triangleIndex) => _triangles[triangleIndex].normal;

        public IdPair GetEdgeVertex(int edgeId) => _edges[edgeId].vertices;
        public int GetEdgeId(int vertexId1, int vertexId2)
        {
            if (edgeDic == null) BuildEdgeDictionary();
            if (edgeDic.TryGetValue((vertexId1, vertexId2), out int edgeId))
            {
                return edgeId;
            }
            return -1;
        }
        public SerializedHashSet<int> GetEdgeNeighbours(int edgeId) => _edges[edgeId].neighbourEdges;
        public SerializedHashSet<int> GetEdgeTriangles(int edgeId) => _edges[edgeId].triangles;

        public IdTriplet GetTriangleVertex(int triangleId) => _triangles[triangleId].vertices;
        public IdTriplet GetTriangleEdges(int triangleId) => _triangles[triangleId].edges;
        public SerializedHashSet<int> GetTriangleNeighbours(int triangleId) => _triangles[triangleId].neighbourTriangles;

        public int GetClosestPointId(Vector3 position)
        {
            float closestDist = float.MaxValue;
            int ret = -1;

            for (int i = 0; i < vertices.Count; i++)
            {
                var v = vertices[i];
                float d = Vector3.Distance(v.position, position);
                if (d < closestDist)
                {
                    closestDist = d;
                    ret = i;
                }
            }
            return ret;
        }

        public int GetClosestTriangle(Vector3 position, out float u, out float v, out float w)
        {
            float closestDistance = float.MaxValue;
            int closestTriangleId = -1;

            u = v = w = 0; // Default barycentric coordinates
            if (_triangles == null) return -1;

            for (int i = 0; i < _triangles.Count; i++)
            {
                // Get the triangle's vertices
                var tri = triangles[i];
                Vector3 p1 = vertices[tri.vertices.i1].position;
                Vector3 p2 = vertices[tri.vertices.i2].position;
                Vector3 p3 = vertices[tri.vertices.i3].position;

                // Compute the closest point on the triangle
                Vector3 closestPoint = Triangle.GetClosestPointInTriangle(position, p1, p2, p3, out float tu, out float tv, out float tw);

                // Compute the distance from the given position to the closest point
                float distance = Vector3.Distance(position, closestPoint);

                // Update the closest triangle if this one is closer
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTriangleId = i;
                    u = tu;
                    v = tv;
                    w = tw;
                }
            }

            return closestTriangleId;
        }

        public (Vector3 p1, Vector3 p2, Vector3 p3) GetTriangle(int index)
        {
            var tri = triangles[index];

            return (vertices[tri.vertices.i1].position, vertices[tri.vertices.i2].position, vertices[tri.vertices.i3].position);
        }

        public Vector3 GetTriangleCenter(int index)
        {
            var tri = triangles[index];

            return (vertices[tri.vertices.i1].position + vertices[tri.vertices.i2].position + vertices[tri.vertices.i3].position) / 3.0f;
        }
    }
}
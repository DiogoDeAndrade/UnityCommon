using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace UC
{

    public static class MeshTools
    {

        public static void ComputeNormals(Mesh mesh, bool area_weight)
        {
            var indices = mesh.GetTriangles(0);
            var vertices = mesh.vertices;

            List<Vector3> triangle_normals = new List<Vector3>();
            List<float> triangle_areas = new List<float>();
            List<List<int>> triangles_per_vertex = new List<List<int>>();
            for (int i = 0; i < vertices.Length; i++) triangles_per_vertex.Add(new List<int>());

            for (int i = 0; i < indices.Length; i += 3)
            {
                int i1, i2, i3;
                i1 = indices[i];
                i2 = indices[i + 1];
                i3 = indices[i + 2];

                Vector3 v1, v2, v3;
                v1 = vertices[i1];
                v2 = vertices[i2];
                v3 = vertices[i3];

                Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1);
                triangle_areas.Add(normal.magnitude * 0.5f);
                triangle_normals.Add(normal.normalized);

                triangles_per_vertex[i1].Add((int)i / 3);
                triangles_per_vertex[i2].Add((int)i / 3);
                triangles_per_vertex[i3].Add((int)i / 3);
            }

            List<Vector3> normals = new List<Vector3>();
            for (int i = 0; i < vertices.Length; i++)
            {
                normals.Add(Vector3.zero);
            }

            if (area_weight)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 normal = Vector3.zero;
                    float Length = 0.0f;

                    foreach (var index in triangles_per_vertex[i])
                    {
                        normal += triangle_normals[index] * triangle_areas[index];
                        Length += triangle_areas[index];
                    }

                    normals[i] = normal.normalized;
                }
            }
            else
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 normal = Vector3.zero;
                    float Length = 0.0f;

                    foreach (var index in triangles_per_vertex[i])
                    {
                        normal += triangle_normals[index];
                        Length += 1.0f;
                    }

                    normals[i] = normal.normalized;
                }
            }

            mesh.SetNormals(normals);
        }

        public static void ComputeNormalsAndTangentSpace(Mesh mesh, bool area_weight)
        {
            var indices = mesh.GetTriangles(0);
            var vertices = mesh.vertices;
            var uv = mesh.uv;

            List<Vector3> triangle_normals = new List<Vector3>();
            List<Vector3> triangle_tangents = new List<Vector3>();
            List<Vector3> triangle_binormals = new List<Vector3>();
            List<float> triangle_areas = new List<float>();
            List<List<int>> triangles_per_vertex = new List<List<int>>();
            for (int i = 0; i < vertices.Length; i++) triangles_per_vertex.Add(new List<int>());

            for (int i = 0; i < indices.Length; i += 3)
            {
                int i0, i1, i2;
                i0 = indices[i];
                i1 = indices[i + 1];
                i2 = indices[i + 2];

                Vector3 v0, v1, v2;
                Vector2 uv0, uv1, uv2;
                v0 = vertices[i0]; uv0 = uv[i0];
                v1 = vertices[i1]; uv1 = uv[i1];
                v2 = vertices[i2]; uv2 = uv[i2];

                var side0 = v0 - v1;
                var side1 = v2 - v1;

                // Normal and triangle area
                Vector3 normal = Vector3.Cross(side1, side0);
                triangle_areas.Add(normal.magnitude * 0.5f);
                normal = normal.normalized;
                triangle_normals.Add(normal);

                // Tangent space
                Vector2 delta0 = uv0 - uv1;
                Vector2 delta1 = uv2 - uv1;

                Vector3 tangent = (delta1.y * side0 - delta0.y * side1).normalized;
                Vector3 binormal = (delta1.x * side0 - delta0.x * side1).normalized;

                var tangent_cross = Vector3.Cross(tangent, binormal);
                if (Vector3.Dot(tangent_cross, normal) < 0.0f)
                {
                    tangent = -tangent;
                    binormal = -binormal;
                }

                triangle_binormals.Add(binormal);
                triangle_tangents.Add(tangent);

                triangles_per_vertex[i0].Add((int)i / 3);
                triangles_per_vertex[i1].Add((int)i / 3);
                triangles_per_vertex[i2].Add((int)i / 3);
            }

            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();
            for (int i = 0; i < vertices.Length; i++)
            {
                normals.Add(Vector3.zero);
                tangents.Add(Vector3.zero);
            }

            if (area_weight)
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 normal = Vector3.zero;
                    Vector3 tangent = Vector3.zero;
                    Vector3 binormal = Vector3.zero;
                    float Length = 0.0f;

                    foreach (var index in triangles_per_vertex[i])
                    {
                        normal += triangle_normals[index] * triangle_areas[index];
                        tangent += triangle_tangents[index] * triangle_areas[index];
                        binormal += triangle_binormals[index] * triangle_areas[index];
                        Length += triangle_areas[index];
                    }

                    normals[i] = normal.normalized;
                    tangents[i] = tangent.normalized;
                }
            }
            else
            {
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 normal = Vector3.zero;
                    Vector3 tangent = Vector3.zero;
                    Vector3 binormal = Vector3.zero;
                    float Length = 0.0f;

                    foreach (var index in triangles_per_vertex[i])
                    {
                        normal += triangle_normals[index];
                        tangent += triangle_tangents[index];
                        binormal += triangle_binormals[index];
                        Length += 1.0f;
                    }

                    normals[i] = normal.normalized;
                    tangents[i] = tangent.normalized;
                }
            }

            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
        }

        public static void ComputeNormalsAndTangentSpaceWelded(Mesh mesh, bool area_weight, float tolerance = 0.001f)
        {
            var indices = mesh.GetTriangles(0);
            var originalVertices = mesh.vertices;
            var uv = mesh.uv;

            var mapping = new Dictionary<int, int>();
            var weldedVertices = new List<Vector3>();

            for (int i = 0; i < originalVertices.Length; i++)
            {
                int idx = -1;
                for (int j = 0; j < weldedVertices.Count; j++)
                {
                    if (Vector3.Distance(originalVertices[i], weldedVertices[j]) < tolerance)
                    {
                        idx = mapping[i] = j;
                        break;
                    }
                }
                if (idx == -1)
                {
                    weldedVertices.Add(originalVertices[i]);
                    mapping[i] = weldedVertices.Count - 1;
                }
            }


            List<Vector3> triangle_normals = new List<Vector3>();
            List<Vector3> triangle_tangents = new List<Vector3>();
            List<Vector3> triangle_binormals = new List<Vector3>();
            List<float> triangle_areas = new List<float>();
            List<List<int>> triangles_per_vertex = new List<List<int>>();
            for (int i = 0; i < weldedVertices.Count; i++) triangles_per_vertex.Add(new List<int>());

            for (int i = 0; i < indices.Length; i += 3)
            {
                int i0, i1, i2;
                i0 = indices[i];
                i1 = indices[i + 1];
                i2 = indices[i + 2];

                Vector3 v0, v1, v2;
                Vector2 uv0, uv1, uv2;
                v0 = originalVertices[i0]; uv0 = uv[i0];
                v1 = originalVertices[i1]; uv1 = uv[i1];
                v2 = originalVertices[i2]; uv2 = uv[i2];

                var side0 = v0 - v1;
                var side1 = v2 - v1;

                // Normal and triangle area
                Vector3 normal = Vector3.Cross(side1, side0);
                triangle_areas.Add(normal.magnitude * 0.5f);
                normal = normal.normalized;
                triangle_normals.Add(normal);

                // Tangent space
                Vector2 delta0 = uv0 - uv1;
                Vector2 delta1 = uv2 - uv1;

                Vector3 tangent = (delta1.y * side0 - delta0.y * side1).normalized;
                Vector3 binormal = (delta1.x * side0 - delta0.x * side1).normalized;

                var tangent_cross = Vector3.Cross(tangent, binormal);
                if (Vector3.Dot(tangent_cross, normal) < 0.0f)
                {
                    tangent = -tangent;
                    binormal = -binormal;
                }

                triangle_binormals.Add(binormal);
                triangle_tangents.Add(tangent);

                triangles_per_vertex[mapping[i0]].Add((int)i / 3);
                triangles_per_vertex[mapping[i1]].Add((int)i / 3);
                triangles_per_vertex[mapping[i2]].Add((int)i / 3);
            }

            List<Vector3> weldedNormals = new List<Vector3>();
            List<Vector4> weldedTangents = new List<Vector4>();
            for (int i = 0; i < originalVertices.Length; i++)
            {
                weldedNormals.Add(Vector3.zero);
                weldedTangents.Add(Vector3.zero);
            }

            if (area_weight)
            {
                for (int i = 0; i < weldedVertices.Count; i++)
                {
                    Vector3 normal = Vector3.zero;
                    Vector3 tangent = Vector3.zero;
                    Vector3 binormal = Vector3.zero;
                    float Length = 0.0f;

                    foreach (var index in triangles_per_vertex[i])
                    {
                        normal += triangle_normals[index] * triangle_areas[index];
                        tangent += triangle_tangents[index] * triangle_areas[index];
                        binormal += triangle_binormals[index] * triangle_areas[index];
                        Length += triangle_areas[index];
                    }

                    weldedNormals[i] = normal.normalized;
                    weldedTangents[i] = tangent.normalized;
                }
            }
            else
            {
                for (int i = 0; i < weldedVertices.Count; i++)
                {
                    Vector3 normal = Vector3.zero;
                    Vector3 tangent = Vector3.zero;
                    Vector3 binormal = Vector3.zero;
                    float Length = 0.0f;

                    foreach (var index in triangles_per_vertex[i])
                    {
                        normal += triangle_normals[index];
                        tangent += triangle_tangents[index];
                        binormal += triangle_binormals[index];
                        Length += 1.0f;
                    }

                    weldedNormals[i] = normal.normalized;
                    weldedTangents[i] = tangent.normalized;
                }
            }

            List<Vector3> normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>();

            for (int i = 0; i < originalVertices.Length; i++)
            {
                normals.Add(weldedNormals[mapping[i]]);
                tangents.Add(weldedTangents[mapping[i]]);
            }

            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
        }

        public static void Transform(Mesh mesh, Vector3 position, Vector3 rotation, Vector3 scale)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            var matrix = Matrix4x4.TRS(position, Quaternion.Euler(rotation.x, rotation.y, rotation.z), scale);

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = matrix * vertices[i];
                if (normals.Length > 0) normals[i] = (matrix * new Vector4(normals[i].x, normals[i].y, normals[i].z, 0)).xyz();
                if (tangents.Length > 0) tangents[i] = (matrix * new Vector4(tangents[i].x, tangents[i].y, tangents[i].z, 0)).xyz();
            }

            mesh.SetVertices(vertices);
            if (normals.Length > 0) mesh.SetNormals(normals);
            if (tangents.Length > 0) mesh.SetTangents(tangents);
        }

        public static void ScaleVertex(Mesh mesh, Vector3 scale)
        {
            var vertices = mesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new Vector3(vertices[i].x * scale.x, vertices[i].y * scale.y, vertices[i].z * scale.z);
            }

            mesh.SetVertices(vertices);
        }

        public static void MergeMeshes(Mesh mesh, List<Mesh> meshes)
        {
            if (meshes.Count == 0) return;
            if (meshes.Count == 1)
            {
                mesh = meshes[0].Clone();
                return;
            }

            // Check if meshes are valid for merge (if they have the same components)
            var vertex0 = meshes[0].vertices;
            var normals0 = meshes[0].normals;
            var uvs0 = meshes[0].uv;
            var colors0 = meshes[0].colors;
            var tangents0 = meshes[0].tangents;

            int n_vertex = vertex0.Length;
            int n_normals = normals0.Length;
            int n_uvs = uvs0.Length;
            int n_colors = colors0.Length;
            int n_tangents = tangents0.Length;

            for (int i = 1; i < meshes.Count; i++)
            {
                var srcVertex = meshes[i].vertices;
                var srcNormals = meshes[i].normals;
                var srcUvs = meshes[i].uv;
                var srcColors = meshes[i].colors;
                var srcTangents = meshes[i].tangents;

                if (((n_vertex > 0) && (srcVertex.Length == 0)) ||
                    ((n_vertex == 0) && (srcVertex.Length > 0)))
                {
                    Debug.Log("Meshes can't be merged (one has positions, the other doesn't)");
                    return;
                }
                if (((n_normals > 0) && (srcNormals.Length == 0)) ||
                    ((n_normals == 0) && (srcNormals.Length > 0)))
                {
                    Debug.Log("Meshes can't be merged (one has normals, the other doesn't)");
                    return;
                }
                if (((n_uvs > 0) && (srcUvs.Length == 0)) ||
                    ((n_uvs == 0) && (srcUvs.Length > 0)))
                {
                    Debug.Log("Meshes can't be merged (one has UVs, the other doesn't)");
                    return;
                }
                if (((n_colors > 0) && (srcColors.Length == 0)) ||
                    ((n_colors == 0) && (srcColors.Length > 0)))
                {
                    Debug.Log("Meshes can't be merged (one has color0, the other doesn't)");
                    return;
                }
                if (((n_tangents > 0) && (srcTangents.Length == 0)) ||
                    ((n_tangents == 0) && (srcTangents.Length > 0)))
                {
                    Debug.Log("Meshes can't be merged (one has tangents, the other doesn't)");
                    return;
                }
            }

            var vertices = new List<Vector3>(vertex0);
            var normals = new List<Vector3>(normals0);
            var uvs = new List<Vector2>(uvs0);
            var color0 = new List<Color>(colors0);
            var tangents = new List<Vector4>(tangents0);
            var indices = new List<int>(meshes[0].triangles);

            for (int i = 1; i < meshes.Count; i++)
            {
                var srcVertex = meshes[i].vertices;
                var srcNormals = meshes[i].normals;
                var srcUvs = meshes[i].uv;
                var srcColors = meshes[i].colors;
                var srcTangents = meshes[i].tangents;

                int base_index = vertices.Count;
                if (srcVertex.Length > 0) { vertices.AddRange(srcVertex); }
                if (srcNormals.Length > 0) { normals.AddRange(srcNormals); }
                if (srcUvs.Length > 0) { uvs.AddRange(srcUvs); }
                if (srcColors.Length > 0) { color0.AddRange(srcColors); }
                if (srcTangents.Length > 0) { tangents.AddRange(srcTangents); }

                var src_index = meshes[i].triangles;
                foreach (var j in src_index)
                {
                    indices.Add(j + base_index);
                }
            }

            mesh.Clear();
            if (vertices.Count > 0) mesh.SetVertices(vertices);
            if (normals.Count > 0) mesh.SetNormals(normals0);
            if (uvs.Count > 0) mesh.SetUVs(0, uvs0);
            if (color0.Count > 0) mesh.SetColors(color0);
            if (tangents.Count > 0) mesh.SetTangents(tangents0);
            if (indices.Count > 0) mesh.SetTriangles(indices, 0);
        }

        public static void SetColor(Mesh mesh, Color color)
        {
            List<Color> colors = new List<Color>();

            for (int i = 0; i < mesh.vertices.Length; i++)
            {
                colors.Add(color);
            }

            mesh.SetColors(colors);
        }


        public static void InvertV(Mesh mesh)
        {
            var uv = mesh.uv;

            for (int i = 0; i < uv.Length; i++)
            {
                uv[i] = new Vector2(uv[i].x, 1.0f - uv[i].y);
            }

            mesh.SetUVs(0, uv);
        }

        public static void CopyNormalsToColor0(Mesh mesh, bool useAlpha = false, float alpha = 1.0f)
        {
            var normals = mesh.normals;
            var color = new Color[normals.Length];

            for (int i = 0; i < normals.Length; i++)
            {
                color[i] = new Color(normals[i].x * 0.5f + 0.5f, normals[i].y * 0.5f + 0.5f, normals[i].z * 0.5f + 0.5f, 1.0f);
            }
            if (useAlpha)
            {
                for (int i = 0; i < normals.Length; i++)
                {
                    color[i].a = alpha;
                }
            }

            mesh.SetColors(color);
        }

        public static Mesh SimplifyMesh(Mesh sourceMesh, float quality)
        {
            // Create our mesh simplifier and setup our entire mesh in it
            var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
            meshSimplifier.Initialize(sourceMesh);

            // This is where the magic happens, lets simplify!
            meshSimplifier.SimplifyMesh(quality);

            // Create our final mesh and apply it back to our mesh filter
            return meshSimplifier.ToMesh();
        }

        public static Mesh FromTopology(Topology top)
        {
            List<int> indices = new List<int>();
            foreach (var tri in top.triangles)
            {
                if (tri == null) continue;

                indices.Add(tri.v1);
                indices.Add(tri.v2);
                indices.Add(tri.v3);
            }

            Mesh mesh = new Mesh();

            mesh.indexFormat = (top.vertices.Count > 65535) ? (UnityEngine.Rendering.IndexFormat.UInt32) : (UnityEngine.Rendering.IndexFormat.UInt16);
            mesh.SetVertices(top.vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.name = "FromTopology";

            return mesh;
        }

        public static Mesh FromTopology(TopologyStatic top)
        {
            List<int> indices = new List<int>();
            foreach (var tri in top.triangles)
            {
                if (tri == null) continue;

                indices.Add(tri.vertices.i1);
                indices.Add(tri.vertices.i2);
                indices.Add(tri.vertices.i3);
            }

            Mesh mesh = new Mesh();

            mesh.indexFormat = (top.vertices.Count > 65535) ? (UnityEngine.Rendering.IndexFormat.UInt32) : (UnityEngine.Rendering.IndexFormat.UInt16);
            mesh.SetVertices(top.GetVertexPositions());
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.name = "FromTopology";

            return mesh;
        }

        public static Mesh SimplifyMeshInterior(Mesh sourceMesh, float colinearTolerance = 0.001f)
        {
            float colTol = 1.0f - colinearTolerance;

            var topology = new Topology(sourceMesh, Matrix4x4.identity);

            topology.OptimizeInterior();
            topology.OptimizeBoundary(colTol, true);

            var retMesh = FromTopology(topology);

            return retMesh;
        }

        public static Mesh ExtrudeMesh(Mesh sourceMesh, Vector3 offsetTop, Vector3 offsetBottom)
        {
            if ((sourceMesh.boneWeights.Length > 0) ||
                (sourceMesh.colors.Length > 0) ||
                (sourceMesh.uv.Length > 0) ||
                (sourceMesh.uv2.Length > 0) ||
                (sourceMesh.uv3.Length > 0) ||
                (sourceMesh.uv4.Length > 0))
            {
                Debug.LogWarning("ExtrudeMesh doesn't support meshes with other attributes beyond positions: need to implement!");
                return null;
            }

            if (sourceMesh.subMeshCount > 1)
            {
                Debug.LogWarning("ExtrudeMesh doesn't support meshes with more than one submesh: need to implement!");
                return null;
            }

            // Build topology from mesh
            TopologyStatic topology = new TopologyStatic(sourceMesh, Matrix4x4.identity, false);

            // From topology, create some vertices and indices corresponding to the top side
            var vertices = new List<Vector3>();
            var indices = new List<int>();

            // Top part
            foreach (var tvertex in topology.vertices)
            {
                vertices.Add(tvertex.position + offsetTop);
            }
            foreach (var tTriangle in topology.triangles)
            {
                indices.Add(tTriangle.vertices.i1);
                indices.Add(tTriangle.vertices.i2);
                indices.Add(tTriangle.vertices.i3);
            }
            // Bottom part
            int bottomOffset = vertices.Count;
            foreach (var tvertex in topology.vertices)
            {
                vertices.Add(tvertex.position + offsetBottom);
            }

            foreach (var tTriangle in topology.triangles)
            {
                indices.Add(tTriangle.vertices.i1 + bottomOffset);
                indices.Add(tTriangle.vertices.i3 + bottomOffset);
                indices.Add(tTriangle.vertices.i2 + bottomOffset);
            }
            // Get all edges that don't share triangles and extrude those
            foreach (var tEdge in topology.edges)
            {
                if (tEdge.triangles.Count() == 1)
                {
                    indices.Add(tEdge.vertices.i1);
                    indices.Add(tEdge.vertices.i1 + bottomOffset);
                    indices.Add(tEdge.vertices.i2);

                    indices.Add(tEdge.vertices.i2);
                    indices.Add(tEdge.vertices.i1 + bottomOffset);
                    indices.Add(tEdge.vertices.i2 + bottomOffset);
                }
            }

            Mesh destMesh = new Mesh();
            destMesh.name = sourceMesh.name + "Shell";
            destMesh.SetVertices(vertices);
            destMesh.SetIndices(indices, MeshTopology.Triangles, 0, true, 0);
            destMesh.RecalculateBounds();
            destMesh.RecalculateNormals();
            destMesh.RecalculateTangents();
            destMesh.UploadMeshData(false);

            return destMesh;
        }

        public enum MidpointStrategy { Divide3, Divide4 };

        public static Mesh SubdivideMidpoint(Mesh sourceMesh, MidpointStrategy strategy)
        {
            List<int> indices = new();
            List<Vector3> vertices = new(sourceMesh.vertices);
            var srcIndices = sourceMesh.triangles;
            int nIndices = srcIndices.Length;

            switch (strategy)
            {
                case MidpointStrategy.Divide3:
                    {
                        // Careful: indices is going to be changed, so sourceMesh.triangles's size is used below!
                        for (int i = 0; i < nIndices; i += 3)
                        {
                            // Subdivide this triangle
                            // Add vertex
                            int newVertexId = vertices.Count;
                            var midpoint = (vertices[srcIndices[i]] + vertices[srcIndices[i + 1]] + vertices[srcIndices[i + 2]]) / 3.0f;
                            vertices.Add(midpoint);

                            indices.Add(srcIndices[i]);
                            indices.Add(srcIndices[i + 1]);
                            indices.Add(newVertexId);

                            indices.Add(srcIndices[i + 1]);
                            indices.Add(srcIndices[i + 2]);
                            indices.Add(newVertexId);

                            indices.Add(srcIndices[i + 2]);
                            indices.Add(srcIndices[i]);
                            indices.Add(newVertexId);
                        }
                    }
                    break;
                case MidpointStrategy.Divide4:
                    // Careful: indices is going to be changed, so sourceMesh.triangles's size is used below!
                    for (int i = 0; i < nIndices; i += 3)
                    {
                        // Subdivide this triangle
                        int i1 = srcIndices[i];
                        int i2 = srcIndices[i + 1];
                        int i3 = srcIndices[i + 2];
                        // Add vertices
                        var midpoint1 = (vertices[srcIndices[i]] + vertices[srcIndices[i + 1]]) * 0.5f;
                        var midpoint2 = (vertices[srcIndices[i + 1]] + vertices[srcIndices[i + 2]]) * 0.5f;
                        var midpoint3 = (vertices[srcIndices[i + 2]] + vertices[srcIndices[i]]) * 0.5f;
                        vertices.Add(midpoint1);
                        int midpointId1 = vertices.Count - 1;
                        vertices.Add(midpoint2);
                        int midpointId2 = vertices.Count - 1;
                        vertices.Add(midpoint3);
                        int midpointId3 = vertices.Count - 1;

                        indices.Add(midpointId1);
                        indices.Add(midpointId2);
                        indices.Add(midpointId3);

                        indices.Add(i1);
                        indices.Add(midpointId1);
                        indices.Add(midpointId3);

                        indices.Add(midpointId1);
                        indices.Add(i2);
                        indices.Add(midpointId2);

                        indices.Add(midpointId2);
                        indices.Add(i3);
                        indices.Add(midpointId3);
                    }
                    break;
                default:
                    break;
            }


            Mesh mesh = new Mesh();

            mesh.indexFormat = (vertices.Count > 65535) ? (UnityEngine.Rendering.IndexFormat.UInt32) : (UnityEngine.Rendering.IndexFormat.UInt16);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            if (sourceMesh.name.IndexOf("Subdivide") == -1)
                mesh.name = sourceMesh.name + " Subdivided";
            else
                mesh.name = sourceMesh.name;

            if (strategy == MidpointStrategy.Divide4)
            {
                // To cleanup the mesh, get topology from the mesh with weld
                var topology = new TopologyStatic(mesh, Matrix4x4.identity, true);

                return FromTopology(topology);
            }

            return mesh;
        }

        public static Mesh SubdivideLongEdges(Mesh sourceMesh, float maxEdgeLength, int maxSplits)
        {
            bool restart = true;
            int numSplits = 0;

            List<int> indices = new(sourceMesh.triangles);
            List<Vector3> vertices = new(sourceMesh.vertices);

            while (restart)
            {
                if (numSplits >= maxSplits) break;

                restart = false;

                for (int i = 0; i < indices.Count; i += 3)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int i1 = indices[i + k];
                        int i2 = indices[i + (k + 1) % 3];
                        int i3 = indices[i + (k + 2) % 3];
                        Vector3 p1 = vertices[i1];
                        Vector3 p2 = vertices[i2];

                        float d = Vector3.Distance(p1, p2);
                        if (d > maxEdgeLength)
                        {
                            numSplits++;
                            restart = true;

                            int newVertexId = vertices.Count;
                            vertices.Add((p1 + p2) * 0.5f);

                            indices[i + (k + 1) % 3] = newVertexId;

                            indices.Add(newVertexId);
                            indices.Add(i2);
                            indices.Add(i3);

                            // Need to split all triangles that share this edge
                            for (int j = i + 3; j < indices.Count; j += 3)
                            {
                                for (int k2 = 0; k2 < 3; k2++)
                                {
                                    int o1 = indices[j + k2];
                                    int o2 = indices[j + (k2 + 1) % 3];
                                    int o3 = indices[j + (k2 + 2) % 3];

                                    if (((o1 == i1) && (o2 == i2)) ||
                                        ((o1 == i2) && (o2 == i1)))
                                    {
                                        // Found shared edge
                                        indices[j + (k2 + 1) % 3] = newVertexId;

                                        indices.Add(newVertexId);
                                        indices.Add(o2);
                                        indices.Add(o3);
                                    }
                                }
                            }
                            if (numSplits >= maxSplits) break;
                        }
                    }

                    if (numSplits >= maxSplits) break;
                }
            }

            Mesh mesh = new Mesh();

            mesh.indexFormat = (vertices.Count > 65535) ? (UnityEngine.Rendering.IndexFormat.UInt32) : (UnityEngine.Rendering.IndexFormat.UInt16);
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            if (sourceMesh.name.IndexOf("Subdivide") == -1)
                mesh.name = sourceMesh.name + " Subdivided";
            else
                mesh.name = sourceMesh.name;

            Debug.Log($"Ran {numSplits} splits...");

            // To cleanup the mesh, get topology from the mesh
            var topology = new TopologyStatic(mesh, Matrix4x4.identity, true);

            return FromTopology(topology);
        }
    }
}
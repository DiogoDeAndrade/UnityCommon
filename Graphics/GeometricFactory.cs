using UnityEngine;
using System.Collections.Generic;
using System;

namespace UC
{

    public static class GeometricFactory
    {

        public static Mesh BuildTerrain(string meshName, float side_length, int tris_per_side, float max_height,
                                        float noise_scale,
                                        Vector2 uv_scale, Vector2 offset,
                                        bool compute_normals, bool compute_tangent)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            int vertex_per_side = tris_per_side + 1;
            int nVertex = vertex_per_side * vertex_per_side;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            Vector3 o = new Vector3(-side_length * 0.5f, 0.0f, -side_length * 0.5f);

            float tri_size = side_length / (float)tris_per_side;
            float uv_delta = 1.0f / (tris_per_side);

            for (int y = 0; y < vertex_per_side; y++)
            {
                for (int x = 0; x < vertex_per_side; x++)
                {
                    Vector2 uv = new Vector2((uv_delta * x + offset.x) * uv_scale.x, (uv_delta * y + offset.y) * uv_scale.y);
                    uvs.Add(uv);

                    uv = uv * noise_scale;
                    float h = Mathf.PerlinNoise(uv.x, uv.y) * max_height;

                    Vector3 v = o + new Vector3(x * tri_size, h, y * tri_size);
                    vertices.Add(v);
                }
            }

            List<int> indices = new List<int>();

            for (int y = 0; y < tris_per_side; y++)
            {
                for (int x = 0; x < tris_per_side; x++)
                {
                    indices.Add(y * vertex_per_side + x);
                    indices.Add((y + 1) * vertex_per_side + x + 1);
                    indices.Add(y * vertex_per_side + x + 1);

                    indices.Add(y * vertex_per_side + x);
                    indices.Add((y + 1) * vertex_per_side + x);
                    indices.Add((y + 1) * vertex_per_side + x + 1);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);

            if (compute_normals)
            {
                if (compute_tangent)
                {
                    MeshTools.ComputeNormalsAndTangentSpace(mesh, false);
                }
                else
                {
                    MeshTools.ComputeNormals(mesh, false);
                }
            }
            else
            {
                List<Vector3> normals = new List<Vector3>();

                for (int i = 0; i < nVertex; i++)
                {
                    normals.Add(new Vector3(0.0f, 1.0f, 0.0f));
                }
                mesh.SetNormals(normals);
            }

            if (!compute_tangent)
            {
                List<Vector4> tangents = new List<Vector4>();
                List<Vector4> binormals = new List<Vector4>();

                for (int i = 0; i < nVertex; i++)
                {
                    tangents.Add(new Vector3(0.0f, 1.0f, 0.0f));
                    binormals.Add(new Vector3(1.0f, 0.0f, 0.0f));
                }
                mesh.SetTangents(tangents);
            }

            return mesh;
        }

        public static Mesh BuildPlane(string meshName, float side_length, int tris_per_side,
                                        Vector2 uv_scale, bool compute_tangent)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            int vertex_per_side = tris_per_side + 1;
            int nVertex = vertex_per_side * vertex_per_side;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();

            Vector3 o = new Vector3(-side_length * 0.5f, 0.0f, -side_length * 0.5f);

            float tri_size = side_length / (float)tris_per_side;
            float uv_delta = 1.0f / (tris_per_side);

            for (int y = 0; y < vertex_per_side; y++)
            {
                for (int x = 0; x < vertex_per_side; x++)
                {
                    Vector2 uv = new Vector2(uv_scale.x * uv_delta * x, uv_scale.y * (1 - (uv_delta * y)));

                    Vector3 v = o + new Vector3(x * tri_size, 0, y * tri_size);
                    vertices.Add(v);
                    normals.Add(new Vector3(0, 1, 0));
                    uvs.Add(uv);
                }
            }

            List<int> indices = new List<int>();

            for (int y = 0; y < tris_per_side; y++)
            {
                for (int x = 0; x < tris_per_side; x++)
                {
                    indices.Add(y * vertex_per_side + x);
                    indices.Add((y + 1) * vertex_per_side + x + 1);
                    indices.Add(y * vertex_per_side + x + 1);

                    indices.Add(y * vertex_per_side + x);
                    indices.Add((y + 1) * vertex_per_side + x);
                    indices.Add((y + 1) * vertex_per_side + x + 1);
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);

            if (compute_tangent)
            {
                MeshTools.ComputeNormalsAndTangentSpace(mesh, false);
            }
            else
            {
                mesh.SetNormals(normals);
            }

            return mesh;
        }

        public static Mesh BuildCube(string meshName, Vector3 size, bool compute_tangent)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> indices = new List<int>();

            // Front
            AddQuad(new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0.0f, 0.0f, 1.0f), new Vector2(1.0f, 1.0f),
                    new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0.0f, 0.0f, 1.0f), new Vector2(0.0f, 1.0f),
                    new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0.0f, 0.0f, 1.0f), new Vector2(0.0f, 0.0f),
                    new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(0.0f, 0.0f, 1.0f), new Vector2(1.0f, 0.0f),
                    ref vertices, ref normals, ref uvs, ref indices);
            // Back
            AddQuad(new Vector3(1.0f, -1.0f, -1.0f), new Vector3(0.0f, 0.0f, -1.0f), new Vector2(1.0f, 1.0f),
                    new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0.0f, 0.0f, -1.0f), new Vector2(0.0f, 1.0f),
                    new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0.0f, 0.0f, -1.0f), new Vector2(0.0f, 0.0f),
                    new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0.0f, 0.0f, -1.0f), new Vector2(1.0f, 0.0f),
                    ref vertices, ref normals, ref uvs, ref indices);
            // Top
            AddQuad(new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(0.0f, 1.0f, 0.0f), new Vector2(0.0f, 0.0f),
                    new Vector3(1.0f, 1.0f, 1.0f), new Vector3(0.0f, 1.0f, 0.0f), new Vector2(1.0f, 0.0f),
                    new Vector3(1.0f, 1.0f, -1.0f), new Vector3(0.0f, 1.0f, 0.0f), new Vector2(1.0f, 1.0f),
                    new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(0.0f, 1.0f, 0.0f), new Vector2(0.0f, 1.0f),
                    ref vertices, ref normals, ref uvs, ref indices);

            // Bottom
            AddQuad(new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f), new Vector2(0.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f), new Vector2(1.0f, 0.0f),
                    new Vector3(1.0f, -1.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f), new Vector2(1.0f, 1.0f),
                    new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f), new Vector2(0.0f, 1.0f),
                    ref vertices, ref normals, ref uvs, ref indices);

            // Right
            AddQuad(new Vector3(1.0f, -1.0f, 1.0f), new Vector3(1.0f, 0.0f, 0.0f), new Vector2(1.0f, 1.0f),
                    new Vector3(1.0f, -1.0f, -1.0f), new Vector3(1.0f, 0.0f, 0.0f), new Vector2(0.0f, 1.0f),
                    new Vector3(1.0f, 1.0f, -1.0f), new Vector3(1.0f, 0.0f, 0.0f), new Vector2(0.0f, 0.0f),
                    new Vector3(1.0f, 1.0f, 1.0f), new Vector3(1.0f, 0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                    ref vertices, ref normals, ref uvs, ref indices);

            // Left
            AddQuad(new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(-1.0f, 0.0f, 0.0f), new Vector2(1.0f, 1.0f),
                    new Vector3(-1.0f, -1.0f, 1.0f), new Vector3(-1.0f, 0.0f, 0.0f), new Vector2(0.0f, 1.0f),
                    new Vector3(-1.0f, 1.0f, 1.0f), new Vector3(-1.0f, 0.0f, 0.0f), new Vector2(0.0f, 0.0f),
                    new Vector3(-1.0f, 1.0f, -1.0f), new Vector3(-1.0f, 0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                    ref vertices, ref normals, ref uvs, ref indices);

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);

            MeshTools.ScaleVertex(mesh, size * 0.5f);

            if (compute_tangent)
            {
                MeshTools.ComputeNormalsAndTangentSpace(mesh, false);
            }

            return mesh;
        }

        public static Mesh BuildCone(string meshName, Vector3 size, float base_y, int subdivs, bool compute_tangent)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> indices = new List<int>();

            vertices.Add(new Vector3(0.0f, base_y, 0.0f));
            normals.Add(new Vector3(0.0f, -1.0f, 0.0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            float angle = 0.0f;
            float angle_inc = Mathf.PI * 2.0f / (float)subdivs;
            int index = 0;

            for (int i = 0; i < subdivs; i++)
            {
                Vector2 uv = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                vertices.Add(new Vector3(size.x * uv.x, base_y, size.z * uv.y));
                normals.Add(new Vector3(0.0f, -1.0f, 0.0f));
                uvs.Add(new Vector2(uv.x * 0.5f + 0.5f, uv.y * 0.5f + 0.5f));

                angle += angle_inc;
            }

            for (int i = 0; i < subdivs; i++)
            {
                indices.Add(index); indices.Add(i + 1 + index); indices.Add(((i + 1) % subdivs) + 1 + index);
            }

            float u = 0.0f;
            float u_inc = 1.0f / (float)subdivs;

            index = vertices.Count;

            for (int i = 0; i < subdivs; i++)
            {
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                vertices.Add(new Vector3(size.x * dir.x, base_y, size.z * dir.y));
                normals.Add(new Vector3(dir.x, 0.0f, dir.y));
                uvs.Add(new Vector2(u, 1.0f));

                vertices.Add(new Vector3(0.0f, base_y + size.y, 0.0f));
                normals.Add(new Vector3(dir.x, 0.0f, dir.y));
                uvs.Add(new Vector2(u, 0.0f));

                angle += angle_inc;
                u += u_inc;
            }

            for (int i = 0; i < subdivs; i++)
            {
                int next = (i + 1) % subdivs;
                indices.Add((i * 2) + index); indices.Add((i * 2) + 1 + index); indices.Add((next * 2) + index);
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);

            if (compute_tangent)
            {
                MeshTools.ComputeNormalsAndTangentSpace(mesh, false);
            }

            return mesh;
        }

        public static Mesh BuildCylinder(string meshName, Vector2 bottom_size, Vector2 top_size, float bottom_height, float top_height, bool bottom_cap, bool top_cap, int subdivs, bool compute_tangent)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> indices = new List<int>();

            float angle = 0.0f;
            float angle_inc = Mathf.PI * 2.0f / (float)subdivs;
            int index = 0;

            if (bottom_cap)
            {
                vertices.Add(new Vector3(0.0f, bottom_height, 0.0f));
                normals.Add(new Vector3(0.0f, -1.0f, 0.0f));
                uvs.Add(new Vector2(0.5f, 0.5f));

                for (int i = 0; i < subdivs; i++)
                {
                    Vector2 uv = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                    vertices.Add(new Vector3(bottom_size.x * uv.x, bottom_height, bottom_size.y * uv.y));
                    normals.Add(new Vector3(0.0f, -1.0f, 0.0f));
                    uvs.Add(new Vector2(uv.x * 0.5f + 0.5f, uv.y * 0.5f + 0.5f));

                    angle += angle_inc;
                }

                for (int i = 0; i < subdivs; i++)
                {
                    indices.Add(index); indices.Add(i + 1 + index); indices.Add(((i + 1) % subdivs) + 1 + index);
                }

                index = vertices.Count;
            }
            float u = 0.0f;
            float u_inc = 1.0f / (float)subdivs;

            for (int i = 0; i < subdivs; i++)
            {
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                vertices.Add(new Vector3(bottom_size.x * dir.x, bottom_height, bottom_size.y * dir.y));
                normals.Add(new Vector3(dir.x, 0.0f, dir.y));
                uvs.Add(new Vector2(u, 1.0f));

                vertices.Add(new Vector3(top_size.x * dir.x, top_height, top_size.y * dir.y));
                normals.Add(new Vector3(dir.x, 0.0f, dir.y));
                uvs.Add(new Vector2(u, 0.0f));

                angle += angle_inc;
                u += u_inc;
            }

            for (int i = 0; i < subdivs; i++)
            {
                int next = (i + 1) % subdivs;
                indices.Add((i * 2) + index); indices.Add((next * 2) + index + 1); indices.Add((next * 2) + index);
                indices.Add((i * 2) + index); indices.Add((i * 2) + index + 1); indices.Add((next * 2) + index + 1);
            }

            index = vertices.Count;

            if (top_cap)
            {
                vertices.Add(new Vector3(0.0f, top_height, 0.0f));
                normals.Add(new Vector3(0.0f, 1.0f, 0.0f));
                uvs.Add(new Vector2(0.5f, 0.5f));

                for (int i = 0; i < subdivs; i++)
                {
                    Vector2 uv = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                    vertices.Add(new Vector3(top_size.x * uv.x, top_height, top_size.y * uv.y));
                    normals.Add(new Vector3(0.0f, 1.0f, 0.0f));
                    uvs.Add(new Vector2(uv.x * 0.5f + 0.5f, uv.y * 0.5f + 0.5f));

                    angle += angle_inc;
                }

                for (int i = 0; i < subdivs; i++)
                {
                    indices.Add(index); indices.Add(((i + 1) % subdivs) + 1 + index); indices.Add(i + 1 + index);
                }

                index = vertices.Count;
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);

            if (compute_tangent)
            {
                MeshTools.ComputeNormalsAndTangentSpace(mesh, false);
            }

            return mesh;
        }

        public static void AddQuad(Vector3 p1, Vector3 n1, Vector2 uv1,
                                    Vector3 p2, Vector3 n2, Vector2 uv2,
                                    Vector3 p3, Vector3 n3, Vector2 uv3,
                                    Vector3 p4, Vector3 n4, Vector2 uv4,
                                    ref List<Vector3> vertices, ref List<Vector3> normals, ref List<Vector2> uvs, ref List<int> indices)
        {
            int base_index = vertices.Count;
            vertices.Add(p1); normals.Add(n1); uvs.Add(uv1);
            vertices.Add(p2); normals.Add(n2); uvs.Add(uv2);
            vertices.Add(p3); normals.Add(n3); uvs.Add(uv3);
            vertices.Add(p4); normals.Add(n4); uvs.Add(uv4);

            indices.Add(base_index + 0); indices.Add(base_index + 1); indices.Add(base_index + 2);
            indices.Add(base_index + 0); indices.Add(base_index + 2); indices.Add(base_index + 3);
        }

        static Mesh BuildSphere(string meshName, Vector3 radius, int lon_divs, int lat_divs,
                                Vector2 uv_scale,
                                bool compute_normals, bool compute_tangent)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> indices = new List<int>();

            vertices.Add(new Vector3(0.0f, -radius.y, 0.0f));
            normals.Add(new Vector3(0, -1, 0));
            uvs.Add(new Vector2(0.5f, 1.0f));

            float lat_inc = Mathf.PI / (float)(lat_divs + 1);
            float lon_inc = 2.0f * Mathf.PI / (float)lon_divs;

            float current_lat = lat_inc - Mathf.PI * 0.5f;
            float inc_u = 1.0f / (float)lon_divs;
            float inc_v = 1.0f / (float)(lat_divs + 1);
            Vector2 uv = new Vector2(0, 1.0f - inc_v);

            for (int y = 0; y < lat_divs; y++)
            {
                float current_lon = 0.0f;

                uv.x = 0;
                for (int x = 0; x < lon_divs; x++)
                {
                    Vector3 pos = new Vector3(Mathf.Cos(current_lat) * Mathf.Cos(current_lon) * radius.x, Mathf.Sin(current_lat) * radius.y, Mathf.Cos(current_lat) * Mathf.Sin(current_lon) * radius.z);
                    vertices.Add(pos);
                    normals.Add(pos.normalized);
                    uvs.Add(new Vector2(uv.x * uv_scale.x, uv.y * uv_scale.y));

                    current_lon += lon_inc;

                    uv.x += inc_u;
                }

                current_lat += lat_inc;
                uv.y -= inc_v;
            }

            vertices.Add(new Vector3(0.0f, radius.y, 0.0f));
            normals.Add(new Vector3(0, 1, 0));
            uvs.Add(new Vector2(0.5f, 0.0f));

            for (int i = 0; i < lon_divs; i++)
            {
                indices.Add(0);
                indices.Add(i + 1);
                indices.Add(((i + 1) % lon_divs) + 1);
            }

            for (int y = 0; y < lat_divs - 1; y++)
            {
                for (int x = 0; x < lon_divs; x++)
                {
                    indices.Add((y * lon_divs) + x + 1);
                    indices.Add(((y + 1) * lon_divs) + ((x + 1) % lon_divs) + 1);
                    indices.Add((y * lon_divs) + ((x + 1) % lon_divs) + 1);

                    indices.Add((y * lon_divs) + x + 1);
                    indices.Add(((y + 1) * lon_divs) + x + 1);
                    indices.Add(((y + 1) * lon_divs) + ((x + 1) % lon_divs) + 1);
                }
            }

            int last_index = vertices.Count - 1;
            int last_row_index = last_index - lon_divs;
            for (int i = 0; i < lon_divs; i++)
            {
                indices.Add(last_index);
                indices.Add(((i + 1) % lon_divs) + last_row_index);
                indices.Add(i + last_row_index);
            }

            var nVertex = vertices.Count;

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);

            if (compute_normals)
            {
                if (compute_tangent)
                {
                    MeshTools.ComputeNormalsAndTangentSpace(mesh, false);
                }
                else
                {
                    MeshTools.ComputeNormals(mesh, false);
                }
            }
            else
            {
                mesh.SetNormals(normals);
            }

            if (!compute_tangent)
            {
                List<Vector4> tangents = new List<Vector4>();

                for (int i = 0; i < nVertex; i++)
                {
                    tangents.Add(new Vector3(0.0f, 1.0f, 0.0f));
                }
                mesh.SetTangents(tangents);
            }

            return mesh;
        }

        public static Mesh BuildNoiseSphere(string meshName, Vector3 radius, int lon_divs, int lat_divs,
                                            Vector2 uv_scale,
                                            bool compute_normals, bool compute_tangent,
                                            Vector2 noise_scale, Vector2 noise_strength)
        {
            Mesh mesh = BuildSphere(meshName, radius, lon_divs, lat_divs, uv_scale, false, false);

            var pos = mesh.vertices;
            var normals = mesh.normals;
            var uv = mesh.uv;

            var nVertex = pos.Length;

            for (int i = 0; i < nVertex; i++)
            {
                float perlinNoise = Mathf.PerlinNoise(uv[i].x * noise_scale.x, uv[i].y * noise_scale.y) * (noise_strength.y - noise_strength.x) + noise_strength.x;

                pos[i] = pos[i] + normals[i] * perlinNoise;
            }

            mesh.SetVertices(pos);

            if (compute_normals)
            {
                if (compute_tangent)
                {
                    MeshTools.ComputeNormalsAndTangentSpace(mesh, false);
                }
                else
                {
                    MeshTools.ComputeNormals(mesh, false);
                }
            }

            if (!compute_tangent)
            {
                List<Vector4> tangents = new List<Vector4>();

                for (int i = 0; i < nVertex; i++)
                {
                    tangents.Add(new Vector3(0.0f, 1.0f, 0.0f));
                }
                mesh.SetTangents(tangents);
            }

            return mesh;
        }

        public static Mesh BuildArrow(string meshName, Vector2 min_size, Vector2 max_size, float bottom_size, float top_size, int subdivs)
        {
            var arrowTop = BuildCone("ArrowTop", new Vector3(max_size.x, top_size, max_size.y), bottom_size, subdivs, true);
            var arrowBottom = BuildCylinder("ArrowBottom", min_size, min_size, 0.0f, bottom_size, true, false, subdivs, true);

            Mesh mesh = new Mesh();
            mesh.name = meshName;
            MeshTools.MergeMeshes(mesh, new List<Mesh> { arrowTop, arrowBottom });
            MeshTools.Transform(mesh, new Vector3(0.0f, 0.0f, 0.0f), new Vector3(-90.0f, 0.0f, 0.0f), new Vector3(1.0f, 1.0f, 1.0f));
            MeshTools.ComputeNormalsAndTangentSpace(mesh, false);

            return mesh;
        }

        public static Mesh BuildAxis(string meshName, Vector2 min_size, Vector2 max_size, float bottom_size, float top_size, int subdivs)
        {
            Mesh mesh = new Mesh();
            mesh.name = meshName;

            var arrowX = BuildArrow("ArrowX", min_size, max_size, bottom_size, top_size, subdivs);
            MeshTools.SetColor(arrowX, new Color(1, 0, 0, 1));
            MeshTools.Transform(arrowX, new Vector3(0, 0, 0), new Vector3(0, -90, 0), new Vector3(1, 1, 1));
            var arrowY = BuildArrow("ArrowY", min_size, max_size, bottom_size, top_size, subdivs);
            MeshTools.SetColor(arrowY, new Color(0, 1, 0, 1));
            MeshTools.Transform(arrowY, new Vector3(0, 0, 0), new Vector3(90, 0, 0), new Vector3(1, 1, 1));
            var arrowZ = BuildArrow("ArrowZ", min_size, max_size, bottom_size, top_size, subdivs);
            MeshTools.SetColor(arrowZ, new Color(0, 0, 1, 1));

            MeshTools.MergeMeshes(mesh, new List<Mesh> { arrowX, arrowY, arrowZ });
            MeshTools.ComputeNormalsAndTangentSpace(mesh, false);

            return mesh;
        }

        public static void InvertOrder(List<int> indices)
        {
            for (int i = 0; i < indices.Count; i+=3)
            {
                (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
            }
        }
    }
}
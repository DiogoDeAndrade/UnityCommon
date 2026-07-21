using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Linq;
using System;

namespace UC
{

    public static partial class MeshTools
    {
        #region Midpoint subdivision
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

                return topology.ToMesh();
            }

            return mesh;
        }
        #endregion

        #region Long edge subdivision - Deprecated
        // This one is an old function, not sure if I'm using it anymore.
        [Obsolete("SubdivideLongEdges is an old function, not sure if it's used anymore. Consider using SubdivideLongEdgesTopology instead.")]
        public static Mesh SubdivideLongEdges(Mesh sourceMesh, float maxEdgeLength, int maxSplits, bool weld = false)
        {
            // Weld first so geometric neighbors actually share indices
            if (weld)
            {
                sourceMesh = (new TopologyStatic(sourceMesh, Matrix4x4.identity, true)).ToMesh();
            }

            int numSplits = 0;

            List<int> indices = new(sourceMesh.triangles);
            List<Vector3> vertices = new(sourceMesh.vertices);

            bool restart = true;
            while (restart)
            {
                restart = false;

                if ((maxSplits != 0) && (numSplits >= maxSplits))
                    break;

                for (int i = 0; i < indices.Count; i += 3)
                {
                    int[] tri = { indices[i], indices[i + 1], indices[i + 2] };

                    for (int k = 0; k < 3; k++)
                    {
                        int a = tri[k];
                        int b = tri[(k + 1) % 3];
                        int c = tri[(k + 2) % 3];

                        float d = Vector3.Distance(vertices[a], vertices[b]);
                        if (d <= maxEdgeLength)
                            continue;

                        numSplits++;
                        restart = true;

                        int m = vertices.Count;
                        vertices.Add((vertices[a] + vertices[b]) * 0.5f);

                        // Replace current triangle with (a, m, c)
                        indices[i + 0] = a;
                        indices[i + 1] = m;
                        indices[i + 2] = c;

                        // Add second half (m, b, c)
                        indices.Add(m);
                        indices.Add(b);
                        indices.Add(c);

                        // Split EVERY other triangle sharing edge (a,b) or (b,a)
                        for (int j = 0; j < indices.Count - 3; j += 3)
                        {
                            if (j == i) continue;

                            int t0 = indices[j + 0];
                            int t1 = indices[j + 1];
                            int t2 = indices[j + 2];

                            // check the three directed edges of this triangle
                            for (int e = 0; e < 3; e++)
                            {
                                int o1 = (e == 0) ? t0 : (e == 1) ? t1 : t2;
                                int o2 = (e == 0) ? t1 : (e == 1) ? t2 : t0;
                                int o3 = (e == 0) ? t2 : (e == 1) ? t0 : t1;

                                if (((o1 == a) && (o2 == b)) || ((o1 == b) && (o2 == a)))
                                {
                                    // replace that triangle with (o1, m, o3)
                                    indices[j + 0] = o1;
                                    indices[j + 1] = m;
                                    indices[j + 2] = o3;

                                    // add (m, o2, o3)
                                    indices.Add(m);
                                    indices.Add(o2);
                                    indices.Add(o3);

                                    break;
                                }
                            }
                        }

                        // topology changed; restart from scratch
                        break;
                    }

                    if (restart)
                        break;
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = (vertices.Count > 65535)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.name = sourceMesh.name.Contains("Subdivide") ? sourceMesh.name : sourceMesh.name + " Subdivided";

            Debug.Log($"Ran {numSplits} splits...");

            // Final cleanup / weld
            var topology = new TopologyStatic(mesh, Matrix4x4.identity, true);
            return topology.ToMesh();
        }
        #endregion

        #region Long edge subdivision - Topology only
        public static Mesh SubdivideLongEdgesTopology(Mesh sourceMesh, float maxEdgeLength, int maxPasses)
        {
            TopologyStatic top = new TopologyStatic(sourceMesh, Matrix4x4.identity, true);
            for (int i = 0; i < maxPasses; i++)
            {
                var next = top.SubdivideLongEdges(maxEdgeLength, out var nSplits);
                if (nSplits == 0)
                    break;
                top = next;
            }
            return top.ToMesh();
        }
        #endregion

        #region Long edge subdivision - Full mesh

        private sealed class SubdivisionSubMeshData
        {
            public MeshTopology topology;
            public List<int>    indices;
        }

        private sealed class SubdivisionBlendShapeFrameData
        {
            public string   shapeName;
            public float    frameWeight;

            public List<Vector3> deltaVertices;
            public List<Vector3> deltaNormals;
            public List<Vector3> deltaTangents;
        }

        private static ulong SubdivisionEdgeKey(int a, int b)
        {
            uint min = (uint)Mathf.Min(a, b);
            uint max = (uint)Mathf.Max(a, b);

            return ((ulong)min << 32) | max;
        }

        private static void DecodeSubdivisionEdgeKey(ulong key, out int a, out int b)
        {
            a = (int)(uint)(key >> 32);
            b = (int)(uint)key;
        }

        private static void AppendSubdivisionTriangle(List<int> indices, int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        private static BoneWeight InterpolateSubdivisionBoneWeight(BoneWeight a, BoneWeight b)
        {
            Dictionary<int, float> combinedWeights = new();

            static void AddInfluence(Dictionary<int, float> weights, int boneIndex, float weight)
            {
                if (weight <= 0.0f)
                    return;

                if (weights.TryGetValue(boneIndex, out float currentWeight))
                    weights[boneIndex] = currentWeight + weight;
                else
                    weights.Add(boneIndex, weight);
            }

            AddInfluence(combinedWeights, a.boneIndex0, a.weight0 * 0.5f);
            AddInfluence(combinedWeights, a.boneIndex1, a.weight1 * 0.5f);
            AddInfluence(combinedWeights, a.boneIndex2, a.weight2 * 0.5f);
            AddInfluence(combinedWeights, a.boneIndex3, a.weight3 * 0.5f);

            AddInfluence(combinedWeights, b.boneIndex0, b.weight0 * 0.5f);
            AddInfluence(combinedWeights, b.boneIndex1, b.weight1 * 0.5f);
            AddInfluence(combinedWeights, b.boneIndex2, b.weight2 * 0.5f);
            AddInfluence(combinedWeights, b.boneIndex3, b.weight3 * 0.5f);

            var strongestInfluences = combinedWeights
                .OrderByDescending(entry => entry.Value)
                .Take(4)
                .ToArray();

            float weightSum = strongestInfluences.Sum(entry => entry.Value);

            if (weightSum <= 1e-8f)
                return default;

            BoneWeight result = default;

            for (int i = 0; i < strongestInfluences.Length; i++)
            {
                int boneIndex = strongestInfluences[i].Key;
                float weight = strongestInfluences[i].Value / weightSum;

                switch (i)
                {
                    case 0:
                        result.boneIndex0 = boneIndex;
                        result.weight0 = weight;
                        break;

                    case 1:
                        result.boneIndex1 = boneIndex;
                        result.weight1 = weight;
                        break;

                    case 2:
                        result.boneIndex2 = boneIndex;
                        result.weight2 = weight;
                        break;

                    case 3:
                        result.boneIndex3 = boneIndex;
                        result.weight3 = weight;
                        break;
                }
            }

            return result;
        }

        private static void SetSubdivisionUVChannel(Mesh mesh, int channel, List<Vector4> values, int dimension)
        {
            switch (dimension)
            {
                case 3:
                    {
                        List<Vector3> output = new(values.Count);

                        for (int i = 0; i < values.Count; i++)
                        {
                            Vector4 value = values[i];
                            output.Add(new Vector3(value.x, value.y, value.z));
                        }

                        mesh.SetUVs(channel, output);
                        break;
                    }

                case 4:
                    mesh.SetUVs(channel, values);
                    break;

                case 1:
                case 2:
                default:
                    {
                        List<Vector2> output = new(values.Count);

                        for (int i = 0; i < values.Count; i++)
                        {
                            Vector4 value = values[i];
                            output.Add(new Vector2(value.x, value.y));
                        }

                        mesh.SetUVs(channel, output);
                        break;
                    }
            }
        }

        public static Mesh SubdivideLongEdgesImproved(Mesh sourceMesh, float maxEdgeLength, int maxPasses)
        {
            if (sourceMesh == null)
                throw new ArgumentNullException(nameof(sourceMesh));

            if (!sourceMesh.isReadable)
            {
                Debug.LogError($"Cannot subdivide mesh '{sourceMesh.name}': the mesh is not readable.");

                return null;
            }

            if (maxEdgeLength <= 0.0f)
                throw new ArgumentOutOfRangeException(nameof(maxEdgeLength));

            maxPasses = Mathf.Max(0, maxPasses);

            int originalVertexCount = sourceMesh.vertexCount;

            // ---------------------------------------------------------------------
            // Vertex channels
            // ---------------------------------------------------------------------

            List<Vector3> vertices = new(sourceMesh.vertices);

            Vector3[] sourceNormals = sourceMesh.normals;
            List<Vector3> normals = (sourceNormals.Length == originalVertexCount) ? (new List<Vector3>(sourceNormals)) : (null);

            Vector4[] sourceTangents = sourceMesh.tangents;
            List<Vector4> tangents = (sourceTangents.Length == originalVertexCount) ? (new List<Vector4>(sourceTangents)) : (null);

            Color[] sourceColors = sourceMesh.colors;
            List<Color> colors = (sourceColors.Length == originalVertexCount) ? (new List<Color>(sourceColors)) : (null);

            List<Vector4>[] uvChannels = new List<Vector4>[8];
            int[] uvDimensions = new int[8];

            for (int channel = 0; channel < 8; channel++)
            {
                VertexAttribute attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);

                if (!sourceMesh.HasVertexAttribute(attribute))
                    continue;

                List<Vector4> values = new();
                sourceMesh.GetUVs(channel, values);

                if (values.Count != originalVertexCount)
                    continue;

                uvChannels[channel] = values;
                uvDimensions[channel] = sourceMesh.GetVertexAttributeDimension(attribute);
            }

            BoneWeight[] sourceBoneWeights = sourceMesh.boneWeights;
            List<BoneWeight> boneWeights = (sourceBoneWeights.Length == originalVertexCount) ? (new List<BoneWeight>(sourceBoneWeights)) : (null);

            // ---------------------------------------------------------------------
            // Blend shapes
            // ---------------------------------------------------------------------

            List<SubdivisionBlendShapeFrameData> blendShapeFrames = new();

            for (int shapeIndex = 0; shapeIndex < sourceMesh.blendShapeCount; shapeIndex++)
            {
                string shapeName = sourceMesh.GetBlendShapeName(shapeIndex);
                int frameCount = sourceMesh.GetBlendShapeFrameCount(shapeIndex);

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    Vector3[] deltaVertices = new Vector3[originalVertexCount];
                    Vector3[] deltaNormals = new Vector3[originalVertexCount];
                    Vector3[] deltaTangents = new Vector3[originalVertexCount];

                    sourceMesh.GetBlendShapeFrameVertices(shapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                    blendShapeFrames.Add(new SubdivisionBlendShapeFrameData
                        {
                            shapeName = shapeName,
                            frameWeight = sourceMesh.GetBlendShapeFrameWeight(shapeIndex, frameIndex),
                            deltaVertices = new List<Vector3>(deltaVertices),
                            deltaNormals = new List<Vector3>(deltaNormals),
                            deltaTangents = new List<Vector3>(deltaTangents)
                        });
                }
            }

            // ---------------------------------------------------------------------
            // Submeshes
            // ---------------------------------------------------------------------

            List<SubdivisionSubMeshData> subMeshes = new(sourceMesh.subMeshCount);

            for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; subMeshIndex++)
            {
                subMeshes.Add(new SubdivisionSubMeshData
                    {
                        topology = sourceMesh.GetTopology(subMeshIndex),

                        // Apply the original base vertex, because the generated mesh will use baseVertex = 0.
                        indices = new List<int>(sourceMesh.GetIndices(subMeshIndex, applyBaseVertex: true))
                    });
            }

            float maxEdgeLengthSq = maxEdgeLength * maxEdgeLength;

            int totalSplitCount = 0;

            // ---------------------------------------------------------------------
            // Subdivision passes
            // ---------------------------------------------------------------------

            for (int pass = 0; pass < maxPasses; pass++)
            {
                HashSet<ulong> edgesToSplit = new();

                void TestEdge(int a, int b)
                {
                    if (a == b) return;

                    if ((a < 0) || (b < 0) || (a >= vertices.Count) || (b >= vertices.Count))
                    {
                        return;
                    }

                    float edgeLengthSq = (vertices[a] - vertices[b]).sqrMagnitude;

                    if (edgeLengthSq > maxEdgeLengthSq)
                    {
                        edgesToSplit.Add(SubdivisionEdgeKey(a, b));
                    }
                }

                // Find all long triangle edges first. This ensures that shared edges
                // are split consistently by all adjacent triangles.
                for (int subMeshIndex = 0; subMeshIndex < subMeshes.Count; subMeshIndex++)
                {
                    SubdivisionSubMeshData subMesh = subMeshes[subMeshIndex];

                    if (subMesh.topology != MeshTopology.Triangles)
                        continue;

                    List<int> indices = subMesh.indices;

                    for (int i = 0; i + 2 < indices.Count; i += 3)
                    {
                        int a = indices[i + 0];
                        int b = indices[i + 1];
                        int c = indices[i + 2];

                        TestEdge(a, b);
                        TestEdge(b, c);
                        TestEdge(c, a);
                    }
                }

                if (edgesToSplit.Count == 0) break;

                // Sorting makes generated vertex/index order deterministic.
                List<ulong> sortedEdges = new(edgesToSplit);
                sortedEdges.Sort();

                Dictionary<ulong, int> midpointIndices = new(sortedEdges.Count);

                // -----------------------------------------------------------------
                // Create one midpoint vertex for every selected edge.
                // -----------------------------------------------------------------

                for (int edgeIndex = 0; edgeIndex < sortedEdges.Count; edgeIndex++)
                {
                    ulong edgeKey = sortedEdges[edgeIndex];

                    DecodeSubdivisionEdgeKey(edgeKey, out int a, out int b);

                    int midpointIndex = vertices.Count;

                    midpointIndices.Add(edgeKey, midpointIndex);

                    vertices.Add((vertices[a] + vertices[b]) * 0.5f);

                    if (normals != null)
                    {
                        Vector3 normal = (normals[a] + normals[b]) * 0.5f;

                        normal.SafeNormalize();
                        normals.Add(normal);
                    }

                    if (tangents != null)
                    {
                        Vector3 tangent = (tangents[a].xyz() + tangents[b].xyz()) * 0.5f;

                        tangent.SafeNormalize();

                        float handednessSum = tangents[a].w + tangents[b].w;

                        float handedness;

                        if (Mathf.Abs(handednessSum) > 1e-6f)
                        {
                            handedness = Mathf.Sign(handednessSum);
                        }
                        else if (Mathf.Abs(tangents[a].w) > 1e-6f)
                        {
                            handedness = Mathf.Sign(tangents[a].w);
                        }
                        else if (Mathf.Abs(tangents[b].w) > 1e-6f)
                        {
                            handedness = Mathf.Sign(tangents[b].w);
                        }
                        else
                        {
                            handedness = 1.0f;
                        }

                        tangents.Add(tangent.xyzw(handedness));
                    }

                    if (colors != null)
                    {
                        colors.Add(Color.LerpUnclamped(colors[a], colors[b], 0.5f));
                    }

                    for (int channel = 0; channel < 8; channel++)
                    {
                        List<Vector4> channelValues = uvChannels[channel];

                        if (channelValues == null)
                            continue;

                        channelValues.Add(Vector4.LerpUnclamped(channelValues[a], channelValues[b],0.5f));
                    }

                    if (boneWeights != null)
                    {
                        boneWeights.Add(InterpolateSubdivisionBoneWeight(boneWeights[a], boneWeights[b]));
                    }

                    for (int frameIndex = 0; frameIndex < blendShapeFrames.Count; frameIndex++)
                    {
                        SubdivisionBlendShapeFrameData frame = blendShapeFrames[frameIndex];

                        frame.deltaVertices.Add(Vector3.LerpUnclamped(frame.deltaVertices[a], frame.deltaVertices[b], 0.5f));
                        frame.deltaNormals.Add(Vector3.LerpUnclamped(frame.deltaNormals[a], frame.deltaNormals[b], 0.5f));
                        frame.deltaTangents.Add(Vector3.LerpUnclamped(frame.deltaTangents[a], frame.deltaTangents[b], 0.5f));
                    }
                }

                totalSplitCount += midpointIndices.Count;

                // -----------------------------------------------------------------
                // Retriangulate each triangle according to which edges were split.
                //
                // Bits:
                //   1 = AB
                //   2 = BC
                //   4 = CA
                // -----------------------------------------------------------------

                for (int subMeshIndex = 0; subMeshIndex < subMeshes.Count; subMeshIndex++)
                {
                    SubdivisionSubMeshData subMesh = subMeshes[subMeshIndex];

                    if (subMesh.topology != MeshTopology.Triangles)
                        continue;

                    List<int> sourceIndices = subMesh.indices;

                    List<int> outputIndices = new(sourceIndices.Count * 2);

                    for (int i = 0; i + 2 < sourceIndices.Count; i += 3)
                    {
                        int a = sourceIndices[i + 0];
                        int b = sourceIndices[i + 1];
                        int c = sourceIndices[i + 2];

                        bool splitAB = midpointIndices.TryGetValue(SubdivisionEdgeKey(a, b), out int midpointAB);
                        bool splitBC = midpointIndices.TryGetValue(SubdivisionEdgeKey(b, c), out int midpointBC);
                        bool splitCA = midpointIndices.TryGetValue(SubdivisionEdgeKey(c, a), out int midpointCA);

                        int splitMask = (splitAB ? 1 : 0) | (splitBC ? 2 : 0) | (splitCA ? 4 : 0);

                        switch (splitMask)
                        {
                            // No split.
                            case 0:
                                AppendSubdivisionTriangle(outputIndices, a, b, c);
                                break;
                            // AB only.
                            case 1:
                                AppendSubdivisionTriangle(outputIndices, a, midpointAB, c);
                                AppendSubdivisionTriangle(outputIndices, midpointAB, b, c);
                                break;
                            // BC only.
                            case 2:
                                AppendSubdivisionTriangle(outputIndices, a, b, midpointBC);
                                AppendSubdivisionTriangle(outputIndices, a, midpointBC, c);
                                break;
                            // AB and BC.
                            case 3:
                                AppendSubdivisionTriangle(outputIndices, a, midpointAB, c);
                                AppendSubdivisionTriangle(outputIndices, midpointAB, midpointBC, c);
                                AppendSubdivisionTriangle(outputIndices, midpointAB, b, midpointBC);
                                break;
                            // CA only.
                            case 4:
                                AppendSubdivisionTriangle(outputIndices, a, b, midpointCA);
                                AppendSubdivisionTriangle(outputIndices, midpointCA, b, c);
                                break;
                            // CA and AB.
                            case 5:
                                AppendSubdivisionTriangle(outputIndices, a, midpointAB, midpointCA);
                                AppendSubdivisionTriangle(outputIndices, midpointAB, b, c);
                                AppendSubdivisionTriangle(outputIndices, midpointAB, c, midpointCA);
                                break;
                            // BC and CA.
                            case 6:
                                AppendSubdivisionTriangle(outputIndices, a, b, midpointCA);
                                AppendSubdivisionTriangle(outputIndices, b, midpointBC, midpointCA);
                                AppendSubdivisionTriangle(outputIndices, midpointBC, c, midpointCA);
                                break;
                            // All three edges.
                            case 7:
                                AppendSubdivisionTriangle(outputIndices, a, midpointAB, midpointCA);
                                AppendSubdivisionTriangle(outputIndices, midpointAB, b, midpointBC);
                                AppendSubdivisionTriangle(outputIndices, midpointCA, midpointBC, c);
                                AppendSubdivisionTriangle(outputIndices, midpointAB, midpointBC, midpointCA);
                                break;
                        }
                    }

                    subMesh.indices = outputIndices;
                }
            }

            // ---------------------------------------------------------------------
            // Create result
            // ---------------------------------------------------------------------

            Mesh result = new Mesh
            {
                name = (sourceMesh.name.Contains("Subdivided")) ? (sourceMesh.name) : ($"{sourceMesh.name} Subdivided"),
                indexFormat = ((vertices.Count > 65535) || (sourceMesh.indexFormat == IndexFormat.UInt32)) ? (IndexFormat.UInt32) : (IndexFormat.UInt16)
            };

            result.SetVertices(vertices);

            if ((normals != null) && (normals.Count == vertices.Count))
            {
                result.SetNormals(normals);
            }

            if ((tangents != null) && (tangents.Count == vertices.Count))
            {
                result.SetTangents(tangents);
            }

            if ((colors != null) && (colors.Count == vertices.Count))
            {
                result.SetColors(colors);
            }

            for (int channel = 0; channel < 8; channel++)
            {
                List<Vector4> channelValues = uvChannels[channel];

                if ((channelValues == null) || (channelValues.Count != vertices.Count))
                {
                    continue;
                }

                SetSubdivisionUVChannel(result, channel, channelValues, uvDimensions[channel]);
            }

            if ((boneWeights != null) && (boneWeights.Count == vertices.Count))
            {
                result.boneWeights = boneWeights.ToArray();
            }

            Matrix4x4[] bindPoses = sourceMesh.bindposes;

            if ((bindPoses != null) && (bindPoses.Length > 0))
            {
                result.bindposes = bindPoses;
            }

            result.subMeshCount = subMeshes.Count;

            for (int subMeshIndex = 0; subMeshIndex < subMeshes.Count; subMeshIndex++)
            {
                SubdivisionSubMeshData subMesh = subMeshes[subMeshIndex];

                result.SetIndices(subMesh.indices, subMesh.topology, subMeshIndex, calculateBounds: false);
            }

            for (int frameIndex = 0; frameIndex < blendShapeFrames.Count; frameIndex++)
            {
                SubdivisionBlendShapeFrameData frame = blendShapeFrames[frameIndex];

                result.AddBlendShapeFrame(frame.shapeName, frame.frameWeight, frame.deltaVertices.ToArray(), frame.deltaNormals.ToArray(), frame.deltaTangents.ToArray());
            }

            result.RecalculateBounds();

            return result;
        }

        #endregion
    }
}
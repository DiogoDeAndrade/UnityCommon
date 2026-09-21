using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;

namespace UC
{
    public static partial class MeshTools
    {
        /// <summary>
        /// Cuts every triangle along the planes of a uniform axis-aligned grid, so that no triangle
        /// of the result crosses a grid plane. The grid lives in whatever space meshToGrid maps the
        /// mesh into: its planes sit at gridOrigin + k * cellSize on each axis, for every integer k.
        /// A triangle that crosses no plane is kept exactly as it is, so a mesh that already fits
        /// its cells comes back as a plain copy.
        ///
        /// Written for a mesh deformed through a piecewise field. The deformation field's trilinear
        /// regions are bounded by the planes through its cell centres; the map is continuous but
        /// kinked there, and a triangle spanning a kink can only render it as a flat face and can
        /// only measure it as its own distortion. Cut at those planes, each triangle sits inside one
        /// region - the same arrangement the field's own sample tetrahedra have.
        ///
        /// Deliberately a second helper beside SubdivideLongEdgesImproved rather than a refactor of
        /// it. That function feeds the navmesh subdivision every golden dump was captured through,
        /// and its midpoint arithmetic is part of what those dumps reproduce; the channel handling
        /// is repeated here so that it does not have to move.
        ///
        /// Each triangle is clipped by every plane it spans, axis by axis, both sides kept
        /// (Sutherland-Hodgman), which leaves convex pieces that are fan-triangulated. A cut point on
        /// an edge is created once per (edge, plane) and shared by every piece and every neighbouring
        /// triangle that meets it - keyed on the edge's vertex indices in canonical order, so the two
        /// sides compute the same parameter and the result stays watertight where the source was. A
        /// vertex within snapEpsilon of a plane, in cell units, counts as on it and is never split,
        /// which is what stops a vertex lying on a plane from producing zero-length edges. Every
        /// vertex channel the source carries is interpolated linearly along the cut edge.
        /// </summary>
        /// <param name="cutTriangles">How many source triangles were cut into more than one piece.</param>
        public static Mesh SubdivideUniformGrid(Mesh sourceMesh, Matrix4x4 meshToGrid, Vector3 gridOrigin, Vector3 cellSize, out int cutTriangles, float snapEpsilon = 1e-5f)
        {
            cutTriangles = 0;

            if (sourceMesh == null)
                throw new ArgumentNullException(nameof(sourceMesh));

            if (!sourceMesh.isReadable)
            {
                Debug.LogError($"Cannot grid-cut mesh '{sourceMesh.name}': the mesh is not readable.");

                return null;
            }

            if ((cellSize.x <= 0.0f) || (cellSize.y <= 0.0f) || (cellSize.z <= 0.0f))
                throw new ArgumentOutOfRangeException(nameof(cellSize));

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

            // Where each vertex sits in cell units - the coordinate the planes are integers of.
            List<Vector3> gridPositions = new(originalVertexCount);

            Vector3 ToGrid(Vector3 meshPosition)
            {
                Vector3 p = meshToGrid.MultiplyPoint3x4(meshPosition) - gridOrigin;

                return new Vector3(p.x / cellSize.x, p.y / cellSize.y, p.z / cellSize.z);
            }

            for (int v = 0; v < originalVertexCount; v++) gridPositions.Add(ToGrid(vertices[v]));

            // ---------------------------------------------------------------------
            // Cut vertices: one per (edge, plane), shared by everything that meets it.
            // ---------------------------------------------------------------------

            Dictionary<(int, int, int, int), int> cutVertices = new();

            int SplitEdge(int a, int b, int axis, int plane)
            {
                // Canonical order, so both triangles on this edge - and every piece of each - ask
                // for the same key and get the same vertex, computed from the same operands.
                if (a > b) (a, b) = (b, a);

                var key = (a, b, axis, plane);

                if (cutVertices.TryGetValue(key, out int existing)) return existing;

                float da = gridPositions[a][axis] - plane;
                float db = gridPositions[b][axis] - plane;
                float t = da / (da - db);

                int index = vertices.Count;

                cutVertices.Add(key, index);

                vertices.Add(Vector3.LerpUnclamped(vertices[a], vertices[b], t));

                // Placed exactly on the plane in grid units, so no later plane of this axis and no
                // snapping tolerance can read the new vertex as crossing the plane it lies in.
                Vector3 grid = Vector3.LerpUnclamped(gridPositions[a], gridPositions[b], t);
                grid[axis] = plane;
                gridPositions.Add(grid);

                if (normals != null)
                {
                    Vector3 normal = Vector3.LerpUnclamped(normals[a], normals[b], t);

                    normal.SafeNormalize();
                    normals.Add(normal);
                }

                if (tangents != null)
                {
                    Vector3 tangent = Vector3.LerpUnclamped(tangents[a].xyz(), tangents[b].xyz(), t);

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
                    colors.Add(Color.LerpUnclamped(colors[a], colors[b], t));
                }

                for (int channel = 0; channel < 8; channel++)
                {
                    List<Vector4> channelValues = uvChannels[channel];

                    if (channelValues == null)
                        continue;

                    channelValues.Add(Vector4.LerpUnclamped(channelValues[a], channelValues[b], t));
                }

                if (boneWeights != null)
                {
                    boneWeights.Add(LerpSubdivisionBoneWeight(boneWeights[a], boneWeights[b], t));
                }

                for (int frameIndex = 0; frameIndex < blendShapeFrames.Count; frameIndex++)
                {
                    SubdivisionBlendShapeFrameData frame = blendShapeFrames[frameIndex];

                    frame.deltaVertices.Add(Vector3.LerpUnclamped(frame.deltaVertices[a], frame.deltaVertices[b], t));
                    frame.deltaNormals.Add(Vector3.LerpUnclamped(frame.deltaNormals[a], frame.deltaNormals[b], t));
                    frame.deltaTangents.Add(Vector3.LerpUnclamped(frame.deltaTangents[a], frame.deltaTangents[b], t));
                }

                return index;
            }

            // Splits one convex piece by one plane into the pieces on either side, dropping any side
            // that degenerates to fewer than three vertices. A vertex on the plane goes to both
            // sides and never makes a cut vertex, which is what keeps the pieces free of zero-length
            // edges. Winding is preserved on both sides.
            List<float> distances = new(8);

            void ClipPiece(List<int> piece, int axis, int plane, List<List<int>> output)
            {
                int n = piece.Count;

                distances.Clear();

                bool anyFront = false;
                bool anyBack = false;

                for (int i = 0; i < n; i++)
                {
                    float d = gridPositions[piece[i]][axis] - plane;

                    if (Mathf.Abs(d) <= snapEpsilon) d = 0.0f;

                    distances.Add(d);

                    if (d > 0.0f) anyFront = true;
                    if (d < 0.0f) anyBack = true;
                }

                if (!(anyFront && anyBack))
                {
                    output.Add(piece);
                    return;
                }

                List<int> front = new(n + 2);
                List<int> back = new(n + 2);

                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;

                    int current = piece[i];
                    float dc = distances[i];
                    float dn = distances[next];

                    if (dc >= 0.0f) front.Add(current);
                    if (dc <= 0.0f) back.Add(current);

                    if (((dc > 0.0f) && (dn < 0.0f)) || ((dc < 0.0f) && (dn > 0.0f)))
                    {
                        int split = SplitEdge(current, piece[next], axis, plane);

                        front.Add(split);
                        back.Add(split);
                    }
                }

                if (front.Count >= 3) output.Add(front);
                if (back.Count >= 3) output.Add(back);
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
                        indices = new List<int>(sourceMesh.GetIndices(subMeshIndex, applyBaseVertex: true))
                    });
            }

            List<List<int>> pieces = new();
            List<List<int>> nextPieces = new();

            for (int subMeshIndex = 0; subMeshIndex < subMeshes.Count; subMeshIndex++)
            {
                SubdivisionSubMeshData subMesh = subMeshes[subMeshIndex];

                if (subMesh.topology != MeshTopology.Triangles)
                    continue;

                List<int> sourceIndices = subMesh.indices;
                List<int> outputIndices = new(sourceIndices.Count);

                for (int i = 0; i + 2 < sourceIndices.Count; i += 3)
                {
                    int a = sourceIndices[i + 0];
                    int b = sourceIndices[i + 1];
                    int c = sourceIndices[i + 2];

                    pieces.Clear();
                    pieces.Add(new List<int>(3) { a, b, c });

                    for (int axis = 0; axis < 3; axis++)
                    {
                        float low = Mathf.Min(gridPositions[a][axis], Mathf.Min(gridPositions[b][axis], gridPositions[c][axis]));
                        float high = Mathf.Max(gridPositions[a][axis], Mathf.Max(gridPositions[b][axis], gridPositions[c][axis]));

                        // The planes strictly inside the triangle's extent on this axis. A plane a
                        // vertex sits on is harmless either way - snapping makes it a no-op - but
                        // there is no point asking.
                        int firstPlane = Mathf.FloorToInt(low) + 1;
                        int lastPlane = Mathf.CeilToInt(high) - 1;

                        for (int plane = firstPlane; plane <= lastPlane; plane++)
                        {
                            nextPieces.Clear();

                            for (int p = 0; p < pieces.Count; p++)
                                ClipPiece(pieces[p], axis, plane, nextPieces);

                            (pieces, nextPieces) = (nextPieces, pieces);
                        }
                    }

                    if ((pieces.Count != 1) || (pieces[0].Count != 3)) cutTriangles++;

                    for (int p = 0; p < pieces.Count; p++)
                    {
                        List<int> piece = pieces[p];

                        // Convex by construction, so a fan from any vertex triangulates it.
                        for (int j = 1; j + 1 < piece.Count; j++)
                            AppendSubdivisionTriangle(outputIndices, piece[0], piece[j], piece[j + 1]);
                    }
                }

                subMesh.indices = outputIndices;
            }

            // ---------------------------------------------------------------------
            // Create result
            // ---------------------------------------------------------------------

            Mesh result = new Mesh
            {
                name = (sourceMesh.name.Contains("GridCut")) ? (sourceMesh.name) : ($"{sourceMesh.name} GridCut"),
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

        /// <summary>
        /// A bone weight at parameter t along an edge: both endpoints' bones pooled with their
        /// weights scaled by (1 - t) and t, the four heaviest kept and renormalized. The midpoint
        /// helper the other subdivision uses is a special case of this at t = 0.5, kept separate so
        /// its arithmetic stays exactly what it was.
        /// </summary>
        private static BoneWeight LerpSubdivisionBoneWeight(BoneWeight a, BoneWeight b, float t)
        {
            Dictionary<int, float> pooled = new(8);

            void Pool(int bone, float weight)
            {
                if (weight <= 0.0f) return;

                pooled.TryGetValue(bone, out float existing);
                pooled[bone] = existing + weight;
            }

            float wa = 1.0f - t;

            Pool(a.boneIndex0, a.weight0 * wa);
            Pool(a.boneIndex1, a.weight1 * wa);
            Pool(a.boneIndex2, a.weight2 * wa);
            Pool(a.boneIndex3, a.weight3 * wa);
            Pool(b.boneIndex0, b.weight0 * t);
            Pool(b.boneIndex1, b.weight1 * t);
            Pool(b.boneIndex2, b.weight2 * t);
            Pool(b.boneIndex3, b.weight3 * t);

            List<KeyValuePair<int, float>> sorted = new(pooled);
            sorted.Sort((x, y) => y.Value.CompareTo(x.Value));

            int[] bones = new int[4];
            float[] weights = new float[4];
            float total = 0.0f;

            for (int i = 0; (i < 4) && (i < sorted.Count); i++)
            {
                bones[i] = sorted[i].Key;
                weights[i] = sorted[i].Value;
                total += weights[i];
            }

            if (total > 0.0f)
            {
                for (int i = 0; i < 4; i++) weights[i] /= total;
            }

            return new BoneWeight
            {
                boneIndex0 = bones[0], weight0 = weights[0],
                boneIndex1 = bones[1], weight1 = weights[1],
                boneIndex2 = bones[2], weight2 = weights[2],
                boneIndex3 = bones[3], weight3 = weights[3]
            };
        }
    }
}

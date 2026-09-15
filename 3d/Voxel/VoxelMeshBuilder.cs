using UnityEngine;
using System.Collections.Generic;

namespace UC
{
    /// <summary>
    /// Runtime mesh construction from <see cref="VoxelData"/>.
    /// Two flavours: palette (UVs into a tiny palette texture) and vertex colour.
    /// Asset saving lives in the editor-only VoxelMeshExporter, which calls into here.
    /// </summary>
    public static class VoxelMeshBuilder
    {
        // ---------------------------------------------------------------------
        // Palette atlas
        // ---------------------------------------------------------------------

        /// <summary>
        /// Builds the UV lookup for the unique palette list, using a power-of-two square atlas so texel
        /// centres are always at (col+0.5)/W, (row+0.5)/H and nothing gets rescaled on import.
        /// </summary>
        public static (Dictionary<Color32, Vector2> uvMap, int texW, int texH) BuildUVMap(List<Color32> palette)
        {
            int count = palette.Count;

            int side = Mathf.Max(1, Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(count))));
            int texW = side;
            int texH = Mathf.Max(1, Mathf.NextPowerOfTwo(Mathf.CeilToInt((float)count / texW)));

            var uvMap = new Dictionary<Color32, Vector2>(VoxelBuilder.Color32EqualityComparer.Instance);
            for (int i = 0; i < palette.Count; i++)
            {
                int col = i % texW;
                int row = i / texW;
                uvMap[palette[i]] = new Vector2((col + 0.5f) / texW, (row + 0.5f) / texH);
            }

            return (uvMap, texW, texH);
        }

        /// <summary>Palette pixels laid out to match <see cref="BuildUVMap"/> (row-major, left-to-right).</summary>
        public static Color32[] BuildPalettePixels(List<Color32> palette, int texW, int texH)
        {
            var pixels = new Color32[texW * texH];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            for (int i = 0; i < palette.Count; i++)
            {
                pixels[(i / texW) * texW + (i % texW)] = palette[i];
            }
            return pixels;
        }

        /// <summary>Runtime palette texture (Point + Clamp, no mips) matching <see cref="BuildUVMap"/>.</summary>
        public static Texture2D BuildPaletteTexture(List<Color32> palette, int texW, int texH, string name = "VoxelPalette")
        {
            var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = name,
            };
            tex.SetPixels32(BuildPalettePixels(palette, texW, texH));
            tex.Apply(false);
            return tex;
        }

        // ---------------------------------------------------------------------
        // Mesh builders
        // ---------------------------------------------------------------------

        /// <param name="cullHiddenFaces">Skip faces shared between two voxels (they can never be seen).</param>
        public static Mesh BuildMeshPalette(VoxelData voxels, Dictionary<Color32, Vector2> uvMap, bool cullHiddenFaces = true)
        {
            var verts   = new List<Vector3>();
            var norms   = new List<Vector3>();
            var uvs     = new List<Vector2>();
            var indices = new List<int>();
            var occupied = cullHiddenFaces ? BuildOccupancy(voxels) : null;

            foreach (var voxel in voxels.Voxels)
            {
                var uv = uvMap.TryGetValue((Color32)voxel.Color, out var found) ? found : Vector2.zero;
                AppendBox(verts, norms, uvs, null, indices, voxel, uv, default, occupied);
            }

            return Finalise(verts, norms, uvs, null, indices);
        }

        /// <param name="linearToGamma">
        /// Set when the voxel colours came from GetPixels on an sRGB texture (linear values): vertex colours
        /// are passed straight to the shader, so they must be converted back to gamma space.
        /// </param>
        public static Mesh BuildMeshVertexColor(VoxelData voxels, bool linearToGamma, bool cullHiddenFaces = true)
        {
            var verts   = new List<Vector3>();
            var norms   = new List<Vector3>();
            var colors  = new List<Color32>();
            var indices = new List<int>();
            var occupied = cullHiddenFaces ? BuildOccupancy(voxels) : null;

            foreach (var voxel in voxels.Voxels)
            {
                Color c = linearToGamma ? voxel.Color.linear : voxel.Color;
                AppendBox(verts, norms, null, colors, indices, voxel, Vector2.zero, (Color32)c, occupied);
            }

            return Finalise(verts, norms, null, colors, indices);
        }

        // ---------------------------------------------------------------------
        // Box appender
        // ---------------------------------------------------------------------

        static readonly Vector3[] s_FaceNormals =
        {
            Vector3.right,   Vector3.left,
            Vector3.up,      Vector3.down,
            Vector3.forward, Vector3.back,
        };

        // Grid offset of the neighbour across each face (col, row, slice)
        static readonly Vector3Int[] s_FaceNeighbours =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
        };

        static readonly Vector3[][] s_FaceCorners =
        {
            // +X
            new[]{ new Vector3( 1,-1,-1), new Vector3( 1, 1,-1), new Vector3( 1, 1, 1), new Vector3( 1,-1, 1) },
            // -X
            new[]{ new Vector3(-1,-1, 1), new Vector3(-1, 1, 1), new Vector3(-1, 1,-1), new Vector3(-1,-1,-1) },
            // +Y
            new[]{ new Vector3(-1, 1,-1), new Vector3(-1, 1, 1), new Vector3( 1, 1, 1), new Vector3( 1, 1,-1) },
            // -Y
            new[]{ new Vector3(-1,-1, 1), new Vector3(-1,-1,-1), new Vector3( 1,-1,-1), new Vector3( 1,-1, 1) },
            // +Z
            new[]{ new Vector3(-1,-1, 1), new Vector3( 1,-1, 1), new Vector3( 1, 1, 1), new Vector3(-1, 1, 1) },
            // -Z
            new[]{ new Vector3( 1,-1,-1), new Vector3(-1,-1,-1), new Vector3(-1, 1,-1), new Vector3( 1, 1,-1) },
        };

        static HashSet<Vector3Int> BuildOccupancy(VoxelData voxels)
        {
            var set = new HashSet<Vector3Int>();
            foreach (var v in voxels.Voxels) set.Add(new Vector3Int(v.Col, v.Row, v.Slice));
            return set;
        }

        static void AppendBox(
            List<Vector3>       verts,
            List<Vector3>       norms,
            List<Vector2>       uvs,
            List<Color32>       colors,
            List<int>           indices,
            Voxel               voxel,
            Vector2             uv,
            Color32             color32,
            HashSet<Vector3Int> occupied)
        {
            Vector3 c = voxel.WorldCenter;
            Vector3 h = voxel.HalfSize;
            var cell = new Vector3Int(voxel.Col, voxel.Row, voxel.Slice);

            for (int face = 0; face < 6; face++)
            {
                if ((occupied != null) && occupied.Contains(cell + s_FaceNeighbours[face])) continue;

                int baseIdx = verts.Count;
                var corners = s_FaceCorners[face];

                for (int k = 0; k < 4; k++)
                {
                    Vector3 cr = corners[k];
                    verts.Add(c + new Vector3(cr.x * h.x, cr.y * h.y, cr.z * h.z));
                    norms.Add(s_FaceNormals[face]);
                    uvs?.Add(uv);
                    colors?.Add(color32);
                }

                indices.Add(baseIdx);     indices.Add(baseIdx + 1); indices.Add(baseIdx + 2);
                indices.Add(baseIdx);     indices.Add(baseIdx + 2); indices.Add(baseIdx + 3);
            }
        }

        static Mesh Finalise(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<Color32> colors, List<int> indices)
        {
            var mesh = new Mesh();
            mesh.indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            if (uvs    != null) mesh.SetUVs(0, uvs);
            if (colors != null) mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

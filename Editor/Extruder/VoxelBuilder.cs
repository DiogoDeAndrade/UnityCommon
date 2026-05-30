// Made using Claude (claude-sonnet-4-6) - https://claude.ai
using UnityEngine;
using System.Collections.Generic;

namespace UC
{
    // -------------------------------------------------------------------------
    // Data model
    // -------------------------------------------------------------------------

    /// <summary>
    /// A single voxel cell.
    /// (Col, Row, Slice) are grid-space coordinates.
    /// WorldCenter is the centre of the voxel box in local-object space.
    /// </summary>
    public struct Voxel
    {
        public int     Col;
        public int     Row;
        public int     Slice;        // physical Z layer index
        public Color   Color;        // point-sampled colour of the cell's centre pixel
        public Vector3 WorldCenter;
        public Vector3 HalfSize;     // half-extents of the voxel box
    }

    /// <summary>
    /// The full set of non-transparent voxels sampled from a texture,
    /// plus the de-duplicated palette used to build the atlas.
    /// </summary>
    public class VoxelData
    {
        public List<Voxel>   Voxels    = new();
        /// <summary>
        /// Unique Color32 values present in the source image, in insertion order.
        /// Built once from the XY colour map so every Z-slice voxel referencing
        /// the same source pixel maps to exactly the same palette entry.
        /// </summary>
        public List<Color32> Palette   = new();
        public int           GridCols;
        public int           GridRows;
        public int           ZGridSize;
        public Vector3       VoxelSize;
    }

    // -------------------------------------------------------------------------
    // Sampler
    // -------------------------------------------------------------------------

    public static class VoxelBuilder
    {
        /// <summary>
        /// Samples <paramref name="pixels"/> (pre-read via <see cref="ReadPixelsSafe"/>)
        /// into a 3-D grid of voxels.
        ///
        /// XY grid: each cell covers <paramref name="xyGridSize"/> x <paramref name="xyGridSize"/>
        /// source pixels. Cells whose centre pixel is not fully opaque (alpha &lt; 255) are discarded.
        ///
        /// Z slices: <paramref name="zGridSize"/> slices are generated along depth.
        /// Slice 0 = full silhouette; each successive slice erodes the boundary
        /// inward by one voxel. Slices are mirrored symmetrically so the final
        /// shape is centred at Z = 0.
        ///
        /// The unique colour palette is derived once from the XY colour map so
        /// that every voxel referencing the same source pixel maps to the same
        /// atlas entry across all Z layers.
        /// </summary>
        public static VoxelData BuildFromTexture(
            Color[]   pixels,
            int       texW,
            int       texH,
            int       xyGridSize,
            int       zGridSize,
            Vector3   voxelSize)
        {
            int cols = Mathf.CeilToInt((float)texW / xyGridSize);
            int rows = Mathf.CeilToInt((float)texH / xyGridSize);

            // -- Build base XY colour map (slice 0) ----------------------------
            // Pure point sampling: read the single centre pixel of each grid cell.
            // No averaging - blending across colour boundaries creates colours
            // that do not exist in the source image.
            var colourMap = new Color[rows, cols];
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                    colourMap[row, col] = PointSampleCell(pixels, texW, col, row, xyGridSize);

            // -- Build unique palette from the XY map (not per-voxel) ----------
            // Deriving it here - before Z replication - ensures every voxel that
            // comes from the same XY cell maps to exactly the same Color32.
            var paletteList = new List<Color32>();
            var paletteSeen = new HashSet<Color32>(Color32EqualityComparer.Instance);
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var c = colourMap[row, col];
                    // Only fully-opaque pixels (alpha == 255) enter the palette.
                    // Semi-transparent values from Photoshop AA, JPEG artefacts,
                    // or hidden layers produce spurious palette entries.
                    if (((Color32)c).a < 255) continue;
                    var c32 = (Color32)c;
                    if (paletteSeen.Add(c32))
                        paletteList.Add(c32);
                }
            }

            // -- Build filled masks per Z slice via erosion --------------------
            var masks = new bool[zGridSize][,];
            masks[0] = BuildBaseMask(colourMap, rows, cols);
            for (int z = 1; z < zGridSize; z++)
                masks[z] = ErodeMask(masks[z - 1], rows, cols);

            // -- World-space extents and origin --------------------------------
            int     totalLayers = 2 * zGridSize - 1;
            Vector3 origin      = new Vector3(
                -cols * voxelSize.x * 0.5f,
                -rows * voxelSize.y * 0.5f,
                -totalLayers * voxelSize.z * 0.5f);
            Vector3 half = voxelSize * 0.5f;

            var data = new VoxelData
            {
                GridCols  = cols,
                GridRows  = rows,
                ZGridSize = zGridSize,
                VoxelSize = voxelSize,
                Palette   = paletteList,
            };

            // -- Emit voxels for every physical layer --------------------------
            // Layer 0 .. N-2 -> mirror slices (erosion N-1 down to 1)
            // Layer N-1      -> centre slice  (erosion 0, full silhouette)
            // Layer N .. 2N-2-> front slices  (erosion 1 up to N-1)
            for (int layer = 0; layer < totalLayers; layer++)
            {
                int     sliceIdx = Mathf.Abs(layer - (zGridSize - 1));
                bool[,] mask     = masks[sliceIdx];
                float   zCentre  = origin.z + (layer + 0.5f) * voxelSize.z;

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        if (!mask[row, col]) continue;

                        data.Voxels.Add(new Voxel
                        {
                            Col         = col,
                            Row         = row,
                            Slice       = layer,
                            Color       = colourMap[row, col],
                            WorldCenter = new Vector3(
                                origin.x + (col + 0.5f) * voxelSize.x,
                                origin.y + (row + 0.5f) * voxelSize.y,
                                zCentre),
                            HalfSize    = half,
                        });
                    }
                }
            }

            return data;
        }

        // ---------------------------------------------------------------------
        // Safe pixel reader - never modifies import settings
        // ---------------------------------------------------------------------

        /// <summary>
        /// Returns the raw pixel data for <paramref name="tex"/> without
        /// touching its import settings. If the texture's CPU copy is already
        /// available (isReadable) it is used directly; otherwise the file bytes
        /// are read from disk and decoded into a temporary Texture2D.
        /// </summary>
        public static (Color[] pixels, int width, int height)
            ReadPixelsSafe(Texture2D tex, string assetPath)
        {
            if (tex.isReadable)
                return (tex.GetPixels(), tex.width, tex.height);

            // Read raw bytes from disk and decode into a temporary texture.
            // This works for any format Unity can load (PNG, TGA, PSD, ...).
            string fullPath = System.IO.Path.GetFullPath(assetPath);
            byte[] bytes    = System.IO.File.ReadAllBytes(fullPath);

            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tmp.LoadImage(bytes);   // resizes to actual dimensions

            Color[] result = tmp.GetPixels();
            int w = tmp.width, h = tmp.height;
            Object.DestroyImmediate(tmp);

            return (result, w, h);
        }

        // ---------------------------------------------------------------------
        // Internal helpers
        // ---------------------------------------------------------------------

        private static bool[,] BuildBaseMask(Color[,] colourMap, int rows, int cols)
        {
            var mask = new bool[rows, cols];
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                    // A voxel is solid only if its centre pixel is fully opaque.
                    mask[row, col] = ((Color32)colourMap[row, col]).a >= 255;
            return mask;
        }

        /// <summary>
        /// Erodes <paramref name="src"/> inward by one voxel in all four cardinal
        /// XY directions. A cell survives only when it and all four axis-aligned
        /// neighbours are filled; boundary cells are always removed.
        /// </summary>
        private static bool[,] ErodeMask(bool[,] src, int rows, int cols)
        {
            var dst = new bool[rows, cols];
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    if (!src[row, col]) continue;
                    bool up    = row + 1 < rows && src[row + 1, col];
                    bool down  = row - 1 >= 0   && src[row - 1, col];
                    bool right = col + 1 < cols  && src[row, col + 1];
                    bool left  = col - 1 >= 0    && src[row, col - 1];
                    dst[row, col] = up && down && right && left;
                }
            return dst;
        }

        /// <summary>
        /// Returns the colour of the centre pixel of a grid cell.
        /// Using the centre rather than the top-left avoids edge-alignment issues.
        /// No blending is performed - the result is always an exact source pixel.
        /// </summary>
        private static Color PointSampleCell(
            Color[] pixels, int texW,
            int col, int row, int gridSize)
        {
            int x = col * gridSize + gridSize / 2;
            int y = row * gridSize + gridSize / 2;
            return pixels[y * texW + x];
        }

        // Exposed so VoxelMeshExporter can reuse it for the atlas
        internal sealed class Color32EqualityComparer : IEqualityComparer<Color32>
        {
            public static readonly Color32EqualityComparer Instance = new();
            public bool Equals(Color32 x, Color32 y)
                => x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a;
            public int GetHashCode(Color32 c)
                => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
        }
    }
}

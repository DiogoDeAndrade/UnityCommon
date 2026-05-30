// Made using Claude (claude-sonnet-4-6) - https://claude.ai
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace UC
{
    /// <summary>
    /// Builds Unity Mesh assets (saved as .asset) and palette PNG textures from VoxelData.
    ///
    /// ExportWithPalette     - mesh with UVs + palette PNG + optional Material
    /// ExportWithVertexColor - mesh with per-vertex Color32 + optional Material
    /// </summary>
    public static class VoxelMeshExporter
    {
        // ---------------------------------------------------------------------
        // Mode A: palette PNG + UVs
        // ---------------------------------------------------------------------

        /// <param name="generateMaterial">
        /// When true, a Material asset is saved alongside the mesh and texture.
        /// </param>
        public static void ExportWithPalette(
            VoxelData voxels,
            string    baseName,
            string    outputDir,
            bool      generateMaterial)
        {
            // 1. Build UV map from the pre-computed palette
            var (uvMap, atlasWidth, atlasHeight) = BuildUVMap(voxels.Palette);

            // 2. Build mesh
            Mesh mesh = BuildMesh_Palette(voxels, uvMap);
            mesh.name = baseName + "_model";

            // 3. Write palette as a PNG file and import it with Point + Clamp
            string texPath = SavePalettePNG(
                voxels.Palette, atlasWidth, atlasHeight,
                outputDir, baseName);

            // 4. Save mesh asset
            string meshPath = $"{outputDir}/{baseName}_model.asset";
            SaveOrReplaceMesh(mesh, meshPath);

            Debug.Log($"[VoxelExtrude] Saved mesh    -> {meshPath}");
            Debug.Log($"[VoxelExtrude] Saved palette -> {texPath}");

            // 5. Optional material
            if (generateMaterial)
            {
                // AssetDatabase.Refresh so the PNG is importable right away
                AssetDatabase.Refresh();
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                SaveMaterial(baseName, outputDir, tex, isVertexColor: false);
            }
        }

        // ---------------------------------------------------------------------
        // Mode B: vertex colour
        // ---------------------------------------------------------------------

        public static void ExportWithVertexColor(
            VoxelData voxels,
            string    baseName,
            string    outputDir,
            bool      generateMaterial,
            bool      linearToGamma)
        {
            Mesh mesh = BuildMesh_VertexColor(voxels, linearToGamma);
            mesh.name = baseName + "_model";

            string meshPath = $"{outputDir}/{baseName}_model.asset";
            SaveOrReplaceMesh(mesh, meshPath);

            Debug.Log($"[VoxelExtrude] Saved vertex-colour mesh -> {meshPath}");

            if (generateMaterial)
                SaveMaterial(baseName, outputDir, tex: null, isVertexColor: true);
        }

        // ---------------------------------------------------------------------
        // Palette PNG writer
        // ---------------------------------------------------------------------

        /// <summary>
        /// Builds UV lookup from the unique palette list, then writes a minimal
        /// PNG (one texel per colour) to disk and sets its import settings to
        /// Point filter + Clamp wrap + no compression.
        /// Returns the project-relative path of the imported asset.
        /// </summary>
        private static string SavePalettePNG(
            List<Color32> palette,
            int           texW,
            int           texH,
            string        outputDir,
            string        baseName)
        {
            // Build pixel data — layout must match BuildUVMap (row-major, left-to-right)
            var pixels = new Color32[texW * texH];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
            for (int i = 0; i < palette.Count; i++)
            {
                int col = i % texW;
                int row = i / texW;
                pixels[row * texW + col] = palette[i];
            }

            // Encode to PNG via a temporary Texture2D
            var tmp = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
            tmp.SetPixels32(pixels);
            tmp.Apply(false);
            byte[] png = tmp.EncodeToPNG();
            Object.DestroyImmediate(tmp);

            // Write file (project-relative path for AssetDatabase, full path for File I/O)
            string relPath  = $"{outputDir}/{baseName}_texture.png";
            string fullPath = Path.GetFullPath(relPath);
            File.WriteAllBytes(fullPath, png);

            // Import and configure
            AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(relPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType        = TextureImporterType.Default;
                importer.filterMode         = FilterMode.Point;
                importer.wrapMode           = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled      = false;
                // Prevent Unity from silently padding/scaling the texture to
                // a different size, which would shift all texel centres and
                // break UV lookups even with Point filtering.
                importer.npotScale          = TextureImporterNPOTScale.None;
                importer.SaveAndReimport();
            }

            return relPath;
        }

        // ---------------------------------------------------------------------
        // UV map builder (shared between palette writer and mesh builder)
        // ---------------------------------------------------------------------

        private static (Dictionary<Color32, Vector2> uvMap, int texW, int texH)
            BuildUVMap(List<Color32> palette)
        {
            int count = palette.Count;

            // Use a power-of-two square atlas.
            // A 1-row strip sounds compact but Unity's NPOT handling can silently
            // scale or pad the height to 2, which moves texel centres and causes
            // UV bleeding even with Point filtering.  A proper PoT square avoids
            // all of that: texel centres are always at (col+0.5)/W, (row+0.5)/H
            // and nothing gets rescaled on import.
            int side = Mathf.Max(1, Mathf.NextPowerOfTwo(
                            Mathf.CeilToInt(Mathf.Sqrt(count))));
            // Clamp height to the minimum number of rows actually needed
            int texW = side;
            int texH = Mathf.NextPowerOfTwo(Mathf.CeilToInt((float)count / texW));
            texH = Mathf.Max(texH, 1);

            var uvMap = new Dictionary<Color32, Vector2>(
                VoxelBuilder.Color32EqualityComparer.Instance);

            for (int i = 0; i < palette.Count; i++)
            {
                int col = i % texW;
                int row = i / texW;
                uvMap[palette[i]] = new Vector2(
                    (col + 0.5f) / texW,
                    (row + 0.5f) / texH);
            }

            return (uvMap, texW, texH);
        }

        // ---------------------------------------------------------------------
        // Mesh builders
        // ---------------------------------------------------------------------

        private static Mesh BuildMesh_Palette(
            VoxelData                    voxels,
            Dictionary<Color32, Vector2> uvMap)
        {
            var verts   = new List<Vector3>();
            var norms   = new List<Vector3>();
            var uvs     = new List<Vector2>();
            var indices = new List<int>();

            foreach (var voxel in voxels.Voxels)
            {
                var uv = uvMap.TryGetValue((Color32)voxel.Color, out var found)
                    ? found : Vector2.zero;
                AppendBox(verts, norms, uvs, null, indices, voxel, uv);
            }

            return FinaliseGeneric(verts, norms, uvs, null, indices);
        }

        private static Mesh BuildMesh_VertexColor(VoxelData voxels, bool linearToGamma)
        {
            var verts   = new List<Vector3>();
            var norms   = new List<Vector3>();
            var colors  = new List<Color32>();
            var indices = new List<int>();

            foreach (var voxel in voxels.Voxels)
            {
                // GetPixels() always returns linear-space values because Unity
                // converts sRGB->linear on load.  Vertex colours are passed
                // straight to the shader without any colour-space conversion,
                // unlike texture samples which are linearised at sample time.
                // When the source PNG is sRGB we therefore need to convert back
                // to gamma/sRGB space so the shader sees the original perceived
                // colour rather than a darkened linear value.
                Color c = linearToGamma
                    ? voxel.Color.linear   // linear -> sRGB (gamma 2.2 approx)
                    : voxel.Color;
                AppendBox(verts, norms, null, colors, indices,
                          voxel, Vector2.zero, (Color32)c);
            }

            return FinaliseGeneric(verts, norms, null, colors, indices);
        }

        // ---------------------------------------------------------------------
        // Box appender
        // ---------------------------------------------------------------------

        private static readonly Vector3[] s_FaceNormals =
        {
            Vector3.right,   Vector3.left,
            Vector3.up,      Vector3.down,
            Vector3.forward, Vector3.back,
        };

        private static readonly Vector3[][] s_FaceCorners =
        {
            // +X
            new[]{ new Vector3( 1,-1,-1), new Vector3( 1, 1,-1),
                   new Vector3( 1, 1, 1), new Vector3( 1,-1, 1) },
            // -X
            new[]{ new Vector3(-1,-1, 1), new Vector3(-1, 1, 1),
                   new Vector3(-1, 1,-1), new Vector3(-1,-1,-1) },
            // +Y
            new[]{ new Vector3(-1, 1,-1), new Vector3(-1, 1, 1),
                   new Vector3( 1, 1, 1), new Vector3( 1, 1,-1) },
            // -Y
            new[]{ new Vector3(-1,-1, 1), new Vector3(-1,-1,-1),
                   new Vector3( 1,-1,-1), new Vector3( 1,-1, 1) },
            // +Z
            new[]{ new Vector3(-1,-1, 1), new Vector3( 1,-1, 1),
                   new Vector3( 1, 1, 1), new Vector3(-1, 1, 1) },
            // -Z
            new[]{ new Vector3( 1,-1,-1), new Vector3(-1,-1,-1),
                   new Vector3(-1, 1,-1), new Vector3( 1, 1,-1) },
        };

        private static void AppendBox(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int>     indices,
            Voxel         voxel,
            Vector2       uv,
            Color32       color32 = default)
        {
            Vector3 c = voxel.WorldCenter;
            Vector3 h = voxel.HalfSize;

            for (int face = 0; face < 6; face++)
            {
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

        // ---------------------------------------------------------------------
        // Mesh finalisation
        // ---------------------------------------------------------------------

        private static Mesh FinaliseGeneric(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Vector2> uvs,
            List<Color32> colors,
            List<int>     indices)
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

        // ---------------------------------------------------------------------
        // Material generator
        // ---------------------------------------------------------------------

        private static void SaveMaterial(
            string    baseName,
            string    outputDir,
            Texture2D tex,
            bool      isVertexColor)
        {
            // Try URP lit first, fall back to built-in Standard
            Shader shader = isVertexColor
                ? (Shader.Find("Universal Render Pipeline/Particles/Lit")
                   ?? Shader.Find("Particles/Standard Lit")
                   ?? Shader.Find("Standard"))
                : (Shader.Find("Universal Render Pipeline/Lit")
                   ?? Shader.Find("Standard"));

            if (shader == null)
            {
                Debug.LogWarning("[VoxelExtrude] Could not find a suitable shader for the material.");
                return;
            }

            var mat = new Material(shader);
            mat.name = baseName + "_material";

            if (tex != null)
            {
                // URP uses _BaseMap; built-in Standard uses _MainTex
                if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap",  tex);
                if (mat.HasProperty("_MainTex"))  mat.SetTexture("_MainTex",  tex);
                // Ensure no smoothness / metallic tint on palette mats
                if (mat.HasProperty("_Smoothness"))  mat.SetFloat("_Smoothness", 0f);
                if (mat.HasProperty("_Glossiness"))  mat.SetFloat("_Glossiness", 0f);
                if (mat.HasProperty("_Metallic"))    mat.SetFloat("_Metallic",   0f);
            }

            string matPath = $"{outputDir}/{baseName}_material.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mat, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(mat, matPath);
            }

            Debug.Log($"[VoxelExtrude] Saved material -> {matPath}");
        }

        // ---------------------------------------------------------------------
        // Asset I/O
        // ---------------------------------------------------------------------

        private static void SaveOrReplaceMesh(Mesh newMesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(newMesh, existing);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(newMesh, path);
            }
        }
    }
}

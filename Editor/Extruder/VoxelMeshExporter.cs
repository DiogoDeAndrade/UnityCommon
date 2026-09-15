// Made using Claude (claude-sonnet-4-6) - https://claude.ai
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace UC
{
    /// <summary>
    /// Builds Unity Mesh assets (saved as .asset) and palette PNG textures from VoxelData.
    /// Mesh geometry itself comes from the runtime VoxelMeshBuilder; this class only handles asset I/O.
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
            var (uvMap, atlasWidth, atlasHeight) = VoxelMeshBuilder.BuildUVMap(voxels.Palette);

            // 2. Build mesh
            Mesh mesh = VoxelMeshBuilder.BuildMeshPalette(voxels, uvMap);
            mesh.name = baseName + "_model";

            // 3. Write palette as a PNG file and import it with Point + Clamp
            string texPath = SavePalettePNG(
                voxels.Palette, atlasWidth, atlasHeight,
                outputDir, baseName);

            // 4. Save mesh asset
            string meshPath = $"{outputDir}/{baseName}_model.asset";
            SaveOrReplaceMesh(mesh, meshPath);

            DebugHelpers.Log($"[VoxelExtrude] Saved mesh    -> {meshPath}");
            DebugHelpers.Log($"[VoxelExtrude] Saved palette -> {texPath}");

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
            Mesh mesh = VoxelMeshBuilder.BuildMeshVertexColor(voxels, linearToGamma);
            mesh.name = baseName + "_model";

            string meshPath = $"{outputDir}/{baseName}_model.asset";
            SaveOrReplaceMesh(mesh, meshPath);

            DebugHelpers.Log($"[VoxelExtrude] Saved vertex-colour mesh -> {meshPath}");

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
            // Build pixel data — layout must match VoxelMeshBuilder.BuildUVMap (row-major, left-to-right)
            var pixels = VoxelMeshBuilder.BuildPalettePixels(palette, texW, texH);

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

        // Mesh construction (UV map, box faces, finalisation) lives in the runtime VoxelMeshBuilder.

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
                DebugHelpers.LogWarning("[VoxelExtrude] Could not find a suitable shader for the material.");
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

            DebugHelpers.Log($"[VoxelExtrude] Saved material -> {matPath}");
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

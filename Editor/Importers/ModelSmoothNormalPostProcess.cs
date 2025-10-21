// Assets/Editor/ModelSmoothNormalPostProcess.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UC
{

    public class ModelSmoothNormalPostProcess : AssetPostprocessor
    {
        // FBX/OBJ/DAE/etc. -> ModelImporter path
        void OnPostprocessModel(GameObject root)
        {
            var importer = (AssetImporter)assetImporter; // ModelImporter here
            var settings = SmoothNormalsUtil.LoadSettings(importer);
            if (!settings.generateSmoothNormals) return;

            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                SmoothNormalsUtil.WriteSmoothNormalsToUV(mf.sharedMesh, settings);

            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                SmoothNormalsUtil.WriteSmoothNormalsToUV(smr.sharedMesh, settings);
        }

        // GLB/GLTF (ScriptedImporter) -> global callback
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
            {
                var lower = path.ToLowerInvariant();
                if (!(lower.EndsWith(".glb") || lower.EndsWith(".gltf"))) continue;

                var imp = AssetImporter.GetAtPath(path);
                if (imp == null) continue;

                var settings = SmoothNormalsUtil.LoadSettings(imp);
                if (!settings.generateSmoothNormals) continue;

                // Touch meshes produced by the scripted importer (sub-assets at same path)
                var all = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var m in all.OfType<Mesh>())
                {
                    SmoothNormalsUtil.WriteSmoothNormalsToUV(m, settings);
                    EditorUtility.SetDirty(m);
                }
                AssetDatabase.SaveAssets();
            }
        }
    }

    public static class SmoothNormalsUtil
    {
        [Serializable]
        public class Settings
        {
            public enum UVChannel { Channel0 = 0, Channel1 = 1, Channel2 = 2, Channel3 = 3, Channel4 = 4, Channel5 = 5, Channel6 = 6, Channel7 = 7 }

            public bool generateSmoothNormals = false;
            public UVChannel uvChannel = UVChannel.Channel7;
        }

        public static Settings LoadSettings(AssetImporter importer)
        {
            if (importer == null || string.IsNullOrEmpty(importer.userData)) return new Settings();
            try { return JsonUtility.FromJson<Settings>(importer.userData) ?? new Settings(); }
            catch { return new Settings(); }
        }

        public static void SaveSettings(AssetImporter importer, Settings s)
        {
            importer.userData = JsonUtility.ToJson(s);
            AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
        }

        // Deterministic “average by position” (with quantization to avoid float jitter)
        public static void WriteSmoothNormalsToUV(Mesh mesh, Settings s)
        {
            if (!mesh) return;

            var verts = mesh.vertices;
            var norms = mesh.normals;
            if (verts == null || norms == null || norms.Length != verts.Length) return;

            // Quantize keys to avoid tiny differences across verification passes
            const int SCALE = 100000; // ~1e-5 unit
            int Q(float v) => Mathf.RoundToInt(v * SCALE);

            var acc = new Dictionary<(int, int, int), Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                var key = (Q(v.x), Q(v.y), Q(v.z));
                if (acc.TryGetValue(key, out var sum)) acc[key] = sum + norms[i];
                else acc[key] = norms[i];
            }
            // normalize accumulated vectors
            var keys = new List<(int, int, int)>(acc.Keys);
            foreach (var k in keys) acc[k] = acc[k].normalized;

            var outUV = new List<Vector3>(verts.Length);
            outUV.Clear();
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                var key = (Q(v.x), Q(v.y), Q(v.z));
                outUV.Add(acc[key]);
            }

            mesh.SetUVs((int)s.uvChannel, outUV);

            // IMPORTANT: don’t UploadMeshData() here; let importer manage Read/Write.
        }
    }
}
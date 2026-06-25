// Thin wrapper over the Autodesk FBX Exporter (com.unity.formats.fbx) so the rest of the project
// can export a GameObject hierarchy to an .fbx without referencing the package directly.
//
// Gated by the UC_FBX_LIBRARY_AVAILABLE scripting define. To enable:
//   1. Window > Package Manager > install "FBX Exporter" (id: com.unity.formats.fbx)
//   2. Project Settings > Player > Scripting Define Symbols: add UC_FBX_LIBRARY_AVAILABLE
// Without the define this still compiles, but Export() reports the library is missing and
// IsAvailable returns false (so callers can degrade gracefully).

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

#if UC_FBX_LIBRARY_AVAILABLE
using UnityEditor.Formats.Fbx.Exporter;
#endif

namespace UC
{
    /// <summary>Configurable FBX exporter. Create one, set options, call <see cref="Export"/>.</summary>
    public class FBXExporter
    {
        /// <summary>True when the FBX library is present (UC_FBX_LIBRARY_AVAILABLE defined + installed).</summary>
        public static bool IsAvailable
        {
            get
            {
#if UC_FBX_LIBRARY_AVAILABLE
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>Create the destination folder if it doesn't exist. Default true.</summary>
        public bool CreateDirectory = true;

        /// <summary>Import the produced file into the AssetDatabase after exporting. Default true.</summary>
        public bool ImportAfterExport = true;

        /// <summary>
        /// Exports the hierarchy rooted at <paramref name="root"/> to an FBX at
        /// <paramref name="path"/> (project-relative, e.g. "Assets/Foo/bar.fbx", or absolute).
        /// Returns false with a reason in <paramref name="error"/> on failure.
        /// </summary>
        public bool Export(GameObject root, string path, out string error)
        {
            error = null;
            if (root == null) { error = "FBXExporter.Export: root GameObject is null."; return false; }
            if (string.IsNullOrEmpty(path)) { error = "FBXExporter.Export: path is empty."; return false; }

#if UC_FBX_LIBRARY_AVAILABLE
            if (CreateDirectory)
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(path));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }

            ExportModelOptions options = new();
            options.ExportFormat = ExportFormat.Binary;

            string exported = ModelExporter.ExportObject(path, root, options);
            if (string.IsNullOrEmpty(exported))
            {
                error = $"FBXExporter.Export: the FBX exporter produced no file for '{path}'.";
                return false;
            }

            if (ImportAfterExport && path.Replace('\\', '/').StartsWith("Assets/"))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return true;
#else
            error = "FBX library not available. Install the 'FBX Exporter' package " +
                    "(com.unity.formats.fbx) and add UC_FBX_LIBRARY_AVAILABLE to " +
                    "Project Settings > Player > Scripting Define Symbols.";
            return false;
#endif
        }
    }
}
#endif

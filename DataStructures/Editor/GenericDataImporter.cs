using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UC
{
    // Imports what "Export data" writes (see GenericSerializer): the file becomes the ScriptableObject it describes.
    [ScriptedImporter(1, "data")]
    public class GenericDataImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            object data = IGenericSerialization.GenericDeserialize(File.ReadAllText(ctx.assetPath), (reference, type) =>
            {
                GenericSerializer.SplitAssetReference(reference, out string assetPath, out _);

                // A file can't wait for its own import, and what's at its path right now (if anything) is what this import is about to replace
                if (assetPath == ctx.assetPath) return null;

                DependOn(ctx, assetPath);

                return GenericSerializer.LoadAssetReference(reference, type);
            }, ctx.assetPath);

            if (data is ScriptableObject scriptableObject)
            {
                ctx.AddObjectToAsset("main", scriptableObject);
                ctx.SetMainObject(scriptableObject);
            }
            else if (data != null)
            {
                ctx.LogImportError($"{ctx.assetPath} describes a {data.GetType().FullName}, and only a ScriptableObject can be an asset");
            }
        }

        // Whatever the object points to has to be imported before it is, and this has to be imported
        // again when that changes: that's the artifact. The source is what covers a file that isn't
        // there yet, so that the reference gets picked up when it shows up.
        static void DependOn(AssetImportContext ctx, string assetPath)
        {
            // The built-in resources ("Resources/unity_builtin_extra" and such) load like any other
            // asset, but there's nothing to depend on there, and trying to sends the asset database
            // into an import loop.
            if ((!assetPath.StartsWith("Assets/")) && (!assetPath.StartsWith("Packages/"))) return;

            ctx.DependsOnArtifact(assetPath);
            ctx.DependsOnSourceAsset(assetPath);
        }
    }
}

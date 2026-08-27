using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;

namespace UC
{

    [ScriptedImporter(3, "dialogue")] // 3 is the version, "dialogue" is the file extension
    public class DialogueImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var dialogueData = DialogueData.Import(ctx.assetPath);

            if (dialogueData != null)
            {
                ResolveIncludes(ctx, dialogueData);
            }

            // Add the ScriptableObject to the import context
            ctx.AddObjectToAsset("Dialogues", dialogueData);
            ctx.SetMainObject(dialogueData);

            string findKey = $"t:DialogueData";
            string[] guids = AssetDatabase.FindAssets(findKey);
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
                Resources.UnloadAsset(asset);
            }
        }

        // Turns each 'include("Name")' into a hard reference to the other imported DialogueData, so
        // builds pull the file in and runtime never has to search for it. Same folder wins over the
        // rest of the project when the name exists in both.
        //
        // The other file may not have been imported yet (fresh project, import order, include
        // cycles) - then the include stays name-only with a warning, the editor falls back to
        // searching at runtime, and "Unity Common/Dialogue/Update References" reimports everything
        // until it all resolves.
        void ResolveIncludes(AssetImportContext ctx, DialogueData dialogueData)
        {
            var includeNames = dialogueData.IncludeNames;
            if ((includeNames == null) || (includeNames.Count == 0)) return;

            string folder = Path.GetDirectoryName(ctx.assetPath).Replace('\\', '/');

            for (int i = 0; i < includeNames.Count; i++)
            {
                string includePath = FindIncludePath(folder, includeNames[i]);

                if (includePath == null)
                {
                    Debug.LogWarning($"Include \"{includeNames[i]}\" in {ctx.assetPath} not found - keeping a soft reference. Run Unity Common/Dialogue/Update References once the file exists.");
                    continue;
                }

                // Reimport when the included source changes or disappears, so a rename/delete
                // surfaces here instead of at runtime
                ctx.DependsOnSourceAsset(includePath);

                var include = AssetDatabase.LoadAssetAtPath<DialogueData>(includePath);
                if (include == null)
                {
                    // The file exists but its artifact isn't available yet (import order) - the
                    // runtime editor fallback and Update References cover this
                    Debug.LogWarning($"Include \"{includeNames[i]}\" in {ctx.assetPath} isn't imported yet - run Unity Common/Dialogue/Update References.");
                    continue;
                }

                dialogueData.SetIncludeRef(i, include);
            }
        }

        // A bare name searches the importing file's folder first, then the whole project; the name
        // has to match the file name (without extension) exactly
        static string FindIncludePath(string folder, string includeName)
        {
            string sameFolder = $"{folder}/{includeName}.dialogue";
            if (File.Exists(sameFolder)) return sameFolder;

            foreach (var guid in AssetDatabase.FindAssets($"{includeName} t:DialogueData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == includeName) return path;
            }

            return null;
        }
    }
}

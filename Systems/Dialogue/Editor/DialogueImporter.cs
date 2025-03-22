using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.Linq;

[ScriptedImporter(1, "dialogue")] // 1 is the version, "dialogue" is the file extension 
public class DialogueImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var dialogueData = DialogueData.Import(ctx.assetPath);

        // Add the ScriptableObject to the import context
        ctx.AddObjectToAsset("Dialogues", dialogueData);
        ctx.SetMainObject(dialogueData);

        string findKey = $"t:DialogueData";
        string[] guids = AssetDatabase.FindAssets(findKey);
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<DialogueData>(guid);
            Resources.UnloadAsset(asset);
        }
    }
}

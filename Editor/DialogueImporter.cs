using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System;

[ScriptedImporter(1, "dialogue")] // 1 is the version, "dialogue" is the file extension 
public class DialogueImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var dialogueData = DialogueData.Import(ctx.assetPath);

        // Add the ScriptableObject to the import context
        ctx.AddObjectToAsset("Dialogues", dialogueData);
        ctx.SetMainObject(dialogueData);
    }
}

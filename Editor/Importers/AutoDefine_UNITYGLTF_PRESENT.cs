using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using static PlasticGui.LaunchDiffParameters;

[InitializeOnLoad]
public static class OptionalGltfDefine
{
    private const string SYMBOL = "UNITYGLTF_PRESENT";

    static OptionalGltfDefine()
    {
        UpdateDefineForActiveTarget();
    }

    private static void UpdateDefineForActiveTarget()
    {
        bool hasGltf = AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetName().Name.IndexOf("UnityGLTF", StringComparison.OrdinalIgnoreCase) >= 0);

        var nbt = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);

        // Get current defines (new API first)
        string[] definesArr;
#if UNITY_2021_2_OR_NEWER
        UnityEditor.PlayerSettings.GetScriptingDefineSymbols(nbt, out definesArr);
#else
        var definesStringOld = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        definesArr = definesStringOld.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
#endif
        var set = new HashSet<string>(definesArr);

        bool changed = false;
        if (hasGltf) changed = set.Add(SYMBOL) || changed;
        else changed = set.Remove(SYMBOL) || changed;

        if (!changed) return;

        var newDefines = string.Join(";", set);

        // Set defines (new API first)
#if UNITY_2021_2_OR_NEWER
        UnityEditor.PlayerSettings.SetScriptingDefineSymbols(nbt, newDefines);
#else
        UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(
            EditorUserBuildSettings.selectedBuildTargetGroup, newDefines);
#endif
    }
}

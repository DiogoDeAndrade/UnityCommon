using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;

[InitializeOnLoad]
public static class OptionalSplineDefine
{
    private const string define = "UNITYSPLINE_PRESENT";
    const string packageName = "com.unity.splines";

    static OptionalSplineDefine()
    {
        UpdateDefine();
        Events.registeredPackages += OnPackagesChanged;
    }

    static void OnPackagesChanged(PackageRegistrationEventArgs args)
    {
        UpdateDefine();
    }

    static void UpdateDefine()
    {
        bool hasSplines = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + packageName) != null;

        foreach (BuildTargetGroup group in System.Enum.GetValues(typeof(BuildTargetGroup)))
        {
            if (group == BuildTargetGroup.Unknown)
                continue;

            // Get current defines (new API first)
            var nbt = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            string[] definesArr;
#if UNITY_2021_2_OR_NEWER
            UnityEditor.PlayerSettings.GetScriptingDefineSymbols(nbt, out definesArr);
#else
            var definesStringOld = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            definesArr = definesStringOld.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
#endif

            var set = new HashSet<string>(definesArr);

            bool changed = false;
            if (hasSplines) changed = set.Add(define) || changed;
            else changed = set.Remove(define) || changed;

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
}

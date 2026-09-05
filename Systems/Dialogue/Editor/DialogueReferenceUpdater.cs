using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace UC
{
    // "Unity Common/Dialogue/Update References": reimports every .dialogue file so all their
    // include("...") references get baked into hard asset links, then validates the whole set -
    // unresolved includes, dialogue keys that don't exist anywhere, keys that only resolve through
    // the DialogueManager's global list, and duplicate keys across files. Run it after adding,
    // renaming or moving dialogue files, and before making a build.
    public static class DialogueReferenceUpdater
    {
        static readonly Regex historyTargetRegex = new(@"^History\(\s*(-?\d+)\s*\)$");
        static readonly Regex markerTargetRegex = new(@"^Marker\(\s*([A-Za-z0-9:_-]+)\s*\)$");

        [MenuItem("Unity Common/Dialogue/Update References")]
        public static void UpdateReferences()
        {
            var paths = AssetDatabase.FindAssets("t:DialogueData")
                                     .Select(AssetDatabase.GUIDToAssetPath)
                                     .Distinct()
                                     .ToList();

            // First round makes sure every file has an up-to-date artifact; the second gives files
            // whose includes weren't imported yet at the time another chance to bake the reference
            for (int round = 0; round < 2; round++)
            {
                bool anyUnresolved = false;

                foreach (var path in paths)
                {
                    var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
                    bool unresolved = (data == null) || HasUnresolvedIncludes(data);

                    if ((round == 0) || unresolved)
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                        anyUnresolved |= unresolved;
                    }
                }

                if ((round == 0) && (!anyUnresolved)) break;
            }

            Validate(paths);
        }

        static bool HasUnresolvedIncludes(DialogueData data)
        {
            for (int i = 0; i < data.IncludeNames.Count; i++)
            {
                if (data.IncludeRefs[i] == null) return true;
            }
            return false;
        }

        static void Validate(List<string> paths)
        {
            var all = new List<(string path, DialogueData data)>();
            foreach (var path in paths)
            {
                var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
                if (data == null)
                {
                    DebugHelpers.LogError($"Dialogue file failed to import: {path}");
                    continue;
                }
                all.Add((path, data));
            }

            int errors = 0;
            int warnings = 0;

            // Every key in the project, to tell "doesn't exist" apart from "exists somewhere this
            // file can't see" - and to find duplicates
            var keyOwners = new Dictionary<string, List<string>>();
            foreach (var (path, data) in all)
            {
                foreach (var dialogue in data.GetAllDialogues())
                {
                    if (!keyOwners.TryGetValue(dialogue.name, out var owners))
                    {
                        keyOwners[dialogue.name] = owners = new List<string>();
                    }
                    owners.Add(path);
                }
            }

            foreach (var (key, owners) in keyOwners)
            {
                if (owners.Count > 1)
                {
                    DebugHelpers.LogWarning($"Dialogue key \"{key}\" is defined in more than one file ({string.Join(", ", owners)}) - lookups will silently take the first one found!");
                    warnings++;
                }

                if (key == "End")
                {
                    DebugHelpers.LogWarning($"A dialogue is named \"End\" ({string.Join(", ", owners)}) - that name is reserved for the \"-> End\" target and the node can never be reached!");
                    warnings++;
                }
            }

            foreach (var (path, data) in all)
            {
                // Includes: after the reimport rounds, anything still null is really missing
                for (int i = 0; i < data.IncludeNames.Count; i++)
                {
                    if (data.IncludeRefs[i] == null)
                    {
                        DebugHelpers.LogError($"{path}: include \"{data.IncludeNames[i]}\" can't be resolved - no .dialogue file with that name exists!");
                        errors++;
                    }
                }

                // Every jump target in the file has to lead somewhere
                foreach (var dialogue in data.GetAllDialogues())
                {
                    foreach (var elem in dialogue.elems)
                    {
                        if (elem.options == null) continue;
                        foreach (var option in elem.options)
                        {
                            if (CheckTarget(data, path, dialogue.name, option.key, keyOwners, ref warnings)) continue;
                            errors++;
                        }
                    }

                    if (dialogue.conditionalNext == null) continue;
                    foreach (var next in dialogue.conditionalNext)
                    {
                        var target = next.nextKey?.nextKey;
                        if (string.IsNullOrEmpty(target)) continue;  // a code entry, not a jump

                        if (CheckTarget(data, path, dialogue.name, target, keyOwners, ref warnings)) continue;
                        errors++;
                    }
                }
            }

            if (errors > 0)
                DebugHelpers.LogError($"Dialogue references: {errors} error(s), {warnings} warning(s) in {all.Count} file(s). See above.");
            else
                DebugHelpers.Log($"Dialogue references OK: {all.Count} file(s) checked, {warnings} warning(s).");
        }

        // True when the target is fine (or merely warned about); false is an error the caller counts
        static bool CheckTarget(DialogueData data, string path, string fromKey, string target, Dictionary<string, List<string>> keyOwners, ref int warnings)
        {
            // The reserved "end the conversation" target
            if (target == "End") return true;

            // History(...) only makes sense at runtime; the only thing checkable here is the sign
            var historyMatch = historyTargetRegex.Match(target);
            if (historyMatch.Success)
            {
                if (int.Parse(historyMatch.Groups[1].Value) > 0)
                {
                    DebugHelpers.LogWarning($"{path}: \"{fromKey}\" jumps to {target} - history offsets are zero or negative (History(-1) goes back one)!");
                    warnings++;
                }
                return true;
            }

            // Marker names can be set by whatever file includes this one, so existence can't be
            // checked statically
            if (markerTargetRegex.IsMatch(target)) return true;

            var (_, dialogue) = data.FindDialogueInHierarchy(target);
            if (dialogue != null) return true;

            if (keyOwners.TryGetValue(target, out var owners))
            {
                DebugHelpers.LogWarning($"{path}: \"{fromKey}\" jumps to \"{target}\", which only exists in {string.Join(", ", owners)} - that works only if the file is registered in the DialogueManager's global list. Consider an include(\"...\").");
                warnings++;
                return true;
            }

            DebugHelpers.LogError($"{path}: \"{fromKey}\" jumps to \"{target}\", which doesn't exist anywhere!");
            return false;
        }
    }
}

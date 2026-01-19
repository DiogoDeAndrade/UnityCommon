using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;

namespace UC
{

    public class DebugVertexAttributeShaderGUI : ShaderGUI
    {
        // ---------------------------
        //  EDIT THESE TO MATCH YOUR GRAPH
        // ---------------------------

        // Float / Int properties (Blackboard "Reference" names)
        // Example: UV Channel is a Float in your screenshot.
        private const string PROP_UV_CHANNEL = "_UV_Channel";
        private const string PROP_SCALE = "_Scale";
        private const string PROP_OFFSET = "_Offset";

        // If you also have a float/int named "Mode", put it here; otherwise remove.
        // (In your screenshot "Mode" is also an enum keyword, so ignore this unless you have a numeric Mode.)
        // private const string PROP_SOME_FLOAT_MODE = "_SomeFloatMode";

        // Enum Keyword "Reference" names in Shader Graph (NOT display names).
        // In SG Blackboard: click the keyword -> in the right panel you'll see "Reference".
        private const string KWREF_DEBUGMODE = "DEBUGMODE";
        private const string KWREF_SPACE = "SPACE";
        private const string KWREF_MODE = "MODE";
        private const string KWREF_SWIZZLE = "SWIZZLE";

        // Options for DebugMode keyword (must match the *Keyword option names* in Shader Graph)
        // If your options are named differently (e.g., "Uv" instead of "UV"), change them here.
        private static readonly string[] DebugModeOptions = { "Normal", "Binormal", "Tangent", "Color", "UV" };

        // Options for Space keyword (change to match your SG options)
        // Common ones would be: Object, World, View, Tangent, etc.
        private static readonly string[] SpaceOptions = { "Local", "World", "View", "Tangent" };

        // Options for Mode keyword if you want to show it always (change to match your SG)
        private static readonly string[] ModeOptions = { "None", "Clamp", "Repeat" };

        // Options for Swizzle keyword if you want to show it always (change to match your SG)
        private static readonly string[] SwizzleOptions = { "XYZW", "XXXX", "YYYY", "ZZZZ", "WWWW" };

        // Display labels
        private static readonly GUIContent GC_DebugMode = new GUIContent("Debug Mode");
        private static readonly GUIContent GC_UVChannel = new GUIContent("UV Channel");
        private static readonly GUIContent GC_Space = new GUIContent("Space");
        private static readonly GUIContent GC_Mode = new GUIContent("Mode");
        private static readonly GUIContent GC_Swizzle = new GUIContent("Swizzle");
        private static readonly GUIContent GC_Scale = new GUIContent("Scale");
        private static readonly GUIContent GC_Offset = new GUIContent("Offset");

        // ---------------------------
        //  Unity entry point
        // ---------------------------
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            // Multi-object safe: work off the first material for reading state,
            // but apply changes to all selected materials.
            var materials = new Material[materialEditor.targets.Length];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = materialEditor.targets[i] as Material;

            if (materials.Length == 0 || materials[0] == null)
                return;

            var mat0 = materials[0];
            var shader = mat0.shader;

            var pDebugMode = FindPropertySafe("DEBUGMODE", props);
            var pSpace = FindPropertySafe("SPACE", props);
            var pMode = FindPropertySafe("MODE", props);
            var pSwizzle = FindPropertySafe("SWIZZLE", props);

            // Resolve LocalKeywords (Unity local keyword system)
            var kwDebug = ResolveEnumKeyword(shader, KWREF_DEBUGMODE, DebugModeOptions);
            var kwSpace = ResolveEnumKeyword(shader, KWREF_SPACE, SpaceOptions);
            var kwMode = ResolveEnumKeyword(shader, KWREF_MODE, ModeOptions);
            var kwSwizzle = ResolveEnumKeyword(shader, KWREF_SWIZZLE, SwizzleOptions);

            // Find float props
            var pUVChannel = FindPropertySafe(PROP_UV_CHANNEL, props);
            var pScale = FindPropertySafe(PROP_SCALE, props);
            var pOffset = FindPropertySafe(PROP_OFFSET, props);

            // Header
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                // --- DebugMode (enum keyword) ---
                int debugIndex = DrawEnumIntPopup(materialEditor, GC_DebugMode, pDebugMode, DebugModeOptions);

                bool isUV = debugIndex == IndexOf(DebugModeOptions, "UV");
                bool showSpace =
                    debugIndex == IndexOf(DebugModeOptions, "Normal") ||
                    debugIndex == IndexOf(DebugModeOptions, "Binormal") ||
                    debugIndex == IndexOf(DebugModeOptions, "Tangent");

                EditorGUILayout.Space(6);

                // --- Conditional: UV Channel (float) ---

                if (isUV && pUVChannel != null)
                {
                    // Treat underlying float as int
                    int current = Mathf.RoundToInt(pUVChannel.floatValue);

                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUILayout.IntSlider(GC_UVChannel, current, 0, 7); // or 0..3
                    if (EditorGUI.EndChangeCheck())
                    {
                        materialEditor.RegisterPropertyChangeUndo(GC_UVChannel.text);
                        pUVChannel.floatValue = next;

                        materialEditor.PropertiesChanged();
                    }
                }

                // --- Conditional: Space (enum keyword) ---
                if (showSpace && pSpace != null)
                    DrawEnumIntPopup(materialEditor, GC_Space, pSpace, SpaceOptions);

                EditorGUILayout.Space(6);

                // --- Optional: Mode (enum keyword) ---
                if (pMode != null)
                    DrawEnumIntPopup(materialEditor, GC_Mode, pMode, ModeOptions);

                if (pSwizzle != null)
                    DrawEnumIntPopup(materialEditor, GC_Swizzle, pSwizzle, SwizzleOptions);

                // --- Always visible floats ---
                if (pScale != null) materialEditor.ShaderProperty(pScale, GC_Scale);
                if (pOffset != null) materialEditor.ShaderProperty(pOffset, GC_Offset);
            }

            // If you want the rest of the graph's exposed properties to appear too, you have two choices:
            // 1) Draw them manually (recommended if you want strict control)
            // 2) Or call base.OnGUI(...) AFTER, but then you'll get duplicates for Scale/Offset/UVChannel.
            //
            // Most people go with manual control: add more FindPropertySafe + ShaderProperty lines as needed.
        }

        // ---------------------------
        //  Helpers
        // ---------------------------

        private class EnumKeyword
        {
            public readonly LocalKeyword[] Keywords;   // one per option
            public readonly string[] OptionNames;      // same length
            public EnumKeyword(LocalKeyword[] kws, string[] names) { Keywords = kws; OptionNames = names; }
        }

        private static int DrawEnumIntPopup(MaterialEditor materialEditor, GUIContent label, MaterialProperty prop, string[] optionNames)
        {
            if (prop == null) return 0;

            int current = Mathf.Clamp(Mathf.RoundToInt(prop.floatValue), 0, optionNames.Length - 1);

            EditorGUI.showMixedValue = prop.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup(label, current, optionNames);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(label.text);

                // Persisted state
                prop.floatValue = next;

                // Immediate update path: force Unity to re-apply keyword enums now
                foreach (var t in materialEditor.targets)
                {
                    if (t is not Material mat) continue;

                    // This is the key call for [KeywordEnum] properties:
                    // it re-evaluates material property drawers and updates keywords/variants immediately.
                    MaterialEditor.ApplyMaterialPropertyDrawers(mat);

                    EditorUtility.SetDirty(mat);
                }

                // Refresh preview / shader pass state
                materialEditor.PropertiesChanged();

                // Sometimes needed to refresh the Inspector UI immediately
                // (safe; won't spam)
                InternalEditorUtility.RepaintAllViews();
            }
            EditorGUI.showMixedValue = false;

            return next;
        }

        private static EnumKeyword ResolveEnumKeyword(Shader shader, string keywordReference, string[] optionNames)
        {
            if (shader == null || string.IsNullOrEmpty(keywordReference) || optionNames == null || optionNames.Length == 0)
                return null;

            var space = shader.keywordSpace;

            string refToken = SanitizeToken(keywordReference);
            string refTokenWithUnderscore = "_" + refToken;

            var kws = new LocalKeyword[optionNames.Length];
            bool anyFound = false;

            for (int i = 0; i < optionNames.Length; i++)
            {
                string optToken = SanitizeToken(optionNames[i]);

                // Try a bunch of common ShaderGraph patterns
                string[] candidates =
                {
            $"{refToken}_{optToken}",
            $"{refTokenWithUnderscore}_{optToken}",
            $"_{refToken}_{optToken}",
            $"__{refToken}_{optToken}",
        };

                LocalKeyword found = default;
                bool ok = false;

                for (int c = 0; c < candidates.Length; c++)
                {
                    if (TryFindLocalKeyword(space, candidates[c], out found))
                    {
                        ok = true;
                        break;
                    }
                }

                kws[i] = ok ? found : default;
                anyFound |= ok;
            }

            return anyFound ? new EnumKeyword(kws, optionNames) : null;
        }

        private static string SanitizeToken(string s)
        {
            // ShaderGraph typically uppercases and replaces non [A-Z0-9] with underscores
            if (string.IsNullOrEmpty(s)) return "";

            s = s.Trim().ToUpperInvariant();

            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                bool ok = (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
                chars[i] = ok ? ch : '_';
            }

            // Collapse multiple underscores
            var result = new System.Text.StringBuilder(chars.Length);
            bool lastUnderscore = false;
            for (int i = 0; i < chars.Length; i++)
            {
                bool isUnderscore = chars[i] == '_';
                if (isUnderscore && lastUnderscore) continue;
                result.Append(chars[i]);
                lastUnderscore = isUnderscore;
            }

            return result.ToString().Trim('_');
        }

        private static bool TryFindLocalKeyword(LocalKeywordSpace space, string name, out LocalKeyword keyword)
        {
            try
            {
                keyword = space.FindKeyword(name);
                // A found keyword has a valid name; default(LocalKeyword) has empty name.
                return !string.IsNullOrEmpty(keyword.name);
            }
            catch
            {
                keyword = default;
                return false;
            }
        }

        private static MaterialProperty FindPropertySafe(string name, MaterialProperty[] props)
        {
            if (string.IsNullOrEmpty(name) || props == null)
                return null;

            for (int i = 0; i < props.Length; i++)
            {
                if (props[i] != null && props[i].name == name)
                    return props[i];
            }
            return null;
        }

        private static int IndexOf(string[] arr, string value)
        {
            if (arr == null) return -1;
            for (int i = 0; i < arr.Length; i++)
                if (string.Equals(arr[i], value, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }
    }

}
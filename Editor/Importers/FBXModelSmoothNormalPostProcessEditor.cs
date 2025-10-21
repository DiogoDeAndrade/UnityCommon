// Assets/Editor/ModelSmoothNormalPostProcessEditor.cs
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UC
{

    [CustomEditor(typeof(ModelImporter))]
    [CanEditMultipleObjects]
    public class FBXModelSmoothNormalPostProcessEditor : UnityEditor.Editor
    {
        private AssetImporterEditor defaultEditor;
        private SmoothNormalsUtil.Settings pendingChanges;
        private bool changes;

        void OnEnable()
        {
            if (defaultEditor == null)
            {
                var builtInType = Type.GetType("UnityEditor.ModelImporterEditor, UnityEditor");
                defaultEditor = (AssetImporterEditor)AssetImporterEditor.CreateEditor(targets, builtInType);

                // Wire this wrapper as the "target editor" so the built-in Apply/Revert keeps working.
                var dynMethod = builtInType.GetMethod("InternalSetAssetImporterTargetEditor",
                                                      BindingFlags.NonPublic | BindingFlags.Instance);
                dynMethod.Invoke(defaultEditor, new object[] { this });
            }
            else
            {
                defaultEditor.OnEnable();
            }

            // --- Load existing importer settings into pendingChanges ---
            if (targets != null && targets.Length > 0 && targets[0] is ModelImporter first)
            {
                try
                {
                    if (!string.IsNullOrEmpty(first.userData))
                    {
                        pendingChanges = JsonUtility.FromJson<SmoothNormalsUtil.Settings>(first.userData);
                        if (pendingChanges == null)
                            pendingChanges = new SmoothNormalsUtil.Settings();
                    }
                    else
                    {
                        pendingChanges = new SmoothNormalsUtil.Settings();
                    }
                }
                catch
                {
                    pendingChanges = new SmoothNormalsUtil.Settings();
                }
            }
            else
            {
                pendingChanges = new SmoothNormalsUtil.Settings();
            }
            changes = false;
        }

        void OnDisable()
        {
            if (defaultEditor != null)
                defaultEditor.OnDisable();
        }

        void OnDestroy()
        {
            if (defaultEditor != null)
            {
                // Symmetric to the sample: make sure the inner editor leaves a clean state.
                defaultEditor.OnEnable();
                DestroyImmediate(defaultEditor);
                defaultEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            // Draw Unity's pretty Model Importer UI
            defaultEditor.OnInspectorGUI();

            // ---- Our extra block -------------------------------------------------
            var targetsArr = targets;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Smooth Normals", EditorStyles.boldLabel);

            // Checkbox
            EditorGUI.BeginChangeCheck();

            pendingChanges.generateSmoothNormals = EditorGUILayout.Toggle("Generate Smooth Normals", pendingChanges.generateSmoothNormals);

            // UV channel (only when enabled)
            if (pendingChanges.generateSmoothNormals)
            {
                pendingChanges.uvChannel = (SmoothNormalsUtil.Settings.UVChannel)EditorGUILayout.EnumPopup("UV Channel", pendingChanges.uvChannel);
            }

            if (EditorGUI.EndChangeCheck())
            {
                changes = true;
            }

            // Apply immediately (same behavior as the sample)
            using (new EditorGUI.DisabledScope(!changes))
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace(); // push content to the right

                    // Half the width of the inspector window
                    float buttonWidth = EditorGUIUtility.currentViewWidth * 0.5f - 20f;

                    if (GUILayout.Button("Generate Smooth Normals", GUILayout.Height(22), GUILayout.Width(buttonWidth)))
                    {
                        Undo.RecordObjects(targetsArr, "Change Smooth Normals Import Settings");

                        foreach (var o in targetsArr)
                        {
                            var mi = (ModelImporter)o;
                            var s = Load(mi);

                            s.generateSmoothNormals = pendingChanges.generateSmoothNormals;
                            s.uvChannel = pendingChanges.uvChannel;

                            mi.userData = JsonUtility.ToJson(s);
                            AssetDatabase.WriteImportSettingsIfDirty(mi.assetPath);
                            mi.SaveAndReimport();
                        }

                        changes = false;
                        GUI.FocusControl(null); // unfocus button to avoid persistent highlight
                    }
                }
            }

        }

        // ---- helpers ------------------------------------------------------------

        private static SmoothNormalsUtil.Settings Load(ModelImporter mi)
        {
            if (string.IsNullOrEmpty(mi.userData)) return new SmoothNormalsUtil.Settings();
            try
            {
                return JsonUtility.FromJson<SmoothNormalsUtil.Settings>(mi.userData)
                       ?? new SmoothNormalsUtil.Settings();
            }
            catch
            {
                return new SmoothNormalsUtil.Settings();
            }
        }

        private readonly struct MixedValueScope : IDisposable
        {
            private readonly bool prev;
            public MixedValueScope(bool mixed) { prev = EditorGUI.showMixedValue; EditorGUI.showMixedValue = mixed; }
            public void Dispose() => EditorGUI.showMixedValue = prev;
        }
    }
}
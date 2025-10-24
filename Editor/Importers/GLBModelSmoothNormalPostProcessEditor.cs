// Assets/Editor/GLBModelSmoothNormalPostProcessEditor.cs
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

#if UNITYGLTF_PRESENT
namespace UC
{

    // NOTE: Adjust the type below if your UnityGLTF importer lives in a different namespace,
    // e.g. UnityGLTF.Editor.GLTFImporter. Use the "Print Importer Type" trick if unsure.
    [CustomEditor(typeof(UnityGLTF.GLTFImporter))]
    [CanEditMultipleObjects]
    public class GLBModelSmoothNormalPostProcessEditor : UnityEditor.Editor
    {
        private AssetImporterEditor defaultEditor;               // the GLTF ScriptedImporter editor
        private SmoothNormalsUtil.Settings pendingChanges = new();
        private bool changes;

        void OnEnable()
        {
            if (defaultEditor == null)
            {
                // 1) Try the package’s custom inspector first
                var builtInType =
                    Type.GetType("UnityGLTF.GLTFImporterInspector, UnityGLTFEditor");

                // 2) Fallback to the generic ScriptedImporter editor if needed
                if (builtInType == null)
                {
                    Debug.LogWarning("GLTFImporterInspector not found. Falling back to ScriptedImporterEditor.");
                    builtInType = Type.GetType("UnityEditor.AssetImporters.ScriptedImporterEditor, UnityEditor");
                }

                defaultEditor = (AssetImporterEditor)
                    AssetImporterEditor.CreateEditor(targets, builtInType);

                // Wire this wrapper as the “target editor” so Apply/Revert still works
                var hook = builtInType.GetMethod("InternalSetAssetImporterTargetEditor",
                                                 BindingFlags.Instance | BindingFlags.NonPublic);
                if (hook != null)
                    hook.Invoke(defaultEditor, new object[] { this });
            }
            else
            {
                defaultEditor.OnEnable();
            }

            // Load existing settings from the first selected importer
            var first = (AssetImporter)target;
            try
            {
                pendingChanges = string.IsNullOrEmpty(first.userData)
                    ? new SmoothNormalsUtil.Settings()
                    : (JsonUtility.FromJson<SmoothNormalsUtil.Settings>(first.userData)
                       ?? new SmoothNormalsUtil.Settings());
            }
            catch
            {
                pendingChanges = new SmoothNormalsUtil.Settings();
            }

            changes = false;
        }

        void OnDisable()
        {
            if (defaultEditor != null) defaultEditor.OnDisable();
        }

        void OnDestroy()
        {
            if (defaultEditor != null)
            {
                defaultEditor.OnEnable();           // symmetry with Unity’s sample pattern
                DestroyImmediate(defaultEditor);
                defaultEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            // 1) draw the GLTF importer’s default inspector first
            defaultEditor.OnInspectorGUI();

            // 2) our extras
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Smooth Normals", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            pendingChanges.generateSmoothNormals =
                EditorGUILayout.Toggle("Enable", pendingChanges.generateSmoothNormals);

            if (pendingChanges.generateSmoothNormals)
            {
                pendingChanges.uvChannel = (SmoothNormalsUtil.Settings.UVChannel)
                    EditorGUILayout.EnumPopup("UV Channel", pendingChanges.uvChannel);
            }

            if (EditorGUI.EndChangeCheck()) changes = true;

            // 3) our own commit button (independent from the default Apply/Revert)
            using (new EditorGUI.DisabledScope(!changes))
            {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    float buttonWidth = EditorGUIUtility.currentViewWidth * 0.5f - 20f;

                    if (GUILayout.Button("Generate Smooth Normals",
                                         GUILayout.Height(22), GUILayout.Width(buttonWidth)))
                    {
                        Undo.RecordObjects(targets, "Generate Smooth Normals");

                        foreach (var o in targets)
                        {
                            var imp = (AssetImporter)o;

                            // write settings into userData
                            imp.userData = JsonUtility.ToJson(pendingChanges);
                            AssetDatabase.WriteImportSettingsIfDirty(imp.assetPath);

                            // reimport so your AssetPostprocessor path runs (OnPostprocessAllAssets handles GLB)
                            imp.SaveAndReimport();
                        }

                        changes = false;
                        GUI.FocusControl(null);
                    }
                }
            }
        }
    }
}
#endif
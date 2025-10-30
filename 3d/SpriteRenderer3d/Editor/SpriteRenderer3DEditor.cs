#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{

    [CustomEditor(typeof(SpriteRenderer3D))]
    [CanEditMultipleObjects]
    public class SpriteRenderer3DEditor : UnityEditor.Editor
    {
        SerializedProperty _sprite, _color, _material;
        SerializedProperty _billboardMode, _pivotOffset, _rotationOffsetEuler;
        SerializedProperty _layer, _sortingPriority, _shadowCasting, _receiveShadows, _lightProbeUsage;

        MaterialEditor _matEditor;

        void OnEnable()
        {
            _sprite = serializedObject.FindProperty("_sprite");
            _color = serializedObject.FindProperty("_color");
            _material = serializedObject.FindProperty("_material");

            _billboardMode = serializedObject.FindProperty("_billboardMode");
            _pivotOffset = serializedObject.FindProperty("_pivotOffset");
            _rotationOffsetEuler = serializedObject.FindProperty("_rotationOffsetEuler");

            _layer = serializedObject.FindProperty("_layer");
            _sortingPriority = serializedObject.FindProperty("_sortingPriority");
            _shadowCasting = serializedObject.FindProperty("_shadowCasting");
            _receiveShadows = serializedObject.FindProperty("_receiveShadows");
            _lightProbeUsage = serializedObject.FindProperty("_lightProbeUsage");
        }

        void OnDisable()
        {
            if (_matEditor != null)
            {
                DestroyImmediate(_matEditor);
                _matEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // --- Source ---
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_sprite, new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(_color, new GUIContent("Color"));
            EditorGUILayout.PropertyField(_material, new GUIContent("Material"));

            EditorGUILayout.Space();

            // --- Billboarding ---
            EditorGUILayout.LabelField("Billboarding", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_billboardMode);
            EditorGUILayout.PropertyField(_pivotOffset);
            EditorGUILayout.PropertyField(_rotationOffsetEuler);

            EditorGUILayout.Space();

            // --- Rendering ---
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_layer);
            EditorGUILayout.PropertyField(_sortingPriority);
            EditorGUILayout.PropertyField(_shadowCasting);
            EditorGUILayout.PropertyField(_receiveShadows);
            EditorGUILayout.PropertyField(_lightProbeUsage);

            serializedObject.ApplyModifiedProperties();

            // --- Embedded Material inspector (with standard material preview, if you open the material itself) ---
            var mat = (Material)_material.objectReferenceValue;
            if (mat != null)
            {
                if (_matEditor == null || _matEditor.target != mat)
                {
                    if (_matEditor != null) DestroyImmediate(_matEditor);
                    _matEditor = (MaterialEditor)CreateEditor(mat);
                }

                EditorGUILayout.Space();
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _matEditor.DrawHeader();
                    _matEditor.OnInspectorGUI();

                    EditorGUILayout.Space(6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Make Local Copy"))
                        {
                            var clone = new Material(mat) { name = mat.name + " (Instance)" };
                            Undo.RegisterCreatedObjectUndo(clone, "Create Material Instance");
                            _material.objectReferenceValue = clone;
                            serializedObject.ApplyModifiedProperties();
                            DestroyImmediate(_matEditor);
                            _matEditor = (MaterialEditor)CreateEditor(clone);
                        }
                        if (GUILayout.Button("Ping"))
                            EditorGUIUtility.PingObject(mat);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Assign a Material to edit its properties here.", MessageType.Info);
            }
        }
    }
#endif
}
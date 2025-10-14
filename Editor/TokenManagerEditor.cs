// Assets/Editor/TokenManagerEditor.cs
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    [CustomEditor(typeof(TokenManager))]
    public class TokenManagerEditor : UnityEditor.Editor
    {
        GUIStyle _headerStyle;
        GUIStyle _colHeaderStyle;
        bool _showRuntime = true;

        void OnEnable()
        {
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _colHeaderStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft
            };
        }

        public override void OnInspectorGUI()
        {
            // Show default inspector fields (if you add any later)
            DrawDefaultInspector();

            EditorGUILayout.Space(8);

            // Runtime panel
            _showRuntime = EditorGUILayout.Foldout(_showRuntime, "Active Tokens (Runtime)", true, _headerStyle);
            if (_showRuntime)
            {
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                {
                    if (!Application.isPlaying)
                    {
                        EditorGUILayout.HelpBox("Enter Play Mode to see active tokens.", MessageType.Info);
                    }
                    else
                    {
                        var mgr = (TokenManager)target;

                        // Column headers
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Token", _colHeaderStyle);
                            GUILayout.FlexibleSpace();
                            GUILayout.Label("Qty", _colHeaderStyle, GUILayout.Width(40));
                        }
                        EditorGUILayout.Separator();

                        int shown = 0;
                        foreach (var (token, count) in mgr) // only yields count > 0
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.ObjectField(token, typeof(Object), false);
                                GUILayout.Label(count.ToString(), GUILayout.Width(40));
                            }
                            shown++;
                        }

                        if (shown == 0)
                            EditorGUILayout.HelpBox("No active tokens.", MessageType.None);

                        // Keep the list live-updating while in Play Mode
                        if (Application.isPlaying)
                            Repaint();
                    }
                }
            }
        }
    }
}

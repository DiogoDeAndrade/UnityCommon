using NaughtyAttributes.Editor;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    [CustomEditor(typeof(PlayableHandler))]
    public class PlayableHandlerEditor : NaughtyInspector
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var handler = (PlayableHandler)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Runtime Debug", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Runtime animation debug is only available in Play Mode.", MessageType.Info);
                return;
            }

            DrawCurrentAnimation(handler);
            DrawTransition(handler);
            DrawFauxLoop(handler);

            Repaint();
        }

        private static void DrawCurrentAnimation(PlayableHandler handler)
        {
            AnimationClip clip = handler.currentClip;

            EditorGUILayout.LabelField("Current Clip", clip != null ? clip.name : "<none>");

            if (clip == null)
                return;

            EditorGUILayout.LabelField("Reversed", handler.currentReversed ? "Yes" : "No");

            float normalized = handler.CurrentNormalizedTime;
            double currentTime = handler.CurrentTime;
            float length = clip.length;

            Rect rect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(
                rect,
                normalized,
                $"{normalized * 100f:0.0}%    {currentTime:0.000}s / {length:0.000}s"
            );

            EditorGUILayout.LabelField("Raw Normalized Time", handler.CurrentRawNormalizedTime.ToString("0.000"));
        }

        private static void DrawTransition(PlayableHandler handler)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Transition", EditorStyles.boldLabel);

            if (!handler.IsTransitioning)
            {
                EditorGUILayout.LabelField("State", "Not transitioning");
                return;
            }

            string fromName = handler.TransitionFromClip != null ? handler.TransitionFromClip.name : "<none>";
            string toName = handler.TransitionToClip != null ? handler.TransitionToClip.name : "<none>";

            EditorGUILayout.LabelField("State", "Transitioning");
            EditorGUILayout.LabelField("From", fromName);
            EditorGUILayout.LabelField("To", toName);

            float normalized = handler.TransitionNormalizedTime;

            Rect rect = GUILayoutUtility.GetRect(18f, 18f);
            EditorGUI.ProgressBar(
                rect,
                normalized,
                $"{normalized * 100f:0.0}%    {handler.TransitionElapsed:0.000}s / {handler.TransitionDuration:0.000}s"
            );
        }

        private static void DrawFauxLoop(PlayableHandler handler)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Faux Loop", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Enabled", handler.FauxLoopEnabled ? "Yes" : "No");

            if (!handler.FauxLoopEnabled)
                return;

            EditorGUILayout.LabelField("Range", $"{handler.FauxLoopStartNormalized:0.000} -> {handler.FauxLoopEndNormalized:0.000}"
            );

            string maxLoops = handler.FauxLoopMaxLoops < 0
                ? "Infinite"
                : handler.FauxLoopMaxLoops.ToString();

            EditorGUILayout.LabelField(
                "Loops",
                $"{handler.FauxLoopCompletedLoops} / {maxLoops}"
            );
        }
    }
}

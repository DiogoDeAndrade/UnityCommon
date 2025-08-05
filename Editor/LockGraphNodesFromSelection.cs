#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UC
{
    [InitializeOnLoad]
    public static class LockGraphNodeSelection
    {
        static LockGraphNodeSelection()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (Event.current.type == EventType.Layout) return;

            GraphNodeComponent node = null;

            if (Selection.activeGameObject != null)
            {
                node = Selection.activeGameObject.GetComponent<GraphNodeComponent>();
            }

            if (node)
            {
                var editor = node.GetComponentInParent<UC.GraphEditor>();
                if ((editor != null) && (editor.enabled))
                {
                    // Cancel selection by re-selecting GraphEditor
                    Selection.activeGameObject = editor.gameObject;
                    Event.current.Use();
                }
            }
        }

        private static void OnSelectionChanged()
        {
            var go = Selection.activeGameObject;
            if (go == null) return;

            var node = go.GetComponent<UC.GraphNodeComponent>();
            if (node != null)
            {
                var editor = node.GetComponentInParent<UC.GraphEditor>();
                if ((editor != null) && (editor.enabled))
                {
                    Selection.activeGameObject = editor.gameObject;
                }
            }
        }
    }
}
#endif

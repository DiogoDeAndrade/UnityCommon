#if UNITY_EDITOR

using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace UC
{

    [InitializeOnLoad]
    static partial class ShiftDuplicate
    {
        [AutoStaticsCleanup]
        private static bool _isDragging;

        static ShiftDuplicate()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView scn)
        {
            Event e = Event.current;

            if (!_isDragging && e.modifiers == EventModifiers.Shift && e.type == EventType.MouseDrag)
            {
                _isDragging = true;
                scn.SendEvent(EditorGUIUtility.CommandEvent("Duplicate"));
            }

            if (e.type is EventType.MouseUp or EventType.Ignore)
                _isDragging = false;
        }
    }
}
#endif
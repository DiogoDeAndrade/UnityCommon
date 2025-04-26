using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace UC
{
    [Overlay(typeof(SceneView), "Path XY Edit")]
    public class PathXYOverlay : Overlay
    {
        EditorTool currentTool;
        GUIContent iconContent;

        PathXY editingPath => Selection.activeGameObject?.GetComponent<PathXY>();
        
        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement();

            IMGUIContainer imgui = new IMGUIContainer(() =>
            {
                DrawOverlayIMGUI();
            });

            root.Add(imgui);
            return root;
        }

        void DrawOverlayIMGUI()
        {
            bool prevSelection;

            if (editingPath == null)
                return;

            bool isBezierOrSmooth = (editingPath.pathType == PathXY.Type.Bezier || editingPath.pathType == PathXY.Type.CatmulRom);
            int editPoint = PathXYEditor.GetEditPoint(editingPath);
            bool hasSelection = editPoint >= 0;

            if (GUILayout.Button(isBezierOrSmooth ? "Append Segment" : "Append Point"))
            {
                Undo.RecordObject(editingPath, "Add Segment/Point");
                if (isBezierOrSmooth)
                    editingPath.AddSegment();
                else
                    editingPath.AddPoint();
                EditorUtility.SetDirty(editingPath);
            }

            prevSelection = GUI.enabled;
            GUI.enabled = hasSelection;
            if (GUILayout.Button(isBezierOrSmooth ? "Insert Segment" : "Insert Point"))
            {
                Undo.RecordObject(editingPath, "Insert Segment/Point");
                if (isBezierOrSmooth)
                    editingPath.AddSegment(editPoint);
                else
                    editingPath.AddPoint(editPoint);
                EditorUtility.SetDirty(editingPath);
            }
            GUI.enabled = prevSelection;

            if (GUILayout.Button("Invert Path"))
            {
                Undo.RecordObject(editingPath, "Invert Path");
                editingPath.InvertPath();
                EditorUtility.SetDirty(editingPath);
            }

            if (GUILayout.Button("Center Path"))
            {
                Undo.RecordObject(editingPath, "Center Path");
                editingPath.CenterPath();
                EditorUtility.SetDirty(editingPath);
            }

            if (GUILayout.Button(editingPath.isWorldSpace ? "Convert to Local" : "Convert to World"))
            {
                TogglePathSpace();
            }
        }

        void TogglePathSpace()
        {
            if (editingPath == null) return;

            bool isWorldSpace = editingPath.isWorldSpace;

            Undo.RecordObject(editingPath, "Convert Path Space");
            if (isWorldSpace)
                editingPath.ConvertToLocalSpace();
            else
                editingPath.ConvertToWorldSpace();

            // Flip world/local mode
            var so = new SerializedObject(editingPath);
            var wsProp = so.FindProperty("worldSpace");
            wsProp.boolValue = !isWorldSpace;
            so.ApplyModifiedProperties();

            editingPath.SetDirty();
            EditorUtility.SetDirty(editingPath);
        }
    }
}

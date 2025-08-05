#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UC
{
    [EditorTool("Graph Link Tool", typeof(GraphEditor))]
    public class GraphEditorTool : EditorTool
    {
        private GraphNodeComponent dragStartNode = null;
        private GraphNodeComponent selectedNode = null;
        private bool isDragging = false;
        private (GraphNodeComponent a, GraphNodeComponent b)? selectedLink = null;

        public override void OnToolGUI(EditorWindow window)
        {
            var editor = (GraphEditor)target;

            if (!editor.enabled) return;

            var nodes = editor.Nodes;
            var e = Event.current;
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Vector2 mousePos = e.mousePosition;
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(mousePos);
            Plane plane = new(Vector3.up, Vector3.zero);
            plane.Raycast(mouseRay, out float hitDistance);
            Vector3 mouseWorld = mouseRay.GetPoint(hitDistance);

            // Delete key: remove link or node
            if ((e.type == EventType.KeyDown) && (e.keyCode == KeyCode.Delete))
            {
                if (selectedLink.HasValue)
                {
                    var (a, b) = selectedLink.Value;
                    if (a != null && b != null)
                    {
                        RemoveLink(editor, a, b);
                        if (!(editor.GraphComponent?.isDirected ?? false))
                            RemoveLink(editor, b, a);

                        UpdateGraph(editor);

                        selectedLink = null;
                        e.Use();
                        return;
                    }
                }
                else if (selectedNode != null)
                {
                    Undo.DestroyObjectImmediate(selectedNode.gameObject);
                    selectedNode = null;
                    e.Use();

                    UpdateGraph(editor);
                    return;
                }
            }

            // Link click detection (only in middle of segment)
            if ((e.type == EventType.MouseDown) && (e.button == 0) && (e.control))
            {
                Vector2 clickPos = e.mousePosition;
                float selectionThreshold = 8f;

                foreach (var from in nodes)
                {
                    var fromPos = HandleUtility.WorldToGUIPoint(from.transform.position);
                    foreach (var to in from.GetLinks() ?? new GraphNodeComponent[0])
                    {
                        if (to == null) continue;
                        var toPos = HandleUtility.WorldToGUIPoint(to.transform.position);

                        float t = ClosestPointT(fromPos, toPos, clickPos);
                        if (t < 0.05f || t > 0.95f)
                            continue;

                        float dist = HandleUtility.DistancePointLine(clickPos, fromPos, toPos);
                        if (dist < selectionThreshold)
                        {
                            selectedLink = (from, to);
                            selectedNode = null;
                            e.Use();
                            return;
                        }
                    }
                }
                selectedLink = null;
            }

            // Node interactions
            foreach (var node in nodes)
            {
                Vector3 pos = node.transform.position;
                float size = HandleUtility.GetHandleSize(pos) * 0.1f;

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (HandleUtility.DistanceToCircle(pos, size) < 10f)
                    {
                        if (e.control)
                        {
                            dragStartNode = node;
                            isDragging = false;
                            e.Use();
                            return;
                        }
                        else
                        {
                            if (selectedNode != node)
                            {
                                selectedNode = node;
                                selectedLink = null;
                                e.Use();
                                return;
                            }
                        }
                    }
                }
                else if (e.type == EventType.MouseDrag && dragStartNode == node && e.button == 0)
                {
                    isDragging = true;
                    e.Use();
                    return;
                }
                else if (e.type == EventType.MouseUp && dragStartNode != null && e.button == 0)
                {
                    if (isDragging)
                    {
                        foreach (var target in nodes)
                        {
                            if (target == dragStartNode) continue;
                            float dist = HandleUtility.DistanceToCircle(target.transform.position, size);
                            if (dist < 10f)
                            {
                                AddLink(editor, dragStartNode, target);
                                break;
                            }
                        }
                    }
                    

                    dragStartNode = null;
                    isDragging = false;
                    e.Use();
                    return;
                }
            }

            if (dragStartNode != null && isDragging)
            {
                Handles.color = Color.red;
                Handles.DrawLine(dragStartNode.transform.position, mouseWorld);
                SceneView.RepaintAll();
            }

            if (selectedNode != null)
            {
                EditorGUI.BeginChangeCheck();
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(selectedNode.transform.position, Vector3.up, HandleUtility.GetHandleSize(selectedNode.transform.position) * 0.2f);
                Vector3 newPos = Handles.PositionHandle(selectedNode.transform.position, Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(selectedNode.transform, "Move Node");
                    selectedNode.transform.position = newPos;
                    EditorUtility.SetDirty(selectedNode);
                }
            }

            if (selectedLink.HasValue)
            {
                var (a, b) = selectedLink.Value;
                if (a != null && b != null)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawAAPolyLine(4f, a.transform.position, b.transform.position);

                    if (!(editor.GraphComponent?.isDirected ?? false))
                        Handles.DrawAAPolyLine(4f, b.transform.position, a.transform.position);
                }
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                dragStartNode = null;
                selectedNode = null;
                selectedLink = null;
                isDragging = false;
                e.Use();
            }
        }

        private float ClosestPointT(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float lengthSq = ab.sqrMagnitude;
            if (lengthSq == 0) return 0;
            float t = Vector2.Dot(p - a, ab) / lengthSq;
            return Mathf.Clamp01(t);
        }

        private void AddLink(GraphEditor editor, GraphNodeComponent from, GraphNodeComponent to)
        {
            AddOneWayLink(from, to);

            if (!(editor.GraphComponent?.isDirected ?? false))
                AddOneWayLink(to, from);

            UpdateGraph(editor);
        }

        private void AddOneWayLink(GraphNodeComponent from, GraphNodeComponent to)
        {
            var currentLinks = new List<GraphNodeComponent>(from.GetLinks() ?? new GraphNodeComponent[0]);
            if (currentLinks.Contains(to)) return;

            Undo.RecordObject(from, "Add Graph Link");

            currentLinks.Add(to);
            SerializedObject so = new SerializedObject(from);
            SerializedProperty linksProp = so.FindProperty("links");
            linksProp.arraySize = currentLinks.Count;
            for (int i = 0; i < currentLinks.Count; i++)
                linksProp.GetArrayElementAtIndex(i).objectReferenceValue = currentLinks[i];
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(from);
            EditorSceneManager.MarkSceneDirty(from.gameObject.scene);
        }

        private void RemoveLink(GraphEditor editor, GraphNodeComponent from, GraphNodeComponent to)
        {
            var links = new List<GraphNodeComponent>(from.GetLinks() ?? new GraphNodeComponent[0]);
            if (!links.Contains(to)) return;

            Undo.RecordObject(from, "Remove Link");
            links.Remove(to);

            var so = new SerializedObject(from);
            var linksProp = so.FindProperty("links");
            linksProp.arraySize = links.Count;
            for (int i = 0; i < links.Count; i++)
                linksProp.GetArrayElementAtIndex(i).objectReferenceValue = links[i];
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(from);
            EditorSceneManager.MarkSceneDirty(from.gameObject.scene);
        }

        void UpdateGraph(GraphEditor editor)
        {
            editor.GraphComponent.BuildFromComponents();

        }
    }
}
#endif

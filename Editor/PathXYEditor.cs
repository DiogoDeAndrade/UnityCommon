using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;
using UnityEditor.EditorTools;

namespace UC
{

    [InitializeOnLoad]
    [CustomEditor(typeof(PathXY)), CanEditMultipleObjects]
    public class PathXYEditor : UnityCommonEditor
    {
        SerializedProperty propType;
        SerializedProperty propClosed;
        SerializedProperty propSides;
        SerializedProperty propPoints;
        SerializedProperty propWorldSpace;
        SerializedProperty propEditMode;

        static Dictionary<PathXY, int>  editPointPerPath = new();
        static int nextPointToSelect = -1;

        static public int GetEditPoint(PathXY path) => editPointPerPath.ContainsKey(path) ? (editPointPerPath[path]) : (-1); 

        static PathXYEditor()
        {
            SceneView.duringSceneGui += UpdatePathEditors;
        }

        static void UpdatePathEditors(SceneView sceneView)
        {
            // Enabling this code makes all PathXY that are close (in world coordinates) to the mouse click to activate, which
            // sometimes makes it hard to use the viewport - need a better solution for this 
           /* Event e = Event.current;

            if ((e.type == EventType.MouseDown) && (e.button == 0))
            {
                var paths = FindObjectsByType<PathXY>(FindObjectsSortMode.None);
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

                foreach (var path in paths)
                {
                    if (Selection.objects.Contains(path)) continue;
                    if (Selection.objects.Contains(path.gameObject)) continue;

                    var ret = path.GetDistance(ray, 5.0f);
                    if (ret.distance < 5.0f)
                    {
                        Selection.activeObject = path.gameObject;
                        if (ret.point >= 0)
                        {
                            nextPointToSelect = ret.point;
                        }
                        e.Use();
                        break;
                    }
                }
            }*/
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            propType = serializedObject.FindProperty("type");
            propClosed = serializedObject.FindProperty("closed");
            propSides = serializedObject.FindProperty("nSides");
            propPoints = serializedObject.FindProperty("points");
            propWorldSpace = serializedObject.FindProperty("worldSpace");
            propEditMode = serializedObject.FindProperty("editMode");
        }

        void OnDisable()
        {
            editPointPerPath.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (WriteTitle())
            {
                var t = (target as PathXY);

                if (targets.Length == 1)
                {
                    EditorGUI.BeginChangeCheck();

                    var type = (PathXY.Type)propType.intValue;

                    EditorGUILayout.PropertyField(propType, new GUIContent("Type", "Type of path.\nLinear: Straight lines between points\nSmooth: Curved line that passes through some points and is influenced by the others.\nCircle: The first point defines the center, the second the radius of the circle. If there is a third point, it defines the radius in that approximate direction.\nArc: First point defines the center, the second and third define the beginning and end of an arc centered on the first point.\nPolygon: First point define the center, the second and third point define the radius in different directions, while the 'Sides' property defines the number of sides of the polygon."));
                    if ((type != PathXY.Type.Circle) && (type != PathXY.Type.Arc))
                    {
                        bool wasClosed = propClosed.boolValue;
                        EditorGUILayout.PropertyField(propClosed, new GUIContent("Closed", "If the path should end where it starts."));
                        if ((propClosed.boolValue) && (!wasClosed))
                        {
                            if (t.pathType == PathXY.Type.Bezier && t.GetEditPointsCount() >= 7)
                            {
                                serializedObject.ApplyModifiedProperties();

                                Undo.RecordObject(target, "Close path");

                                var points = t.GetEditPoints();
                                int count = points.Count;

                                // Match last anchor to first
                                points[count - 1] = points[0];

                                // Mirror tangents: p1 and p(n-2)
                                Vector3 anchor = points[0];
                                Vector3 delta = points[1] - anchor;
                                points[count - 2] = anchor - delta;

                                serializedObject.Update();
                                t.SetEditPoints(points);
                                EditorUtility.SetDirty(t);
                            }
                        }
                    }
                    EditorGUILayout.PropertyField(propWorldSpace, new GUIContent("World Space", "Are the positions in world space, or relative to this object."));
                    if (type == PathXY.Type.Polygon)
                    {
                        EditorGUILayout.PropertyField(propSides, new GUIContent("Sides", "Number of sides in the polygon."));
                    }
                    EditorGUILayout.PropertyField(propPoints, new GUIContent("Points", "Waypoints"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }
        }

        public static void DrawPathEditingUI(PathXY t)
        {
            if (t == null) return;

            int editPoint = (editPointPerPath.ContainsKey(t)) ? (editPointPerPath[t]) : (-1);
            if (nextPointToSelect != -1)
            {
                editPoint = nextPointToSelect;
                nextPointToSelect = -1;
            }

            bool localSpace = !t.isWorldSpace;
            var type = t.pathType;

            List<Vector3> newPoints = t.GetEditPoints();

            bool manualDirty = false;

            EditorGUI.BeginChangeCheck();

            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.Delete)
                {
                    // Del pressed, check if a point is selected
                    if (editPoint >= 0)
                    {
                        // Remove this point
                        Undo.RecordObject(t, "Delete point");
                        newPoints.RemoveAt(editPoint);
                        editPoint = -1;
                        manualDirty = true;

                        e.Use();
                    }
                }
                if ((e.keyCode == KeyCode.KeypadPlus) && (editPoint >= 0))
                {
                    // Plus pressed, insert a point
                    // Get new position for this point (halfway between this and the next one)
                    Undo.RecordObject(t, "Add point");
                    editPoint = t.AddPoint(editPoint);

                    newPoints = t.GetEditPoints();
                    manualDirty = true;

                    e.Use();
                }
            }

            if (localSpace)
            {
                var worldMatrix = t.transform.localToWorldMatrix;
                for (int i = 0; i < newPoints.Count; i++) newPoints[i] = worldMatrix * new Vector4(newPoints[i].x, newPoints[i].y, newPoints[i].z, 1);
            }

            for (int i = 0; i < newPoints.Count; i++)
            {
                float s = (editPoint == i) ? (6.0f) : (5.0f);
                Handles.color = (editPoint == i) ? (Color.yellow) : (Color.white);
                bool selectable = true;
                bool isBezierHandle = false;

                // Render text
                string text = "";
                if ((type == PathXY.Type.Linear) || (type == PathXY.Type.CatmulRom))
                {
                    text = $"{i}";
                }
                else if ((type == PathXY.Type.Circle) || (type == PathXY.Type.Polygon))
                {
                    if (i == 0) text = "Center";
                    else if (i == 1) text = "Primary Axis";
                    else if (i == 2) text = "Secondary Axis";
                    else selectable = false;
                }
                else if (type == PathXY.Type.Arc)
                {
                    if (i == 0) text = "Center";
                    else if (i == 1) text = "Start";
                    else if (i == 2) text = "End";
                    else selectable = false;
                }
                else if (type == PathXY.Type.Bezier)
                {
                    text = $"{i}";
                    if ((t.isClosed) && (i == t.GetEditPointsCount() - 1)) text = "";

                    if (i % 3 != 0)
                    {
                        isBezierHandle = true;
                        Handles.color = (editPoint == i) ? (Color.cyan) : (Color.white);
                    }
                }

                if (selectable)
                {
                    if (Handles.Button(newPoints[i], Quaternion.identity, s, s, (isBezierHandle) ? (Handles.RectangleHandleCap) : (Handles.CircleHandleCap)))
                    {
                        editPoint = i;
                    }
                    if (editPoint == i)
                    {
                        if (isBezierHandle)
                            Handles.RectangleHandleCap(-1, newPoints[i], Quaternion.identity, s * 0.8f, EventType.Repaint);
                        else
                            Handles.CircleHandleCap(-1, newPoints[i], Quaternion.identity, s * 0.8f, EventType.Repaint);
                    }
                }
                if (text != "")
                {
                    Handles.Label(newPoints[i] + Vector3.right * s * 1.25f, text);
                }
            }

            Handles.color = Color.white;

            if ((editPoint >= 0) && (editPoint < newPoints.Count))
            {
                Vector3 original = newPoints[editPoint];
                Vector3 moved = Handles.PositionHandle(original, Quaternion.identity);

                if (moved != original)
                {
                    newPoints[editPoint] = moved;

                    if (type == PathXY.Type.Bezier)
                    {
                        if (editPoint % 3 == 0)
                        {
                            Vector3 delta = moved - original;
                            int count = newPoints.Count;

                            int anchor = editPoint;

                            int prevTangent = anchor - 1;
                            int nextTangent = anchor + 1;

                            if ((prevTangent < 0) && (t.isClosed)) prevTangent = (prevTangent - 1 + count) % count;
                            if ((nextTangent >= count) && (t.isClosed)) nextTangent = (nextTangent + 1) % count;

                            if ((prevTangent >= 0) && (prevTangent < count)) newPoints[prevTangent] += delta;
                            if ((nextTangent >= 0) && (nextTangent < count)) newPoints[nextTangent] += delta;

                            // Also move the duplicate anchor if needed
                            if (t.isClosed)
                            {
                                if (editPoint == 0)
                                {
                                    newPoints[count - 1] = moved; // move last anchor to match first
                                }
                                else if (editPoint == count - 1)
                                {
                                    newPoints[0] = moved; // move first anchor to match last
                                }
                            }
                        }

                        bool isSpecialEdgeTangent = ((editPoint == 1) || (editPoint == newPoints.Count - 2));
                        bool controlPressed = (Event.current != null) && Event.current.control;
                        if ((editPoint % 3 != 0) &&
                            (!controlPressed) && 
                            ((t.isClosed) || !isSpecialEdgeTangent))
                        {
                            // Mirror tangents
                            int anchorIndex = -1;
                            int mirrorIndex = -1;
                            int count = newPoints.Count;

                            if ((editPoint % 3) == 1)
                            {
                                // Outgoing tangent: anchor is before
                                anchorIndex = editPoint - 1;
                                mirrorIndex = anchorIndex - 1;

                                // Special case: p1 should mirror to p5
                                if ((t.isClosed) && (anchorIndex == 0))
                                {
                                    mirrorIndex = count - 2; // p5
                                }
                            }
                            else if ((editPoint % 3) == 2)
                            {
                                // Incoming tangent: anchor is after
                                anchorIndex = editPoint + 1;
                                mirrorIndex = anchorIndex + 1;

                                // Special case: p5 should mirror to p1
                                if (t.isClosed && anchorIndex == count - 1)
                                {
                                    mirrorIndex = 1; // p1
                                }
                            }

                            // Wrap safely
                            anchorIndex = (anchorIndex + count) % count;
                            mirrorIndex = (mirrorIndex + count) % count;

                            if (anchorIndex >= 0 && anchorIndex < count &&
                                mirrorIndex >= 0 && mirrorIndex < count)
                            {
                                Vector3 anchor = newPoints[anchorIndex];
                                Vector3 delta = moved - anchor;
                                newPoints[mirrorIndex] = anchor - delta;
                            }
                        }
                    }
                }

                // Adjust perpendicular axis on specific path types
                if ((type == PathXY.Type.Circle) || (type == PathXY.Type.Polygon))
                {
                    if (newPoints.Count >= 2)
                    {
                        Vector3 delta = (newPoints[1] - newPoints[0]).normalized;
                        (delta.x, delta.y) = (delta.y, -delta.x);
                        delta.Normalize();

                        if (newPoints.Count >= 3)
                        {
                            float r = Vector3.Dot(delta, (newPoints[2] - newPoints[1]));
                            newPoints[2] = newPoints[0] + r * delta;
                        }
                    }
                }
            }

            if (localSpace)
            {
                var worldMatrix = t.transform.worldToLocalMatrix;
                for (int i = 0; i < newPoints.Count; i++) newPoints[i] = worldMatrix * new Vector4(newPoints[i].x, newPoints[i].y, newPoints[i].z, 1.0f);
            }

            if ((EditorGUI.EndChangeCheck()) || (manualDirty))
            {
                Undo.RecordObject(t, "Move point");
                t.SetEditPoints(newPoints);
            }

            RenderPath(t, true);

            editPointPerPath[t] = editPoint;
        }

        static void RenderPath(PathXY path, bool displayTangents)
        {
            var prevMatrix = Handles.matrix;
            if (!path.isWorldSpace)
            {
                Handles.matrix = path.transform.localToWorldMatrix;
            }

            var type = (PathXY.Type)path.pathType;
            var editPoints = path.GetEditPoints();
            if ((type == PathXY.Type.CatmulRom) && (!path.isClosed))
            {
                if (editPoints.Count > 2)
                {
                    // Draw last segment
                    Vector3 p1 = editPoints[editPoints.Count - 1];
                    Vector3 p2 = editPoints[editPoints.Count - 2];
                    Vector3 d = p2 - p1;

                    float len = 0.1f;
                    Handles.color = Color.grey;
                    for (int i = 0; i < 1.0f / len; i++)
                    {
                        Handles.DrawLine(p1 + d * i * len, p1 + d * (i + 0.5f) * len, 0.5f);
                    }
                }
            }
            if ((type == PathXY.Type.Bezier) && (displayTangents))
            {
                Handles.color = Color.gray;
                var pts = editPoints;

                int count = pts.Count;
                int step = 3;

                for (int i = 0; i + 3 < count; i += step)
                {
                    Handles.DrawDottedLine(pts[i], pts[i + 1], 2.0f); // Start anchor to handle
                    Handles.DrawDottedLine(pts[i + 2], pts[i + 3], 2.0f); // Handle to end anchor
                }

                if (path.isClosed && count >= 4 && count % 3 == 1)
                {
                    // Close the last curve
                    Handles.DrawDottedLine(pts[count - 1], pts[0], 2.0f);
                    Handles.DrawDottedLine(pts[count - 2], pts[count - 1], 2.0f);
                }
            }

            if (((type == PathXY.Type.Circle) || (type == PathXY.Type.Polygon) || (type == PathXY.Type.Polygon)) && (editPoints.Count > 0))
            {
                Vector3 center = editPoints[0];
                Vector3 upBound = path.upAxis * path.upExtent;
                Vector3 rightBound = path.rightAxis * path.rightExtent;

                Handles.color = Color.yellow;
                Handles.DrawPolyLine(new Vector3[] { center - rightBound - upBound, center + rightBound - upBound, center + rightBound + upBound, center - rightBound + upBound, center - rightBound - upBound });
            }

            Handles.matrix = prevMatrix;
        }

        public void OnSceneGUI()
        {
            if (ToolManager.activeToolType == typeof(PathXYTool))
            {
                // When PathXYTool is active, don't draw extra stuff here
                return;
            }

            var t = (target as PathXY);
            if (t == null) return;

            RenderPath(t, false);
        }

        protected override GUIStyle GetTitleSyle()
        {
            return GUIUtils.GetTriggerTitleStyle();
        }

        protected override string GetTitle()
        {
            return "Path XY";
        }

        protected override (Texture2D, Rect) GetIcon()
        {
            Texture2D varTexture = null;
            if (propType.intValue == (int)PathXY.Type.Linear)
                varTexture = GUIUtils.GetTexture("PathStraight");
            else
                varTexture = GUIUtils.GetTexture("PathCurved");

            return (varTexture, new Rect(0.0f, 0.0f, 1.0f, 1.0f));
        }

        protected override (Color, Color, Color) GetColors() => (GUIUtils.ColorFromHex("#ffcaca"), GUIUtils.ColorFromHex("#2f4858"), GUIUtils.ColorFromHex("#ff6060"));
    }
}
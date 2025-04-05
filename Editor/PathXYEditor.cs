using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

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
        SerializedProperty propOnlyDisplayWhenSelected;
        SerializedProperty propDisplayColor;

        int editPoint = -1;
        UnityEditor.Tool lastTool = UnityEditor.Tool.None;

        static int nextPointToSelect = -1;

        static PathXYEditor()
        {
            SceneView.duringSceneGui += UpdatePathEditors;
        }

        static void UpdatePathEditors(SceneView sceneView)
        {
            Event e = Event.current;

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
            }
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
            propOnlyDisplayWhenSelected = serializedObject.FindProperty("onlyDisplayWhenSelected");
            propDisplayColor = serializedObject.FindProperty("displayColor");

            lastTool = Tools.current;
        }
        void OnDisable()
        {
            Tools.current = lastTool;
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
                        EditorGUILayout.PropertyField(propClosed, new GUIContent("Closed", "If the path should end where it starts."));
                    }
                    if (type == PathXY.Type.Polygon)
                    {
                        EditorGUILayout.PropertyField(propSides, new GUIContent("Sides", "Number of sides in the polygon."));
                    }
                    EditorGUILayout.PropertyField(propPoints, new GUIContent("Points", "Waypoints"));
                    EditorGUILayout.PropertyField(propWorldSpace, new GUIContent("World Space", "Are the positions in world space, or relative to this object."));
                    EditorGUILayout.PropertyField(propEditMode, new GUIContent("Edit Mode", "If edit mode is on, you can edit the points the scene view.\nClick on a point to select it, use the gizmo to move them around."));
                    EditorGUILayout.PropertyField(propOnlyDisplayWhenSelected, new GUIContent("Only display when selected", "If on, it will only display the path when the object is selected, otherwise it will show the object with the selected color."));
                    EditorGUILayout.PropertyField(propDisplayColor, new GUIContent("Display Color", "What color should the path be rendered when not being edited"));

                    if (EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();
                    }

                    bool prevEnabled = GUI.enabled;
                    GUI.enabled = (propPoints.arraySize < 3) || ((type != PathXY.Type.Circle) && (type != PathXY.Type.Arc) && (type != PathXY.Type.Polygon));

                    if (type == PathXY.Type.Smooth)
                    {
                        if (GUILayout.Button("Add Segment"))
                        {
                            Undo.RecordObject(target, "Add segment");
                            t.AddPoint();
                            serializedObject.Update();

                            // Change to edit mode
                            propEditMode.boolValue = true;
                            serializedObject.ApplyModifiedProperties();

                            EditorUtility.SetDirty(target);

                            editPoint = t.GetEditPointsCount() - 1;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("Add Point"))
                        {
                            Undo.RecordObject(target, "Add point");
                            editPoint = t.AddPoint();
                            serializedObject.Update();

                            // Change to edit mode
                            propEditMode.boolValue = true;
                            serializedObject.ApplyModifiedProperties();

                            EditorUtility.SetDirty(target);
                        }
                    }
                    if ((editPoint >= 0) && (GUILayout.Button("Insert Point")))
                    {
                        Undo.RecordObject(target, "Insert point");
                        editPoint = t.AddPoint(editPoint);
                        serializedObject.Update();

                        // Change to edit mode
                        propEditMode.boolValue = true;
                        serializedObject.ApplyModifiedProperties();

                        EditorUtility.SetDirty(target);
                    }

                    GUI.enabled = prevEnabled;
                }

                bool repaint = false;

                bool canInvert = true;
                foreach (PathXY tpath in targets)
                {
                    if ((tpath.pathType != PathXY.Type.Linear) &&
                        (tpath.pathType != PathXY.Type.Smooth) &&
                        (tpath.pathType != PathXY.Type.Arc))
                    {
                        canInvert = false;
                        break;
                    }
                }

                if (canInvert)
                {
                    if (GUILayout.Button("Invert Path"))
                    {
                        Undo.RecordObject(target, "Invert path");
                        t.InvertPath();
                        serializedObject.Update();

                        repaint = true;
                    }
                }
                if (GUILayout.Button("Center Path"))
                {
                    Undo.RecordObject(target, "Center path");
                    foreach (PathXY tpath in targets) tpath.CenterPath();
                    serializedObject.Update();

                    repaint = true;
                }

                bool allInSameSpace = true;
                bool isWorld = (targets[0] as PathXY).isWorldSpace;
                for (int i = 1; i < targets.Length; i++)
                {
                    if ((targets[i] as PathXY).isWorldSpace != isWorld)
                    {
                        allInSameSpace = false;
                        break;
                    }
                }

                if (allInSameSpace)
                {
                    string convertButtonText = (propWorldSpace.boolValue) ? ("Convert to Local Space") : ("Convert to World Space");

                    if (GUILayout.Button(convertButtonText))
                    {
                        Undo.RecordObject(target, convertButtonText);

                        foreach (PathXY tpath in targets)
                        {
                            if (propWorldSpace.boolValue)
                                tpath.ConvertToLocalSpace();
                            else
                                tpath.ConvertToWorldSpace();
                        }
                        serializedObject.Update();

                        propWorldSpace.boolValue = !propWorldSpace.boolValue;
                        serializedObject.ApplyModifiedProperties();

                        foreach (PathXY tpath in targets) EditorUtility.SetDirty(tpath);

                        repaint = true;
                    }
                }

                if (repaint) SceneView.RepaintAll();
            }
        }

        public void OnSceneGUI()
        {
            if (nextPointToSelect != -1)
            {
                editPoint = nextPointToSelect;
                nextPointToSelect = -1;
            }

            var t = (target as PathXY);

            bool localSpace = !t.isWorldSpace;
            var type = (PathXY.Type)propType.intValue;

            if (t.isEditMode)
            {
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
                            Undo.RecordObject(target, "Delete point");
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
                        Undo.RecordObject(target, "Add point");
                        editPoint = t.AddPoint(editPoint);

                        newPoints = t.GetEditPoints();
                        manualDirty = true;

                        e.Use();
                    }
                }

                // Disable transform gizmo tool
                if (Tools.current != UnityEditor.Tool.None) lastTool = Tools.current;
                Tools.current = UnityEditor.Tool.None;

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

                    // Render text
                    string text = "";
                    if ((type == PathXY.Type.Linear) || (type == PathXY.Type.Smooth))
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

                    if (selectable)
                    {
                        if (Handles.Button(newPoints[i], Quaternion.identity, s, s, Handles.CircleHandleCap))
                        {
                            editPoint = i;
                        }
                        if (editPoint == i)
                        {
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
                    newPoints[editPoint] = Handles.PositionHandle(newPoints[editPoint], Quaternion.identity);

                    if ((type == PathXY.Type.Circle) || (type == PathXY.Type.Polygon))
                    {
                        if (newPoints.Count >= 2)
                        {
                            // Project this point into the perpendicular and use that instead
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
                    Undo.RecordObject(target, "Move point");
                    t.SetEditPoints(newPoints);
                }

                var prevMatrix = Handles.matrix;
                if (!t.isWorldSpace)
                {
                    Handles.matrix = t.transform.localToWorldMatrix;
                }

                var editPoints = t.GetEditPoints();
                if ((type == PathXY.Type.Smooth) && (!propClosed.boolValue))
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

                if (((type == PathXY.Type.Circle) || (type == PathXY.Type.Polygon) || (type == PathXY.Type.Polygon)) && (editPoints.Count > 0))
                {
                    Vector3 center = editPoints[0];
                    Vector3 upBound = t.upAxis * t.upExtent;
                    Vector3 rightBound = t.rightAxis * t.rightExtent;

                    Handles.color = Color.yellow;
                    Handles.DrawPolyLine(new Vector3[] { center - rightBound - upBound, center + rightBound - upBound, center + rightBound + upBound, center - rightBound + upBound, center - rightBound - upBound });
                }

                Handles.matrix = prevMatrix;
            }
            else
            {
                if (lastTool != UnityEditor.Tool.None) Tools.current = lastTool;
                lastTool = UnityEditor.Tool.None;
            }
        }

        void DrawPath(PathXY.Type type, List<Vector3> points, Color color, bool drawDirection)
        {
            if (points == null) return;

            Handles.color = color;

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 p = points[i - 1];

                Handles.DrawLine(p, points[i], 1.0f);
            }
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
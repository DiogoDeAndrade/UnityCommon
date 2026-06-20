using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace UC.Editor
{
    [CustomEditor(typeof(NavMeshLink2d))]
    public class NavMeshLink2dEditor : UnityEditor.Editor
    {
        SerializedProperty propAgentType;
        SerializedProperty propStart;
        SerializedProperty propEnd;
        SerializedProperty propAuto;
        SerializedProperty propBidir;
        SerializedProperty propCost;

        // Which endpoint (0 = A, 1 = B) is currently selected for moving, per link.
        static readonly Dictionary<NavMeshLink2d, int> editPointPerLink = new();

        void OnEnable()
        {
            propAgentType = serializedObject.FindProperty("agentType");
            propStart = serializedObject.FindProperty("localStart");
            propEnd = serializedObject.FindProperty("localEnd");
            propAuto = serializedObject.FindProperty("autoTraverse");
            propBidir = serializedObject.FindProperty("bidirectional");
            propCost = serializedObject.FindProperty("costMultiplier");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(propAgentType, new GUIContent("Agent Type", "Which navmesh (by agent type) this link belongs to."));
            EditorGUILayout.PropertyField(propStart, new GUIContent("Local Start (A)", "First endpoint, relative to this transform."));
            EditorGUILayout.PropertyField(propEnd, new GUIContent("Local End (B)", "Second endpoint, relative to this transform."));
            EditorGUILayout.PropertyField(propAuto, new GUIContent("Auto Traverse", "If set, the agent walks across the link. If clear, it delegates the crossing to an INavMeshLinkTraversal on the agent."));
            EditorGUILayout.PropertyField(propBidir, new GUIContent("Bidirectional", "If clear, the link can only be traversed from A to B."));
            EditorGUILayout.PropertyField(propCost, new GUIContent("Cost Multiplier", "Multiplier on the geometric bridge length used as the A* traversal cost."));

            EditorGUILayout.Space();

            // Box-Collider-style "Edit" toggle: activates / deactivates the scene-view editing tool.
            bool active = ToolManager.activeToolType == typeof(NavMeshLink2dTool);
            if (GUILayout.Button(active ? "Stop Editing Endpoints" : "Edit Endpoints"))
            {
                if (active) ToolManager.RestorePreviousPersistentTool();
                else ToolManager.SetActiveTool<NavMeshLink2dTool>();
            }

            serializedObject.ApplyModifiedProperties();
        }

        // Drawn by NavMeshLink2dTool while the tool is active. Lets the user pick an endpoint and move
        // it; endpoints are stored in local space and written back through SetEditPoints with undo.
        public static void DrawLinkEditingUI(NavMeshLink2d link)
        {
            if (link == null) return;

            var tr = link.transform;
            int editPoint = editPointPerLink.TryGetValue(link, out var ep) ? ep : 0;

            var local = link.GetEditPoints();
            Vector3[] world = { tr.TransformPoint(local[0]), tr.TransformPoint(local[1]) };
            string[] labels = { "A", "B" };

            EditorGUI.BeginChangeCheck();

            for (int i = 0; i < 2; i++)
            {
                float s = HandleUtility.GetHandleSize(world[i]) * 0.12f;
                Handles.color = (editPoint == i) ? Color.yellow : Color.white;
                if (Handles.Button(world[i], Quaternion.identity, s, s, Handles.SphereHandleCap))
                    editPoint = i;
                Handles.Label(world[i] + Vector3.right * s * 1.5f, labels[i]);
            }

            Handles.color = Color.yellow;
            Handles.DrawDottedLine(world[0], world[1], 3.0f);

            if (editPoint >= 0 && editPoint < 2)
                world[editPoint] = Handles.PositionHandle(world[editPoint], Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(link, "Edit NavMesh Link");
                local[0] = tr.InverseTransformPoint(world[0]);
                local[1] = tr.InverseTransformPoint(world[1]);
                link.SetEditPoints(local);
                EditorUtility.SetDirty(link);
            }

            editPointPerLink[link] = editPoint;
        }

        void OnSceneGUI()
        {
            // While the tool is active it draws the editable handles; here we only show a light preview
            // so the link stays visible and pickable when the tool is off.
            if (ToolManager.activeToolType == typeof(NavMeshLink2dTool)) return;

            var link = target as NavMeshLink2d;
            if (link == null) return;

            Handles.color = new Color(1.0f, 0.6f, 0.1f, 1.0f);
            Vector3 a = link.worldStart;
            Vector3 b = link.worldEnd;
            Handles.DrawDottedLine(a, b, 3.0f);
            float s = HandleUtility.GetHandleSize(a) * 0.08f;
            Handles.SphereHandleCap(0, a, Quaternion.identity, s, EventType.Repaint);
            Handles.SphereHandleCap(0, b, Quaternion.identity, s, EventType.Repaint);
        }
    }
}

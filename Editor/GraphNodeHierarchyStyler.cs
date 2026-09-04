#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GraphNodeHierarchyStyler
{
    static GraphNodeHierarchyStyler()
    {
        EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyGUI;
    }

    private static void OnHierarchyGUI(EntityId instanceID, Rect selectionRect)
    {
        var obj = EditorUtility.EntityIdToObject(instanceID) as GameObject;
        if (obj == null) return;

        var node = obj.GetComponent<UC.GraphNodeComponent>();
        if (node == null) return;

        var editor = obj.GetComponentInParent<UC.GraphEditor>();
        if (editor == null) return;
        if (!editor.enabled) return;

        // Only draw a simple line across the default label — clean strikethrough
        float y = selectionRect.y + selectionRect.height * 0.5f;
        Handles.color = new Color(1.0f, 0.0f, 0.0f, 0.5f);
        Handles.DrawLine(new Vector2(selectionRect.xMin, y), new Vector2(selectionRect.xMax, y));
    }


}
#endif

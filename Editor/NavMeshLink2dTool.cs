using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace UC.Editor
{
    [EditorTool("Edit NavMesh Link", typeof(NavMeshLink2d))]
    public class NavMeshLink2dTool : EditorTool
    {
        GUIContent m_IconContent;

        void OnEnable()
        {
            // Prefer a project icon if one exists, otherwise fall back to a built-in tool icon.
            var img = AssetUtils.FindAssetByName<Texture2D>("PathStraight");

            m_IconContent = new GUIContent()
            {
                image = img,
                text = "Edit NavMesh Link",
                tooltip = "Edit NavMeshLink2d endpoints"
            };

            if (m_IconContent.image == null)
                m_IconContent.image = EditorGUIUtility.IconContent("MoveTool").image;
        }

        public override GUIContent toolbarIcon => m_IconContent;

        public override void OnToolGUI(EditorWindow window)
        {
            if (target is NavMeshLink2d link)
            {
                NavMeshLink2dEditor.DrawLinkEditingUI(link);
            }
        }
    }
}

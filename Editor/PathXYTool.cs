using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace UC
{
    [EditorTool("Edit Path XY", typeof(PathXY))]
    public class PathXYTool : EditorTool
    {
        GUIContent m_IconContent;

        void OnEnable()
        {
            // You can replace the icon path with your own
            var img = AssetUtils.FindAssetByName<Texture2D>("PathCurved");

            m_IconContent = new GUIContent()
            {
                image = img,
                text = "Edit Path XY",
                tooltip = "Edit PathXY Curve"
            };
        }

        public override GUIContent toolbarIcon => m_IconContent;

        public override void OnToolGUI(EditorWindow window)
        {
            if (target is PathXY path)
            {
                PathXYEditor.DrawPathEditingUI(path);
            }
        }
    }
}

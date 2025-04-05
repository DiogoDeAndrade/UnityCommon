using UnityEditor;
using UnityEngine;

namespace UC
{
    public static class ColorPaletteEditorUtility
    {
        [MenuItem("Assets/Unity Common Tools/Palette/Create Editable Palette", priority = 2000)]
        public static void DuplicateEditablePalette()
        {
            // Get the selected asset
            var selectedObject = Selection.activeObject as ColorPalette;
            if (selectedObject == null)
            {
                Debug.LogError("No ColorPalette asset selected.");
                return;
            }

            // Create a copy of the selected palette
            string path = AssetDatabase.GetAssetPath(selectedObject);
            string directory = System.IO.Path.GetDirectoryName(path);
            string newPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{selectedObject.name}_Editable.asset");

            ColorPalette editableCopy = ScriptableObject.CreateInstance<ColorPalette>();
            var colors = selectedObject.GetColors();
            if (colors != null)
            {
                foreach (var c in colors)
                {
                    editableCopy.Add(c.name, c.color);
                }
            }

            // Save the new asset
            AssetDatabase.CreateAsset(editableCopy, newPath);
            AssetDatabase.SaveAssets();

            // Focus on the new asset
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = editableCopy;

            Debug.Log($"Created editable copy of palette at: {newPath}");
        }

        [MenuItem("Assets/Unity Common Tools/Palette/Create Editable Palette", true)]
        public static bool ValidateDuplicateEditablePalette()
        {
            var selectedObject = Selection.activeObject as ColorPalette;
            if (selectedObject == null) return false;

            return true;
        }
    }
}
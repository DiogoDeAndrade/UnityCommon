using UnityEditor;
using UnityEngine;
using System.Linq;

namespace UC
{

    public class ExtractPaletteTool : MonoBehaviour
    {
        private enum ExtractionMode
        {
            Full,
            Top,
            Distance,
            KMeans,
            Adaptive
        }

        private struct Settings
        {
            public Settings(ExtractionMode extractionMode, int maxColors = 8, float distanceTolerance = 0.1f)
            {
                this.extractionMode = extractionMode;
                this.maxColors = maxColors;
                this.distanceTolerance = distanceTolerance;
            }

            public ExtractionMode extractionMode;
            public int maxColors;
            public float distanceTolerance;
        }

        private class PaletteExtractionSettings : EditorWindow
        {
            private const string EXTRACTION_MODE_PREF = "PaletteExtractor_ExtractionMode";
            private const string MAX_COLORS_PREF = "PaletteExtractor_MaxColors";
            private const string DISTANCE_TOLERANCE_PREF = "PaletteExtractor_DistanceTolerance";

            private Settings settings;
            private bool initialized = false;
            private System.Action<bool, Settings> onComplete;

            public static void ShowWindow(System.Action<bool, Settings> onComplete)
            {
                var window = GetWindow<PaletteExtractionSettings>(true, "Palette Extraction Settings");
                window.onComplete = onComplete;
                window.minSize = new Vector2(300, 150);
                window.maxSize = new Vector2(400, 200);
                window.Show();
            }

            private void LoadPrefs()
            {
                if (!initialized)
                {
                    settings = new(
                        (ExtractionMode)EditorPrefs.GetInt(EXTRACTION_MODE_PREF, (int)ExtractionMode.Distance),
                        EditorPrefs.GetInt(MAX_COLORS_PREF, 8),
                        EditorPrefs.GetFloat(DISTANCE_TOLERANCE_PREF, 0.1f)
                    );
                    initialized = true;
                }
            }

            private void SavePrefs()
            {
                EditorPrefs.SetInt(EXTRACTION_MODE_PREF, (int)settings.extractionMode);
                EditorPrefs.SetInt(MAX_COLORS_PREF, settings.maxColors);
                EditorPrefs.SetFloat(DISTANCE_TOLERANCE_PREF, settings.distanceTolerance);
            }

            private void ResetToDefaults()
            {
                settings.extractionMode = ExtractionMode.Distance;
                settings.maxColors = 8;
                settings.distanceTolerance = 0.1f;
            }

            private void OnGUI()
            {
                LoadPrefs();

                EditorGUILayout.Space(10);

                // Extraction Mode
                EditorGUI.BeginChangeCheck();
                settings.extractionMode = (ExtractionMode)EditorGUILayout.EnumPopup("Extraction Mode", settings.extractionMode);

                EditorGUILayout.Space(10);

                // Mode-specific settings
                switch (settings.extractionMode)
                {
                    case ExtractionMode.Full:
                        break;

                    case ExtractionMode.Top:
                    case ExtractionMode.KMeans:
                    case ExtractionMode.Adaptive:
                        settings.maxColors = EditorGUILayout.IntSlider("Maximum Colors", settings.maxColors, 2, 256);
                        break;

                    case ExtractionMode.Distance:
                        settings.distanceTolerance = EditorGUILayout.Slider("Color Distance Tolerance", settings.distanceTolerance, 0f, 1f);
                        break;
                }

                EditorGUILayout.Space(20);

                // Buttons
                EditorGUILayout.BeginHorizontal();

                // Reset button
                if (GUILayout.Button("Reset to Defaults", GUILayout.Width(120)))
                {
                    ResetToDefaults();
                }

                GUILayout.FlexibleSpace();

                // Cancel button
                if (GUILayout.Button("Cancel", GUILayout.Width(80)))
                {
                    onComplete?.Invoke(false, settings);
                    Close();
                }

                // OK button
                if (GUILayout.Button("OK", GUILayout.Width(80)))
                {
                    SavePrefs();
                    onComplete?.Invoke(true, settings);
                    Close();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        static ProbList<Color> allColors;

        [MenuItem("Assets/Unity Common Tools/Palette/Extract palette")]
        private static void ExtractPalette()
        {
            PaletteExtractionSettings.ShowWindow((confirmed, settings) =>
            {
                if (confirmed)
                {
                    // Create new palette asset
                    var palette = ScriptableObject.CreateInstance<ColorPalette>();

                    // Get the object that was right-clicked
                    string[] guids = Selection.assetGUIDs;
                    if (guids.Length == 0)
                    {
                        Debug.LogError("No asset selected");
                        return;
                    }

                    allColors = new();

                    foreach (string guid in guids)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (obj != null)
                        {
                            ProcessSelectedObject(palette, obj, settings);
                        }
                    }

                    // Ignore colors with alpha
                    allColors.RemoveAll((c) => c.a < 0.1f);

                    // Create palette from histogram
                    if (settings.extractionMode == ExtractionMode.Full)
                    {
                        Color tmp = Color.black;

                        foreach (var (pixel, weight) in allColors)
                        {
                            if (!palette.GetColor(pixel, 0, false, ref tmp))
                            {
                                palette.Add(pixel);
                            }
                        }
                    }
                    else if (settings.extractionMode == ExtractionMode.Top)
                    {
                        Color tmp = Color.black;

                        foreach (var (pixel, weight) in allColors.GetTopN(settings.maxColors))
                        {
                            if (!palette.GetColor(pixel, settings.distanceTolerance, false, ref tmp))
                            {
                                palette.Add(pixel);
                            }
                        }
                    }
                    else if (settings.extractionMode == ExtractionMode.Distance)
                    {
                        Color tmp = Color.black;

                        foreach (var (pixel, weight) in allColors)
                        {
                            if (!palette.GetColor(pixel, settings.distanceTolerance, false, ref tmp))
                            {
                                palette.Add(pixel);
                            }
                        }
                    }
                    else if (settings.extractionMode == ExtractionMode.KMeans)
                    {
                        Color tmp = Color.black;

                        var kMeans = allColors.KMeans(settings.maxColors, (c1, c2) => c1.DistanceRGB(c2), 64);
                        foreach (var c in kMeans)
                        {
                            palette.Add(c);
                        }
                    }
                    else if (settings.extractionMode == ExtractionMode.Adaptive)
                    {
                        Color tmp = Color.black;

                        var colors = allColors.GetAdaptiveTopN(settings.maxColors, (c1, c2) => c1.DistanceRGB(c2));
                        foreach (var c in colors)
                        {
                            palette.Add(c);
                        }
                    }

                    palette.SortColors(ColorPalette.SortMode.Saturation);

                    // Prompt user for save location
                    string defaultName = "New Color Palette";
                    string defaultPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    if (string.IsNullOrEmpty(defaultPath))
                    {
                        defaultPath = "Assets";
                    }
                    else if (!AssetDatabase.IsValidFolder(defaultPath))
                    {
                        defaultPath = System.IO.Path.GetDirectoryName(defaultPath);
                    }

                    string path = EditorUtility.SaveFilePanelInProject(
                        "Save Color Palette",
                        defaultName,
                        "asset",
                        "Choose where to save the color palette",
                        defaultPath
                    );

                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.CreateAsset(palette, path);
                        AssetDatabase.SaveAssets();
                        Selection.activeObject = palette;
                        EditorGUIUtility.PingObject(palette);
                    }
                }
            });
        }

        private static void ProcessSelectedObject(ColorPalette palette, UnityEngine.Object obj, Settings settings)
        {
            string path = AssetDatabase.GetAssetPath(obj);

            // If it's a folder, process all assets inside
            if (AssetDatabase.IsValidFolder(path))
            {
                ProcessFolder(palette, path, settings);
                return;
            }

            // Handle Sprites and Textures
            if (obj is Sprite sprite)
            {
                ExtractPaletteFromTexture(palette, sprite.texture, path, settings);
            }
            else if (obj is Texture2D texture)
            {
                ExtractPaletteFromTexture(palette, texture, path, settings);
            }
        }

        private static void ProcessFolder(ColorPalette palette, string folderPath, Settings settings)
        {
            // Get all files in the folder
            string[] assetPaths = System.IO.Directory.GetFiles(folderPath, "*.*", System.IO.SearchOption.AllDirectories)
                .Where(file => file.ToLower().EndsWith(".png") ||
                              file.ToLower().EndsWith(".jpg") ||
                              file.ToLower().EndsWith(".jpeg") ||
                              file.ToLower().EndsWith(".tga"))
                .ToArray();

            foreach (string assetPath in assetPaths)
            {
                // Convert to Unity path format
                string unityPath = assetPath.Replace('\\', '/');

                // Load the asset
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(unityPath);
                if (asset != null)
                {
                    ProcessSelectedObject(palette, asset, settings);
                }
            }

            // Process subfolders
            string[] subFolders = AssetDatabase.GetSubFolders(folderPath);
            foreach (string subFolder in subFolders)
            {
                ProcessFolder(palette, subFolder, settings);
            }
        }

        private static void ExtractPaletteFromTexture(ColorPalette palette, Texture2D originalTexture, string assetPath, Settings settings)
        {
            // Check if we can read the texture
            if (!originalTexture.isReadable)
            {
                // Load the texture settings
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer != null)
                {
                    // Store original settings
                    var originalReadable = importer.isReadable;
                    var originalTextureType = importer.textureType;

                    try
                    {
                        // Modify settings to make it readable
                        importer.isReadable = true;
                        importer.textureType = TextureImporterType.Default;

                        // Apply the settings
                        importer.SaveAndReimport();

                        // Get the readable texture
                        Texture2D readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                        ReadTextureColors(readableTexture, palette, settings);

                        // Restore original settings
                        importer.isReadable = originalReadable;
                        importer.textureType = originalTextureType;
                        importer.SaveAndReimport();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"Failed to process texture {assetPath}: {e.Message}");
                    }
                }
            }
            else
            {
                ReadTextureColors(originalTexture, palette, settings);
            }
        }

        private static void ReadTextureColors(Texture2D texture, ColorPalette palette, Settings settings)
        {
            try
            {
                // Create a temporary RenderTexture to handle different formats
                RenderTexture tempRT = RenderTexture.GetTemporary(
                    texture.width,
                    texture.height,
                    0,
                    RenderTextureFormat.ARGB32);

                // Copy the texture content to the temporary RenderTexture
                Graphics.Blit(texture, tempRT);

                // Store the active RenderTexture
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = tempRT;

                // Create a new readable texture and read the pixels
                Texture2D readableTexture = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, false);
                readableTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
                readableTexture.Apply();

                // Restore the active RenderTexture
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(tempRT);

                // Get all pixels
                Color[] pixels = readableTexture.GetPixels();

                foreach (var pixel in pixels)
                {
                    allColors.Add(pixel, 1.0f);
                }

                // Clean up
                DestroyImmediate(readableTexture);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error reading texture {texture.name}: {e.Message}");
            }
        }

        [MenuItem("Assets/Unity Common Tools/Palette/Extract palette", validate = true)]
        private static bool ExtractPaletteValidation()
        {
            string[] guids = Selection.assetGUIDs;
            if (guids.Length == 0) return false;

            bool containsOnlyImages = true;
            bool containsOnlyFolders = true;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

                // Check if the object is a folder
                if (AssetDatabase.IsValidFolder(path))
                {
                    containsOnlyImages = false;
                }
                else if (obj is Texture || obj is Sprite)
                {
                    containsOnlyFolders = false;
                }
                else
                {
                    // If it's neither a folder nor an image, invalidate both
                    containsOnlyImages = false;
                    containsOnlyFolders = false;
                    break;
                }
            }

            // Return true if either all are images or all are folders
            return containsOnlyImages || containsOnlyFolders;
        }
    }
}
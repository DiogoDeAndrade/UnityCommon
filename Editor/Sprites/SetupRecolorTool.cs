using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System.Collections.Generic;

public class SetupRecolorTool : MonoBehaviour
{
    private struct Settings
    {
        public Settings(ColorPalette palette)
        {
            this.palette = palette;
        }

        public ColorPalette palette;
    }

    private class SetupRecolorConfigWindow : EditorWindow
    {
        private const string PALETTE_PREF = "SetupRecolor_Palette";

        private Settings settings;
        private bool initialized = false;
        private System.Action<bool, Settings> onComplete;

        public static void ShowWindow(System.Action<bool, Settings> onComplete)
        {
            var window = GetWindow<SetupRecolorConfigWindow>(true, "Recolor Setup Settings");
            window.onComplete = onComplete;
            window.minSize = new Vector2(300, 150);
            window.maxSize = new Vector2(400, 200);
            window.Show();
        }

        private void LoadPrefs()
        {
            if (!initialized)
            {
                ColorPalette palette = null;
                var paletteGUID = EditorPrefs.GetString(PALETTE_PREF, "");
                if (!string.IsNullOrEmpty(paletteGUID))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(paletteGUID);
                    palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(assetPath);
                }

                settings = new(palette);
                initialized = true;
            }
        }

        private void SavePrefs()
        {
            if (settings.palette)
            {
                string path = AssetDatabase.GetAssetPath(settings.palette);
                if (!string.IsNullOrEmpty(path))
                {
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    EditorPrefs.SetString(PALETTE_PREF, guid);
                }
                else
                {
                    EditorPrefs.SetString(PALETTE_PREF, "");
                }
            }
            else
            {
                EditorPrefs.SetString(PALETTE_PREF, "");
            }
        }

        private void ResetToDefaults()
        {
            settings.palette = null;
        }

        private void OnGUI()
        {
            LoadPrefs();

            EditorGUILayout.Space(10);

            // Extraction Mode
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Palette", GUILayout.Width(50)); // Text label
            settings.palette = EditorGUILayout.ObjectField(settings.palette, typeof(ColorPalette), false) as ColorPalette;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

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

            var prevEnabled = GUI.enabled;
            GUI.enabled = (settings.palette != null);
            // OK button
            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                SavePrefs();
                onComplete?.Invoke(true, settings);
                Close();
            }
            GUI.enabled = prevEnabled;

            EditorGUILayout.EndHorizontal();
        }
    }

    [MenuItem("Assets/Unity Common Tools/Palette/Setup Recolor")]
    private static void ExtractPalette()
    {
        SetupRecolorConfigWindow.ShowWindow((confirmed, settings) =>
        {
            if (confirmed)
            {
                // Get the object that was right-clicked
                string[] guids = Selection.assetGUIDs;
                if (guids.Length == 0)
                {
                    Debug.LogError("No asset selected");
                    return;
                }


                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                    if (obj != null)
                    {
                        ProcessSelectedObject(obj, settings);
                    }
                }

                AssetDatabase.Refresh();
            }
        });
    }

    private static void ProcessSelectedObject(UnityEngine.Object obj, Settings settings)
    {
        string path = AssetDatabase.GetAssetPath(obj);

        // If it's a folder, process all assets inside
        if (AssetDatabase.IsValidFolder(path))
        {
            ProcessFolder(path, settings);
            return;
        }

        // Handle Sprites and Textures
        if (obj is Sprite sprite)
        {
            ExtractPaletteFromTexture(sprite.texture, path, settings);
        }
        else if (obj is Texture2D texture)
        {
            ExtractPaletteFromTexture(texture, path, settings);
        }
    }

    private static void ProcessFolder(string folderPath, Settings settings)
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
            // Check if this is already a recolor
            if (assetPath.IndexOf("_recolor_") != -1) continue;

            // Convert to Unity path format
            string unityPath = assetPath.Replace('\\', '/');

            // Load the asset
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(unityPath);
            if (asset != null)
            {
                ProcessSelectedObject(asset, settings);
            }
        }

        // Process subfolders
        string[] subFolders = AssetDatabase.GetSubFolders(folderPath);
        foreach (string subFolder in subFolders)
        {
            ProcessFolder(subFolder, settings);
        }
    }

    private static void ExtractPaletteFromTexture(Texture2D originalTexture, string assetPath, Settings settings)
    {
        string newImage = "";

        // Check if we can read the texture
        if (!originalTexture.isReadable)
        {
            // Load the texture settings
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer != null)
            {
                // Store original settings
                var originalReadable = importer.isReadable;

                try
                {
                    // Modify settings to make it readable
                    importer.isReadable = true;

                    // Apply the settings
                    importer.SaveAndReimport();

                    // Get the readable texture
                    Texture2D readableTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    newImage = CreateRemap(readableTexture, assetPath, settings);

                    // Restore original settings
                    importer.isReadable = originalReadable;
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
            newImage = CreateRemap(originalTexture, assetPath, settings);
        }

        if (!string.IsNullOrEmpty(newImage))
        {
            // Ensure the asset database refreshes to recognize the new image
            AssetDatabase.Refresh();

            var newImporter = AssetImporter.GetAtPath(newImage) as TextureImporter;
            var originalImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(originalTexture)) as TextureImporter;

            if (originalImporter != null && newImporter != null)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(newImage);

                // Copy settings
                ApplyImporterSettings(originalImporter, newImporter);

                var otherTextures = originalImporter.secondarySpriteTextures.ToList();
                AddOrChangeTexture(otherTextures, "_PaletteTexture", texture);
                originalImporter.secondarySpriteTextures = otherTextures.ToArray();

                originalImporter.SaveAndReimport();

                // Apply the new settings
                newImporter.SaveAndReimport();
            }
        }
    }

    private static void AddOrChangeTexture(List<SecondarySpriteTexture> otherTextures, string name, Texture2D texture)
    {
        SecondarySpriteTexture secondarySpriteTexture = new SecondarySpriteTexture
        {
            name = name, // Name it as needed
            texture = texture
        };

        // Check if a secondary texture with the same name already exists
        for (int i = 0; i < otherTextures.Count; i++)
        {
            if (otherTextures[i].name == name)
            {
                otherTextures[i] = secondarySpriteTexture;
                return;
            }
        }

        otherTextures.Add(secondarySpriteTexture);
    }

    private static void ApplyImporterSettings(TextureImporter originalImporter, TextureImporter newImporter)
    {
        // Color needs to be "pure"
        newImporter.sRGBTexture = false;

        newImporter.textureType = originalImporter.textureType;
        newImporter.spritePixelsPerUnit = originalImporter.spritePixelsPerUnit;
        newImporter.spriteImportMode = originalImporter.spriteImportMode;
        newImporter.spritePivot = originalImporter.spritePivot;
        newImporter.isReadable = originalImporter.isReadable;
        newImporter.mipmapEnabled = originalImporter.mipmapEnabled;
        newImporter.filterMode = originalImporter.filterMode;
        newImporter.wrapMode = originalImporter.wrapMode;
        newImporter.anisoLevel = originalImporter.anisoLevel;
        newImporter.alphaSource = originalImporter.alphaSource;
        newImporter.textureCompression = originalImporter.textureCompression;
        newImporter.compressionQuality = originalImporter.compressionQuality;
        newImporter.maxTextureSize = originalImporter.maxTextureSize;

        // Copy sprite import settings if it's a sprite or a sprite atlas
        if ((originalImporter.textureType == TextureImporterType.Sprite) &&
            (originalImporter.spriteImportMode != SpriteImportMode.Single))
        {
            Debug.LogError("Probably need to implement this, the tool doesn't support copying Sprite atlas currently.");
        }
    }

    private static string CreateRemap(Texture2D srcTexture, string srcAssetPath, Settings settings)
    {
        try
        {
            // Create a temporary RenderTexture to handle different formats
            RenderTexture tempRT = RenderTexture.GetTemporary(srcTexture.width, srcTexture.height, 0, RenderTextureFormat.ARGB32);

            // Copy the texture content to the temporary RenderTexture
            Graphics.Blit(srcTexture, tempRT);

            // Store the active RenderTexture
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = tempRT;

            // Create a new readable texture and read the pixels
            Texture2D readableTexture = new Texture2D(srcTexture.width, srcTexture.height, TextureFormat.ARGB32, false);
            readableTexture.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
            readableTexture.Apply();

            // Restore the active RenderTexture
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tempRT);

            // Get all pixels
            Color[] pixels = readableTexture.GetPixels();

            // Create a new Texture identical to the original one
            // but for each pixel, get the ID of the color on the settings.palette color palette
            // and set the RGB of the pixel in the new texture as (ID/ColorCount, 0, 0, Alpha).
            // Then save the texture to a file of the same name, but on the settings.outputfolder.
            // Assuming settings.palette has a List<Color> or similar to access colors by index
            Color[] remappedColors = new Color[pixels.Length];

            int colorCount = settings.palette.Count;
            Texture2D remappedTexture = new Texture2D(srcTexture.width, srcTexture.height, TextureFormat.ARGB32, false);

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                int closestColorID = settings.palette.GetIndexClosestColorRGB(pixel);
                float encodedID = (closestColorID + 0.5f) / (float)colorCount;

                // Set remapped pixel to the new texture
                remappedColors[i] = new Color(encodedID, 0, 0, pixel.a);
            }

            remappedTexture.SetPixels(remappedColors);
            remappedTexture.Apply();

            string directory = Path.GetDirectoryName(srcAssetPath);
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(srcAssetPath);
            string extension = Path.GetExtension(srcAssetPath);

            // Save the remapped texture to the output folder
            byte[] bytes = remappedTexture.EncodeToPNG();

            //string outputPath = Path.Combine(settings.outputFolderPath, srcTexture.name + "_remap.png");
            string outputPath = Path.Combine(directory, $"_recolor_{filenameWithoutExtension}{extension}");
            File.WriteAllBytes(outputPath, bytes);

            // Clean up
            DestroyImmediate(remappedTexture);
            DestroyImmediate(readableTexture);

            // Ensure the absolute path starts with the Assets directory path
            if (outputPath.StartsWith(Application.dataPath))
            {
                // Remove the Assets directory path and prepend "Assets/" to create a relative path
                return "Assets" + outputPath.Substring(Application.dataPath.Length);
            }
            else
            {
                return outputPath;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error reading texture {srcTexture.name}: {e.Message}");
        }

        return null;
    }

    [MenuItem("Assets/Unity Common Tools/Palette/Setup Recolor", validate = true)]
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

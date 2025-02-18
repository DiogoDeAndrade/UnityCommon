using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.U2D.Aseprite;

public class ASESpriteTextureExtractor 
{
    [MenuItem("Assets/Unity Common Tools/Extract Texture from Aseprite", true)] // Validation method
    private static bool ValidateAsepriteExtraction()
    {
        return Selection.activeObject != null &&
               AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".aseprite");
    }

    [MenuItem("Assets/Unity Common Tools/Extract Texture from Aseprite")]
    private static void ExtractAsepriteTexture()
    {
        string asepriteFilePath = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (string.IsNullOrEmpty(asepriteFilePath))
        {
            UnityEngine.Debug.LogError("Invalid Aseprite file selection.");
            return;
        }

        string outputPath = Path.Combine(Path.GetDirectoryName(asepriteFilePath),
                                         Path.GetFileNameWithoutExtension(asepriteFilePath) + ".png");

        ExportAsepriteToPNG(asepriteFilePath, outputPath);
    }

    private static void ExportAsepriteToPNG(string asepritePath, string outputPath)
    {
        // Load the Texture2D from the asset path
        Texture2D originalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(asepritePath);

        if (originalTexture == null)
        {
            UnityEngine.Debug.LogError("No texture found in the selected Aseprite file.");
            return;
        }

        // Create a new readable Texture2D
        Texture2D readableTexture = new Texture2D(originalTexture.width, originalTexture.height, TextureFormat.RGBA32, false);

        // Copy pixels from the original texture
        RenderTexture rt = RenderTexture.GetTemporary(originalTexture.width, originalTexture.height);
        Graphics.Blit(originalTexture, rt);
        RenderTexture.active = rt;

        readableTexture.ReadPixels(new Rect(0, 0, originalTexture.width, originalTexture.height), 0, 0);
        readableTexture.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        // Encode the readable texture to PNG
        byte[] pngData = readableTexture.EncodeToPNG();
        if (pngData == null)
        {
            UnityEngine.Debug.LogError("Failed to encode texture to PNG.");
            return;
        }

        // Write PNG file to disk
        File.WriteAllBytes(outputPath, pngData);
        UnityEngine.Debug.Log($"Texture exported as PNG: {outputPath}");

        // Refresh the asset database so Unity detects the new file
        AssetDatabase.Refresh();

        // Clean up the texture
        Object.DestroyImmediate(readableTexture); 
    }
}

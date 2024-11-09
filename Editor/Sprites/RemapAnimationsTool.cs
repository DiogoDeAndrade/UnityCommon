using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class RemapAnimationsTool : MonoBehaviour
{
    public enum ImageSource
    {
        FromDirectory,
        SelectFiles
    }

    [MenuItem("Assets/Tools/Remap Animations")]
    private static void RemapAnimations()
    {
        // Show the modal dialog
        RemapAnimationsDialog.ShowDialog();
    }

    [MenuItem("Assets/Tools/Remap Animations", validate = true)]
    private static bool RemapAnimationsValidation()
    {
        if (Selection.objects.Length > 0)
        {
            foreach (var obj in Selection.objects)
            {
                if (obj.GetType() != typeof(AnimationClip))
                {
                    return false;
                }
            }

            return true;
        }

        return false;
    }

    static Dictionary<Sprite, Sprite> ConversionCache;

    internal static void Run(ImageSource imageSource)
    {
        ConversionCache = new Dictionary<Sprite, Sprite>();

        string folderPath = "";

        if (imageSource == ImageSource.FromDirectory)
        {
            string lastFolderPath = EditorPrefs.GetString("RemapAnimationsTool_LastFolderPath", "");

            folderPath = EditorUtility.OpenFolderPanel("Select Sprite Source Folder", lastFolderPath, "");

            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            // Save the selected folder path for future use
            EditorPrefs.SetString("RemapAnimationsTool_LastFolderPath", folderPath);
        }

        foreach (var obj in Selection.objects)
        {
            Run(obj as AnimationClip, folderPath);
        }
    }

    private static void Run(AnimationClip animationClip, string folderPath)
    {
        // Get all bindings (tracks) in the animation clip
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClip);

        bool modify = false;

        foreach (var binding in bindings)
        {
            // Check if the binding targets a Sprite property
            if (binding.propertyName.Contains("m_Sprite"))
            {
                // Get the current keyframes in this binding
                var keyframes = AnimationUtility.GetObjectReferenceCurve(animationClip, binding);

                for (int i = 0; i < keyframes.Length; i++)
                {
                    var originalSprite = keyframes[i].value as Sprite;
                    if (originalSprite != null)
                    {
                        // Replace the sprite using the GetReplacementSprite function
                        Sprite replacementSprite;
                        if (!ConversionCache.TryGetValue(originalSprite, out replacementSprite))
                        {
                            replacementSprite = GetReplacementSprite(originalSprite, folderPath);
                        }
                        if ((replacementSprite != null) && (replacementSprite != originalSprite))
                        {
                            // Add this to the dictionary, we can cache what we already match
                            ConversionCache.TryAdd(originalSprite, replacementSprite);

                            keyframes[i].value = replacementSprite;
                            modify = true;
                        }
                    }
                }

                // Set the modified keyframes back to the clip
                AnimationUtility.SetObjectReferenceCurve(animationClip, binding, keyframes);
            }
        }

        if (modify)
        {
            // Save the modified AnimationClip
            Debug.Log($"Updated clip {animationClip.name}!");

            EditorUtility.SetDirty(animationClip);
            AssetDatabase.SaveAssets();
        }
        else
        {
            Debug.Log($"Clip {animationClip.name} was not updated!");
        }
    }

    private static Sprite GetReplacementSprite(Sprite originalSprite, string folderPath)
    {
        string originalName = originalSprite.name.ToLower();

        if (folderPath != "")
        {
            // Load all sprite assets in the folder
            string[] spritePaths = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories);
            Sprite[] sprites = spritePaths
                .Select(path =>
                {
                    // Convert the absolute path to a relative path for AssetDatabase
                    string relativePath = "Assets" + path.Substring(Application.dataPath.Length);
                    return AssetDatabase.LoadAssetAtPath<Sprite>(relativePath);
                })
                .Where(sprite => sprite != null)
                .ToArray();

            // Stage 1: Exact name match (case-insensitive)
            var exactMatch = sprites.FirstOrDefault(sprite => sprite.name.Equals(originalName, System.StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null) return exactMatch;

            // Stage 2: Partial matches
            // Sort by preference: match at the beginning, then at the end, and finally anywhere in the name
            var partialMatch = sprites
                .Where(sprite => HasConsecutiveMatch(originalName, sprite.name.ToLower(), Mathf.CeilToInt(originalName.Length * 0.75f)))
                .FirstOrDefault();

            if (partialMatch != null) return partialMatch;
        }

        // Stage 3: No match found, open dialog to select manually
        bool setPath = false;
        if (string.IsNullOrEmpty(folderPath))
        {
            folderPath = EditorPrefs.GetString("RemapAnimationsTool_LastFolderPath", "");
            setPath = true;
        }
        string selectedPath = EditorUtility.OpenFilePanel($"Select Replacement Sprite for {originalName}", folderPath, "png");
        if (!string.IsNullOrEmpty(selectedPath))
        {
            // Load the manually selected sprite
            if (setPath)
            {
                EditorPrefs.SetString("RemapAnimationsTool_LastFolderPath", folderPath);
            }

            string relativePath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            return AssetDatabase.LoadAssetAtPath<Sprite>(relativePath);
        }

        Debug.LogWarning($"No replacement sprite found for: {originalSprite.name}");
        return null; // No replacement found or selected
    }

    // Helper function to check if two strings have at least `minLength` consecutive characters matching
    private static bool HasConsecutiveMatch(string original, string target, int minLength)
    {
        for (int i = 0; i <= original.Length - minLength; i++)
        {
            string substring = original.Substring(i, minLength);
            if (target.Contains(substring))
            {
                return true;
            }
        }
        return false;
    }
}

public class RemapAnimationsDialog : EditorWindow
{
    private RemapAnimationsTool.ImageSource imageSource;

    public static void ShowDialog()
    {
        // Create a new window instance
        var window = CreateInstance<RemapAnimationsDialog>();
        window.titleContent = new GUIContent("Remap Animations");
        window.position = new Rect(Screen.width / 2, Screen.height / 2, 300, 150);
        window.ShowModalUtility();
    }

    private void OnGUI()
    {
        GUILayout.Label("Select Image Source", EditorStyles.boldLabel);

        // Enum selection dropdown
        imageSource = (RemapAnimationsTool.ImageSource)EditorGUILayout.EnumPopup("Image Source", imageSource);

        GUILayout.Space(10);

        // OK and Cancel buttons
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("OK"))
        {
            // Handle OK logic
            Close();

            RemapAnimationsTool.Run(imageSource);
        }

        if (GUILayout.Button("Cancel"))
        {
            Close();
        }
        GUILayout.EndHorizontal();
    }
}

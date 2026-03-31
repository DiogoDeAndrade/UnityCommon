using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UC
{
    [FilePath("ProjectSettings/UnityCommonScreenshotSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ScreenshotSettings : ScriptableSingleton<ScreenshotSettings>
    {
        public Vector2Int resolution = new Vector2Int(1920, 1080);

        // Stored as GlobalObjectId string
        public string cameraGlobalId = "";

        public bool incremental = true;
        public string baseScreenshotName = "screenshot_{0}.png";

        public bool exportAlpha = true;

        public void Save() => Save(true);

        public Camera ResolveCamera()
        {
            if (string.IsNullOrWhiteSpace(cameraGlobalId))
                return null;

            if (!GlobalObjectId.TryParse(cameraGlobalId, out var gid))
                return null;

            var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            return obj as Camera;
        }

        public void SetCamera(Camera cam)
        {
            if (cam == null)
            {
                cameraGlobalId = "";
                return;
            }

            cameraGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(cam).ToString();
        }
    }

    public class ScreenshotOptionsWindow : EditorWindow
    {
        SerializedObject _so;
        SerializedProperty _resolution;
        SerializedProperty _cameraGlobalId;
        SerializedProperty _incremental;
        SerializedProperty _baseScreenshotName;
        SerializedProperty _exportAlpha;

        Camera _cameraObjCache;

        [MenuItem("Unity Common/Screenshot Options")]
        public static void Open()
        {
            var w = GetWindow<ScreenshotOptionsWindow>("Screenshot Options");
            w.minSize = new Vector2(460, 190);
            w.Show();
        }

        void OnEnable()
        {
            _so = new SerializedObject(ScreenshotSettings.instance);

            _resolution = _so.FindProperty(nameof(ScreenshotSettings.resolution));
            _cameraGlobalId = _so.FindProperty(nameof(ScreenshotSettings.cameraGlobalId));
            _incremental = _so.FindProperty(nameof(ScreenshotSettings.incremental));
            _baseScreenshotName = _so.FindProperty(nameof(ScreenshotSettings.baseScreenshotName));
            _exportAlpha = _so.FindProperty(nameof(ScreenshotSettings.exportAlpha));

            _cameraObjCache = ScreenshotSettings.instance.ResolveCamera();
        }

        void OnGUI()
        {
            _so.Update();

            EditorGUILayout.LabelField("Unity Common Screenshot", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            EditorGUILayout.PropertyField(_resolution, new GUIContent("Resolution"));

            EditorGUI.BeginChangeCheck();
            _cameraObjCache = (Camera)EditorGUILayout.ObjectField(
                new GUIContent("Camera (optional)"),
                _cameraObjCache,
                typeof(Camera),
                allowSceneObjects: true);

            if (EditorGUI.EndChangeCheck())
            {
                ScreenshotSettings.instance.SetCamera(_cameraObjCache);
                _cameraGlobalId.stringValue = ScreenshotSettings.instance.cameraGlobalId;
            }

            EditorGUILayout.PropertyField(_exportAlpha, new GUIContent("Export Alpha"));
            EditorGUILayout.PropertyField(_incremental, new GUIContent("Incremental"));
            EditorGUILayout.PropertyField(_baseScreenshotName, new GUIContent("Base screenshot name"));

            EditorGUILayout.Space(8);

            EditorGUILayout.HelpBox(
                "Saves to ../Screenshots (relative to Assets).\n" +
                "Base name can include extension (.png or .exr). If no extension is provided, defaults to .png.\n" +
                "If Incremental is ON, {0} is replaced by an index (auto-finds the next free number).\n" +
                "If Camera is empty, it captures the GameView (Play Mode only).",
                MessageType.Info);

            _so.ApplyModifiedProperties();

            if (GUI.changed)
                ScreenshotSettings.instance.Save();
        }
    }

    public static class ScreenshotTool
    {
        [MenuItem("Unity Common/Take Screenshot _F12")]
        public static void TakeScreenshotMenu()
        {
            TakeScreenshot();
        }

        public static void TakeScreenshot()
        {
            var settings = ScreenshotSettings.instance;

            Vector2Int res = settings.resolution;
            if (res.x <= 0 || res.y <= 0)
            {
                Debug.LogError("Screenshot resolution must be > 0.");
                return;
            }

            // Output folder: ../screenshots relative to Assets
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots"));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Decide extension (.png or .exr) from base name
            string baseNameRaw = string.IsNullOrWhiteSpace(settings.baseScreenshotName)
                ? "screenshot_{0}.png"
                : settings.baseScreenshotName.Trim();

            (string baseNoExt, string ext) = SplitBaseNameAndExtension(baseNameRaw);

            // Determine path (handles incremental naming)
            string path = BuildUniquePath(dir, baseNoExt, settings.incremental, ext);

            Camera cam = settings.ResolveCamera();

            try
            {
                if (cam != null)
                {
                    // Camera render path works in edit mode or play mode
                    RenderCameraToFile(cam, res.x, res.y, path, ext, settings.exportAlpha);
                }
                else
                {
                    // GameView path: Play Mode only
                    if (!Application.isPlaying)
                    {
                        EditorUtility.DisplayDialog(
                            "Take Screenshot",
                            "No Camera is set.\n\nGameView screenshots require Play Mode.\n\n" +
                            "Either enter Play Mode or assign a Camera in Screenshot Options.",
                            "OK");
                        Debug.LogError("GameView screenshot requires Play Mode when no camera is assigned.");
                        return;
                    }

                    ScreenshotRuntimeRunner.RequestGameViewCapture(res.x, res.y, path, ext, settings.exportAlpha);
                }

                Debug.Log($"Screenshot saved: {path}");
                // EditorUtility.RevealInFinder(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static (string baseNoExt, string ext) SplitBaseNameAndExtension(string baseNameRaw)
        {
            // Accept ".png" or ".exr" explicitly. If none, default to ".png".
            string ext = Path.GetExtension(baseNameRaw);
            if (string.IsNullOrEmpty(ext))
                return (baseNameRaw, ".png");

            ext = ext.ToLowerInvariant();
            if (ext != ".png" && ext != ".exr")
            {
                // Unknown extension: treat as "no extension" and append .png
                return (baseNameRaw, ".png");
            }

            string baseNoExt = baseNameRaw.Substring(0, baseNameRaw.Length - ext.Length);
            return (baseNoExt, ext);
        }

        static string BuildUniquePath(string dir, string baseNoExt, bool incremental, string ext)
        {
            string pattern = baseNoExt;

            if (incremental)
            {
                if (!pattern.Contains("{"))
                    pattern += "_{0}";

                for (int i = 0; ; i++)
                {
                    string file = string.Format(pattern, i) + ext;
                    string path = Path.Combine(dir, file);
                    if (!File.Exists(path))
                        return path;
                }
            }
            else
            {
                // Non-incremental: overwrite (and if it contains {0}, format with 0)
                string fileName = pattern.Contains("{0}") ? string.Format(pattern, 0) : pattern;
                return Path.Combine(dir, fileName + ext);
            }
        }

        // -----------------------------
        // Camera capture (works in edit mode)
        // -----------------------------
        static void RenderCameraToFile(Camera cam, int width, int height, string path, string ext, bool exportAlpha)
        {
            var prevRT = RenderTexture.active;
            var prevTarget = cam.targetTexture;

            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                var rtFormat = (ext == ".exr") ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32;

                rt = new RenderTexture(width, height, 24, rtFormat)
                {
                    antiAliasing = 1
                };

                cam.targetTexture = rt;
                RenderTexture.active = rt;

                cam.Render();

                var texFormat = (ext == ".exr") ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
                bool linear = (ext == ".exr");
                tex = new Texture2D(width, height, texFormat, mipChain: false, linear: linear);

                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false, false);

                if (!exportAlpha)
                    ForceOpaqueAlpha(tex);

                WriteTextureToFile(tex, path, ext);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevRT;

                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static void ForceOpaqueAlpha(Texture2D tex)
        {
            // Ensures alpha is 1 for all pixels (both PNG and EXR)
            var pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i].a = 1f;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
        }

        static void WriteTextureToFile(Texture2D tex, string path, string ext)
        {
            byte[] bytes;

            if (ext == ".exr")
            {
                // EXR keeps HDR/float range (if tex is float).
                bytes = tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
            }
            else
            {
                bytes = tex.EncodeToPNG();
            }

            File.WriteAllBytes(path, bytes);
        }
    }
}

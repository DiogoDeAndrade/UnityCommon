using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UC
{

    public class BuildTool : EditorWindow
    {
        private static readonly string buildDefsPath = "Assets/Settings/BuildDefs.asset";
        private BuildDefs buildDefs;
        private string buildLog = "";
        private string butlerPath;

        private SerializedObject buildDefsSerializedObject;

        [MenuItem("Unity Common/Build")]
        public static void OpenBuildTool()
        {
            BuildTool window = GetWindow<BuildTool>("Build Tool");
            window.Show();
        }

        private void OnEnable()
        {
            buildDefs = GetOrCreateBuildDefs();
            if (string.IsNullOrEmpty(buildDefs.version))
            {
                buildDefs.version = PlayerSettings.bundleVersion;
                SaveBuildDefs();
            }

            buildDefsSerializedObject = new SerializedObject(buildDefs);
        }

        private void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Product Name: " + Application.productName, EditorStyles.boldLabel);
            if (GUILayout.Button("Player Settings"))
            {
                SettingsService.OpenProjectSettings("Project/Player");
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Version: " + buildDefs.version, EditorStyles.boldLabel);

            if (GUILayout.Button("Inc. Major")) { IncrementVersion(0); }
            if (GUILayout.Button("Inc. Minor")) { IncrementVersion(1); }
            if (GUILayout.Button("Inc. Rev")) { IncrementVersion(2); }

            // NEW: fetch from Player Settings
            if (GUILayout.Button(new GUIContent("Get Current Version", "Read PlayerSettings.bundleVersion")))
            {
                GetCurrentVersionFromPlayerSettings();
            }

            if (GUILayout.Button(new GUIContent("Update Build Date", "Updates the BuildInfo.txt file")))
            {
                UpdateBuildTimestamp();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("Build Options", EditorStyles.boldLabel);
            if (buildDefs != null)
            {
                EditorGUI.BeginChangeCheck();

                if (buildDefs.projectName == "") buildDefs.projectName = PlayerSettings.productName.ToLower().Replace(" ", "");

                buildDefs.buildWindows = EditorGUILayout.Toggle("Build for Windows", buildDefs.buildWindows);
                buildDefs.buildWeb = EditorGUILayout.Toggle("Build for Web", buildDefs.buildWeb);
                if (buildDefs.anyBuilds)
                {
                    buildDefs.createZipFiles = EditorGUILayout.Toggle("Create Zip Files", buildDefs.createZipFiles);
                }
                if (buildDefs.createZipFiles)
                {
                    buildDefs.uploadToItch = EditorGUILayout.Toggle("Upload to Itch", buildDefs.uploadToItch);
                }
                if (buildDefs.uploadToItch)
                {
                    buildDefs.username = EditorGUILayout.TextField("Itch.io Username", buildDefs.username);
                    buildDefs.projectName = EditorGUILayout.TextField("Itch.io Project Name", buildDefs.projectName);
                }

                GUILayout.Space(10);
                GUILayout.Label("Ignore File Patterns (deleted from build output before zipping)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Patterns are relative to the build root folder.\n" +
                    "Examples:\n" +
                    "  StreamingAssets/Models/*.gguf\n" +
                    "  StreamingAssets/Models/LargeModel.gguf\n" +
                    "  *.gguf",
                    MessageType.Info);

                buildDefsSerializedObject.Update();
                SerializedProperty ignoreListProp = buildDefsSerializedObject.FindProperty("ignoreFilePatterns");
                EditorGUILayout.PropertyField(ignoreListProp, new GUIContent("Patterns"), true);
                if (buildDefsSerializedObject.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(buildDefs);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    SaveBuildDefs();
                }
            }
            else
            {
                GUILayout.Label("BuildDefs asset not found!", EditorStyles.helpBox);
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Build"))
            {
                buildLog = ""; // Clear log before new build
                UpdateBuildTimestamp();
                BuildGame();
            }

            GUILayout.Space(10);
            GUILayout.Label("Build Log:", EditorStyles.boldLabel);
            buildLog = EditorGUILayout.TextArea(buildLog, GUILayout.ExpandHeight(true));
        }

        private void IncrementVersion(int part)
        {
            string[] parts = buildDefs.version.Split('.');
            int[] numbers = new int[] { 0, 0, 0 };

            for (int i = 0; i < parts.Length && i < 3; i++)
            {
                int.TryParse(parts[i], out numbers[i]);
            }

            numbers[part]++;
            if (part == 0) { numbers[1] = 0; numbers[2] = 0; }
            else if (part == 1) { numbers[2] = 0; }

            buildDefs.version = $"{numbers[0]}.{numbers[1]}.{numbers[2]}";
            PlayerSettings.bundleVersion = buildDefs.version;
            SaveBuildDefs();

            UnityEngine.Debug.Log("New Version: " + buildDefs.version);
        }

        private void GetCurrentVersionFromPlayerSettings()
        {
            buildDefs.version = PlayerSettings.bundleVersion;
            SaveBuildDefs();
            Repaint();
            UnityEngine.Debug.Log("Fetched version from Player Settings: " + buildDefs.version);
        }

        void UpdateBuildTimestamp()
        {
            string path = "Assets/Resources/BuildInfo.txt";
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, timestamp);
            AssetDatabase.Refresh();
        }

        private void BuildGame()
        {
            string productName = Application.productName;
            string buildFolder = "Builds/";

            if (Directory.Exists(buildFolder))
            {
                foreach (var file in Directory.GetFiles(buildFolder, "*.zip"))
                {
                    File.Delete(file);
                    Log("Deleted old zip file: " + file);
                }
            }

            if (!Directory.Exists(buildFolder))
            {
                Directory.CreateDirectory(buildFolder);
            }

            if (buildDefs.buildWindows)
            {
                string windowsPath = buildFolder + productName + "Windows/";
                BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, windowsPath + productName + ".exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
                Log("Windows build completed: " + windowsPath);

                DeleteIgnoredStreamingAssets(windowsPath);

                string burstDebugPath = windowsPath + productName + "_BurstDebugInformation_DoNotShip";
                if (Directory.Exists(burstDebugPath))
                {
                    Directory.Delete(burstDebugPath, true);
                    Log("Deleted Burst Debug Information folder: " + burstDebugPath);
                }

                if (buildDefs.createZipFiles)
                {
                    CreateZip(buildFolder, productName + "Windows", buildFolder + productName + "Windows_v" + buildDefs.version + ".zip");

                    string zipPath = buildFolder + productName + "Windows_v" + buildDefs.version + ".zip";
                    if (buildDefs.uploadToItch)
                    {
                        UploadToItch(zipPath, "windows");
                    }
                }
            }

            if (buildDefs.buildWeb)
            {
                string webPath = buildFolder + productName + "Web/";
                BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, webPath, BuildTarget.WebGL, BuildOptions.None);
                Log("WebGL build completed: " + webPath);

                DeleteIgnoredStreamingAssets(webPath);

                if (buildDefs.createZipFiles)
                {
                    CreateZip(buildFolder, productName + "Web", buildFolder + productName + "Web_v" + buildDefs.version + ".zip");

                    string zipPath = buildFolder + productName + "Web_v" + buildDefs.version + ".zip";
                    if (buildDefs.uploadToItch)
                    {
                        UploadToItch(zipPath, "html5");
                    }
                }
            }
        }

        private void CreateZip(string parentFolder, string folderName, string zipFilePath)
        {
            string fullFolderPath = Path.Combine(parentFolder, folderName);
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }
            ZipFile.CreateFromDirectory(fullFolderPath, zipFilePath, System.IO.Compression.CompressionLevel.Optimal, true);
            Log("Created zip file: " + zipFilePath);
        }

        private void DeleteIgnoredStreamingAssets(string buildRootPath)
        {
            if (buildDefs == null || buildDefs.ignoreFilePatterns == null || buildDefs.ignoreFilePatterns.Count == 0)
                return;

            // 1) Try Standalone layout: <buildRoot>/<ProductName>_Data/StreamingAssets
            string dataFolderName = Application.productName + "_Data";
            string standaloneStreamingAssets = Path.Combine(
                buildRootPath,
                dataFolderName,
                "StreamingAssets"
            );

            // 2) Try WebGL layout: <buildRoot>/StreamingAssets
            string webglStreamingAssets = Path.Combine(buildRootPath, "StreamingAssets");

            string streamingAssetsRoot = null;
            if (Directory.Exists(standaloneStreamingAssets))
                streamingAssetsRoot = standaloneStreamingAssets;
            else if (Directory.Exists(webglStreamingAssets))
                streamingAssetsRoot = webglStreamingAssets;

            if (streamingAssetsRoot == null)
            {
                Log($"No StreamingAssets folder found under '{buildRootPath}'. Skipping ignore step.");
                return;
            }

            Log($"Using StreamingAssets root: {streamingAssetsRoot}");

            foreach (var patternRaw in buildDefs.ignoreFilePatterns)
            {
                if (string.IsNullOrWhiteSpace(patternRaw))
                    continue;

                string pattern = patternRaw.Replace('\\', '/').Trim();
                string dirPart = Path.GetDirectoryName(pattern)?.Replace('\\', '/');
                string filePart = Path.GetFileName(pattern);

                if (string.IsNullOrEmpty(filePart))
                    continue;

                // No directory part -> search whole StreamingAssets subtree
                if (string.IsNullOrEmpty(dirPart) || dirPart == ".")
                {
                    try
                    {
                        var files = Directory.GetFiles(streamingAssetsRoot, filePart, SearchOption.AllDirectories);
                        foreach (var f in files)
                            TryDeleteStreamingAsset(f);
                    }
                    catch (Exception e)
                    {
                        Log($"Error searching pattern '{pattern}' in '{streamingAssetsRoot}': {e.Message}");
                    }
                }
                else
                {
                    // Directory part -> treat as subfolder of StreamingAssets
                    string targetDir = Path.Combine(streamingAssetsRoot, dirPart.Replace('/', Path.DirectorySeparatorChar));
                    if (!Directory.Exists(targetDir))
                        continue;

                    try
                    {
                        var files = Directory.GetFiles(targetDir, filePart, SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                            TryDeleteStreamingAsset(f);
                    }
                    catch (Exception e)
                    {
                        Log($"Error searching pattern '{pattern}' in '{targetDir}': {e.Message}");
                    }
                }
            }
        }

        private void TryDeleteStreamingAsset(string path)
        {
            try
            {
                File.Delete(path);
                Log("Deleted ignored StreamingAsset: " + path);
            }
            catch (Exception e)
            {
                Log($"Failed to delete ignored StreamingAsset '{path}': {e.Message}");
            }
        }

        private void Log(string message)
        {
            message = CleanLogOutput(message);

            buildLog += message + "\n";
            UnityEngine.Debug.Log(message);
        }

        public static string CleanLogOutput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            StringBuilder cleanedString = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (c == '\0' || c == '\r')
                    continue; // Remove null and carriage return

                if (c >= 32 && c <= 126 || c == '\n' || c == '\t' || c == ' ')
                {
                    cleanedString.Append(c);
                }
            }

            return cleanedString.ToString();
        }

        private BuildDefs GetOrCreateBuildDefs()
        {
            BuildDefs asset = AssetDatabase.LoadAssetAtPath<BuildDefs>(buildDefsPath);
            if (asset == null)
            {
                string directory = Path.GetDirectoryName(buildDefsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                asset = CreateInstance<BuildDefs>();
                asset.version = "1.0.0";
                AssetDatabase.CreateAsset(asset, buildDefsPath);
                AssetDatabase.SaveAssets();
                Log("Created BuildDefs at: " + buildDefsPath);
            }
            return asset;
        }

        private void SaveBuildDefs()
        {
            EditorUtility.SetDirty(buildDefs);
            AssetDatabase.SaveAssets();
        }

        private void UploadToItch(string zipFilePath, string channel)
        {
            butlerPath = EditorPrefs.GetString("ButlerPath", "");
            if (string.IsNullOrEmpty(butlerPath))
            {
                butlerPath = EditorUtility.OpenFilePanel("Select Butler Executable", "", "exe");
                if (!string.IsNullOrEmpty(butlerPath))
                {
                    EditorPrefs.SetString("ButlerPath", butlerPath);
                }
            }

            if (string.IsNullOrEmpty(butlerPath) || string.IsNullOrEmpty(buildDefs.username))
            {
                Log("Butler path or username not set. Cannot upload to Itch.io.");
                return;
            }

            string productName = buildDefs.projectName;
            string command = $"\"{butlerPath}\" push \"{zipFilePath}\" {buildDefs.username}/{productName}:{channel} --userversion {buildDefs.version}";
            Log("Executing: " + command);

            Process process = new Process();
            process.StartInfo.FileName = butlerPath;
            process.StartInfo.Arguments = $"push \"{zipFilePath}\" {buildDefs.username}/{productName}:{channel} --userversion {buildDefs.version}";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Log(output);
        }
    }
}
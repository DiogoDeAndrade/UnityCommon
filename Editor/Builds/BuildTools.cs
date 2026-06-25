using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.Text.RegularExpressions;

namespace UC
{

    public class BuildTool : EditorWindow
    {
        private static readonly string buildDefsPath = "Assets/Settings/BuildDefs.asset";
        private BuildDefs buildDefs;
        private string buildLog = "";
        private Vector2 buildLogScroll;
        private string butlerPath;

        protected   bool butlerUploadRunning;
        private     float butlerUploadProgress;
        private     string butlerUploadTitle = "";
        private     string butlerUploadInfo = "";

        private SerializedObject buildDefsSerializedObject;

        [MenuItem("Unity Common/Build", priority = -5)]
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

                GUILayout.Space(5);

                buildDefs.overrideGraphicsSettings = EditorGUILayout.Toggle(new GUIContent("Override Graphics Settings", "Temporarily override the active Render Pipeline Asset during each build."), buildDefs.overrideGraphicsSettings);

                if (buildDefs.overrideGraphicsSettings)
                {
                    EditorGUI.indentLevel++;

                    buildDefs.windowsRenderPipelineAsset = (RenderPipelineAsset)EditorGUILayout.ObjectField(new GUIContent("Windows URP Asset", "If null, the current/default render pipeline asset is used."), buildDefs.windowsRenderPipelineAsset, typeof(RenderPipelineAsset), false);
                    buildDefs.webGLRenderPipelineAsset = (RenderPipelineAsset)EditorGUILayout.ObjectField(new GUIContent("WebGL URP Asset", "If null, the current/default render pipeline asset is used."), buildDefs.webGLRenderPipelineAsset, typeof(RenderPipelineAsset), false);

                    EditorGUI.indentLevel--;
                }

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
                ClearBuildLog();
                UpdateBuildTimestamp();
                BuildGame();
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Log:", EditorStyles.boldLabel);

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                GUILayout.EndHorizontal();
                ClearBuildLog();
                return;
            }

            GUILayout.EndHorizontal();

            GUIStyle logStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                alignment = TextAnchor.UpperLeft
            };

            // Account for vertical scrollbar and padding.
            float logTextWidth = Mathf.Max(100.0f, position.width - 45.0f);

            // Add extra padding because CalcHeight can underestimate TextArea height slightly,
            // especially with wrapped lines and IMGUI padding.
            float logTextHeight = logStyle.CalcHeight(
                new GUIContent(buildLog),
                logTextWidth
            ) + 40.0f;

            // This is the important part:
            // the scroll view can expand to remaining window space,
            // while the inner text area has its real content height.
            buildLogScroll = EditorGUILayout.BeginScrollView(
                buildLogScroll,
                false, // alwaysShowHorizontal
                true,  // alwaysShowVertical
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true)
            );

            EditorGUI.BeginDisabledGroup(true);

            EditorGUILayout.TextArea(
                buildLog,
                logStyle,
                GUILayout.Width(logTextWidth),
                GUILayout.Height(logTextHeight)
            );

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();
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
            BuildTarget previousBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup previousBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;

            try
            {
                BuildGameInternal();
            }
            finally
            {
                RestoreInitialBuildTarget(previousBuildTargetGroup, previousBuildTarget);
            }
        }

        private void RestoreInitialBuildTarget(BuildTargetGroup targetGroup, BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                Log($"Build target already restored: {target}");
                return;
            }

            Log($"Restoring initial build target: {target}");

            bool restored = EditorUserBuildSettings.SwitchActiveBuildTarget(targetGroup, target);

            if (!restored)
            {
                Log($"Failed to restore initial build target: {target}");
            }
        }

        private void BuildGameInternal()
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
                string windowsPath = buildFolder + productName + "_Windows/";
                BuildPlayerWithOptionalGraphicsOverride(EditorBuildSettings.scenes, windowsPath + productName + ".exe", BuildTarget.StandaloneWindows64, BuildOptions.None, buildDefs.windowsRenderPipelineAsset );
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
                    CreateZip(buildFolder, productName + "_Windows", buildFolder + productName + "_Windows_v" + buildDefs.version + ".zip");

                    string zipPath = buildFolder + productName + "_Windows_v" + buildDefs.version + ".zip";
                    if (buildDefs.uploadToItch)
                    {
                        UploadToItch(zipPath, "windows");
                    }
                }
            }

            if (buildDefs.buildWeb)
            {
                string webPath = buildFolder + productName + "_Web/";
                BuildPlayerWithOptionalGraphicsOverride(EditorBuildSettings.scenes, webPath, BuildTarget.WebGL, BuildOptions.None, buildDefs.webGLRenderPipelineAsset);
                Log("WebGL build completed: " + webPath);

                DeleteIgnoredStreamingAssets(webPath);

                if (buildDefs.createZipFiles)
                {
                    CreateZip(buildFolder, productName + "_Web", buildFolder + productName + "_Web_v" + buildDefs.version + ".zip");

                    string zipPath = buildFolder + productName + "_Web_v" + buildDefs.version + ".zip";
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

            if (string.IsNullOrWhiteSpace(message))
                return;

            buildLog += message + "\n";

            // Keep log from becoming absurdly huge.
            const int maxLogLength = 40000;
            if (buildLog.Length > maxLogLength)
            {
                buildLog = buildLog.Substring(buildLog.Length - maxLogLength);
            }

            buildLogScroll.y = float.MaxValue;

            UnityEngine.Debug.Log(message);
            Repaint();
        }

        public static string CleanLogOutput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove ANSI terminal escape sequences.
            input = System.Text.RegularExpressions.Regex.Replace(
                input,
                @"\x1B\[[0-?]*[ -/]*[@-~]",
                ""
            );

            // Remove Butler/progress-bar mojibake caused by box-drawing chars being decoded badly.
            input = System.Text.RegularExpressions.Regex.Replace(
                input,
                @"Ô[\u0080-\u00FFA-Za-z0-9]+",
                ""
            );

            StringBuilder cleanedString = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (c == '\0')
                    continue;

                // Treat carriage return as newline-ish, because CLI progress bars use it.
                // But do not let it create thousands of progress lines later.
                if (c == '\r')
                {
                    cleanedString.Append('\n');
                    continue;
                }

                if (c == '\n' || c == '\t')
                {
                    cleanedString.Append(c);
                    continue;
                }

                if (!char.IsControl(c))
                {
                    cleanedString.Append(c);
                }
            }

            string result = cleanedString.ToString();

            // Collapse excessive blank lines.
            result = System.Text.RegularExpressions.Regex.Replace(result, @"\n{3,}", "\n\n");

            return result.Trim();
        }

        private void ClearBuildLog()
        {
            buildLog = "";
            buildLogScroll = Vector2.zero;

            // Clear focus from SelectableLabel/TextArea internal editor state.
            GUI.FocusControl(null);
            GUIUtility.keyboardControl = 0;
            GUIUtility.hotControl = 0;

            Repaint();
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

        private void BuildPlayerWithOptionalGraphicsOverride(
            EditorBuildSettingsScene[] scenes,
            string locationPathName,
            BuildTarget buildTarget,
            BuildOptions buildOptions,
            RenderPipelineAsset overrideRenderPipelineAsset)
        {
            RenderPipelineAsset previousDefaultRenderPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset previousQualityRenderPipeline = QualitySettings.renderPipeline;

            BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);

            bool shouldOverride =
                buildDefs.overrideGraphicsSettings &&
                overrideRenderPipelineAsset != null;

            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != buildTarget)
                {
                    Log($"Switching active build target to {buildTarget}...");

                    bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                        buildTargetGroup,
                        buildTarget
                    );

                    if (!switched)
                    {
                        Log($"Failed to switch active build target to {buildTarget}. Build aborted.");
                        return;
                    }
                }

                if (shouldOverride)
                {
                    GraphicsSettings.defaultRenderPipeline = overrideRenderPipelineAsset;
                    QualitySettings.renderPipeline = overrideRenderPipelineAsset;

                    Log($"Temporarily set render pipeline for {buildTarget}: {overrideRenderPipelineAsset.name}");
                }
                else if (buildDefs.overrideGraphicsSettings)
                {
                    Log($"No render pipeline override set for {buildTarget}. Using current/default graphics settings.");
                }

                BuildPipeline.BuildPlayer(
                    scenes,
                    locationPathName,
                    buildTarget,
                    buildOptions
                );
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = previousDefaultRenderPipeline;
                QualitySettings.renderPipeline = previousQualityRenderPipeline;

                Log($"Restored render pipeline settings after {buildTarget} build.");
            }
        }

        private bool UploadToItch(string uploadPath, string channel)
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

            if (string.IsNullOrEmpty(butlerPath) || !File.Exists(butlerPath))
            {
                Log("Butler path is not set or does not exist. Cannot upload to Itch.io.");
                return false;
            }

            if (string.IsNullOrEmpty(buildDefs.username))
            {
                Log("Itch.io username is not set. Cannot upload to Itch.io.");
                return false;
            }

            if (string.IsNullOrEmpty(buildDefs.projectName))
            {
                Log("Itch.io project name is not set. Cannot upload to Itch.io.");
                return false;
            }

            if (!File.Exists(uploadPath) && !Directory.Exists(uploadPath))
            {
                Log("Upload path does not exist: " + uploadPath);
                return false;
            }

            string itchTarget = $"{buildDefs.username}/{buildDefs.projectName}:{channel}";
            string arguments = $"push \"{uploadPath}\" {itchTarget} --userversion {buildDefs.version}";

            Log("Uploading to Itch.io:");
            Log("  Path: " + uploadPath);
            Log("  Target: " + itchTarget);
            Log("  Version: " + buildDefs.version);

            butlerUploadRunning = true;
            butlerUploadProgress = 0.0f;
            butlerUploadTitle = $"Uploading {channel} to Itch.io";
            butlerUploadInfo = itchTarget;

            StringBuilder finalOutput = new StringBuilder();
            StringBuilder finalError = new StringBuilder();

            try
            {
                Process process = new Process();
                process.StartInfo.FileName = butlerPath;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                // Important: Butler outputs UTF-8 progress characters.
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

                process.Start();

                System.Threading.Thread stdoutThread = new System.Threading.Thread(() =>
                {
                    ReadButlerStream(process.StandardOutput, finalOutput);
                });

                System.Threading.Thread stderrThread = new System.Threading.Thread(() =>
                {
                    ReadButlerStream(process.StandardError, finalError);
                });

                stdoutThread.Start();
                stderrThread.Start();

                while (!process.HasExited)
                {
                    EditorUtility.DisplayProgressBar(
                        butlerUploadTitle,
                        butlerUploadInfo,
                        Mathf.Clamp01(butlerUploadProgress)
                    );

                    System.Threading.Thread.Sleep(100);
                }

                stdoutThread.Join();
                stderrThread.Join();

                EditorUtility.ClearProgressBar();

                string output = CleanButlerFinalOutput(finalOutput.ToString());
                string error = CleanButlerFinalOutput(finalError.ToString());

                if (!string.IsNullOrWhiteSpace(output))
                {
                    Log(output);
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Log("Butler stderr:");
                    Log(error);
                }

                if (process.ExitCode == 0)
                {
                    Log($"Itch.io upload completed successfully: {itchTarget}");
                    return true;
                }

                Log($"Itch.io upload failed. Butler exit code: {process.ExitCode}");
                return false;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Log("Itch.io upload failed with exception: " + e.Message);
                return false;
            }
            finally
            {
                butlerUploadRunning = false;
                EditorUtility.ClearProgressBar();
            }
        }

        private void ReadButlerStream(StreamReader reader, StringBuilder finalOutput)
        {
            char[] buffer = new char[512];

            while (true)
            {
                int count = reader.Read(buffer, 0, buffer.Length);

                if (count <= 0)
                    break;

                string chunk = new string(buffer, 0, count);

                ParseButlerProgress(chunk);

                lock (finalOutput)
                {
                    finalOutput.Append(chunk);
                }
            }
        }

        private void ParseButlerProgress(string chunk)
        {
            MatchCollection matches = Regex.Matches(chunk, @"(\d+(?:\.\d+)?)%");

            if (matches.Count > 0)
            {
                Match last = matches[matches.Count - 1];

                if (float.TryParse(
                    last.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out float percent))
                {
                    butlerUploadProgress = Mathf.Clamp01(percent / 100.0f);

                    butlerUploadInfo = $"Uploading... {percent:0.00}%";
                }
            }

            if (chunk.IndexOf("finalizing", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                butlerUploadProgress = 1.0f;
                butlerUploadInfo = "Finalizing build...";
            }
            else if (chunk.IndexOf("almost there", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                butlerUploadProgress = 1.0f;
                butlerUploadInfo = "Almost there...";
            }
        }

        private string CleanButlerFinalOutput(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            input = CleanLogOutput(input);

            string[] lines = input.Split('\n');
            StringBuilder result = new StringBuilder();

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Drop Butler animated progress lines.
                if (Regex.IsMatch(line, @"^\d+(\.\d+)?%"))
                    continue;

                if (Regex.IsMatch(line, @"@\s*\d+(\.\d+)?\s*(KiB|MiB|GiB)/s"))
                    continue;

                if (line.Contains("left") && Regex.IsMatch(line, @"\d+(\.\d+)?\s*(KiB|MiB|GiB)\s+left"))
                    continue;

                // Drop Butler progress bar lines.
                if (Regex.IsMatch(line, @"^[\u2590\u2588\u2591\s]+\u258C\s*\d+(\.\d+)?%"))
                    continue;

                if (Regex.IsMatch(line, @"^\d+(\.\d+)?%"))
                    continue;

                if (line.Contains("almost there") || line.Contains("finalizing build"))
                    continue;

                result.AppendLine(line);
            }

            return result.ToString().Trim();
        }
    }
}
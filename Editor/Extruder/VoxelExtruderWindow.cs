// Made using Claude (claude-sonnet-4-6) - https://claude.ai
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace UC
{
    /// <summary>
    /// Editor window for the Voxel Extrude tool.
    /// Right-click any Texture2D in the Project window -> Unity Common Tools / Mode / Extrude
    /// </summary>
    public class VoxelExtruderWindow : EditorWindow
    {
        // -- Source -----------------------------------------------------------
        private Texture2D _sourceTexture;
        private string    _sourcePath;

        // -- Grid settings -----------------------------------------------------
        private int _xyGridSize = 1;   // pixels per voxel cell in XY
        private int _zGridSize  = 1;   // number of Z depth slices (1 = flat)

        // -- Voxel world size -------------------------------------------------
        private float _voxelSizeX = 0.1f;
        private float _voxelSizeY = 0.1f;
        private float _voxelSizeZ = 0.1f;

        // -- Output mode ------------------------------------------------------
        private enum OutputMode { PalettedTexture, VertexColor }
        private OutputMode _outputMode        = OutputMode.PalettedTexture;
        private bool       _generateMaterial  = true;
        private bool       _srgbVertexColor   = true;

        // -- UI state ---------------------------------------------------------
        private Vector2  _scroll;
        private string   _lastStatus = "";
        private GUIStyle _statusStyle;

        // ---------------------------------------------------------------------
        // Menu entry
        // ---------------------------------------------------------------------

        [MenuItem("Assets/Unity Common Tools/Model/Extrude", false, 1000)]
        private static void OpenFromContextMenu()
        {
            var tex = Selection.activeObject as Texture2D;
            if (tex == null)
            {
                EditorUtility.DisplayDialog("Voxel Extrude",
                    "Please select a Texture2D asset first.", "OK");
                return;
            }

            var win = GetWindow<VoxelExtruderWindow>(true, "Voxel Extrude", true);
            win.minSize = new Vector2(400, 380);
            win.SetTexture(tex);
            win.Show();
        }

        [MenuItem("Assets/Unity Common Tools/Model/Extrude", true)]
        private static bool OpenFromContextMenuValidate()
            => Selection.activeObject is Texture2D;

        // ---------------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------------

        public void SetTexture(Texture2D tex)
        {
            _sourceTexture = tex;
            _sourcePath    = AssetDatabase.GetAssetPath(tex);
            _lastStatus    = "";
        }

        // ---------------------------------------------------------------------
        // GUI
        // ---------------------------------------------------------------------

        private void OnGUI()
        {
            EnsureStyles();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // -- Header --------------------------------------------------------
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Voxel Extrude", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Generate a voxelized mesh from a 2-D sprite.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(6);
            DrawSeparator();

            // -- Source texture ------------------------------------------------
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _sourceTexture = (Texture2D)EditorGUILayout.ObjectField(
                "Texture", _sourceTexture, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && _sourceTexture != null)
                _sourcePath = AssetDatabase.GetAssetPath(_sourceTexture);

            if (_sourceTexture == null)
            {
                EditorGUILayout.HelpBox("Assign a Texture2D to continue.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Path", _sourcePath);
            EditorGUILayout.TextField("Size",
                $"{_sourceTexture.width} x {_sourceTexture.height} px");
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            // -- Grid settings -------------------------------------------------
            EditorGUILayout.Space(8);
            DrawSeparator();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Grid Size", EditorStyles.boldLabel);

            _xyGridSize = Mathf.Max(1,
                EditorGUILayout.IntField(
                    new GUIContent("XY Grid (px)",
                        "How many source pixels form one voxel cell in X and Y. " +
                        "1 = every pixel is its own voxel."),
                    _xyGridSize));

            int cols = Mathf.CeilToInt((float)_sourceTexture.width  / _xyGridSize);
            int rows = Mathf.CeilToInt((float)_sourceTexture.height / _xyGridSize);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("XY voxel grid", $"{cols} x {rows}  ({cols * rows} cells)");
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(4);

            _zGridSize = Mathf.Max(1,
                EditorGUILayout.IntField(
                    new GUIContent("Z Grid (slices)",
                        "Number of depth slices. 1 = flat. Each additional slice erodes " +
                        "the silhouette by one voxel inward, creating a rounded profile. " +
                        "Slices are mirrored symmetrically, so total depth = (2N - 1) voxels."),
                    _zGridSize));

            int totalLayers = 2 * _zGridSize - 1;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Total Z layers", $"{totalLayers}");
            EditorGUI.EndDisabledGroup();

            if (_zGridSize > 1)
                EditorGUILayout.HelpBox(
                    $"Each successive slice removes one ring of edge voxels. " +
                    $"The silhouette must be at least {_zGridSize} voxels thick in XY " +
                    $"for inner slices to contain any geometry.",
                    MessageType.Info);

            // -- Voxel world size ----------------------------------------------
            EditorGUILayout.Space(8);
            DrawSeparator();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Voxel World Size", EditorStyles.boldLabel);

            _voxelSizeX = Mathf.Max(0.001f,
                EditorGUILayout.FloatField(
                    new GUIContent("X", "World-space width of one voxel."),
                    _voxelSizeX));

            _voxelSizeY = Mathf.Max(0.001f,
                EditorGUILayout.FloatField(
                    new GUIContent("Y", "World-space height of one voxel."),
                    _voxelSizeY));

            _voxelSizeZ = Mathf.Max(0.001f,
                EditorGUILayout.FloatField(
                    new GUIContent("Z", "World-space depth of one voxel layer."),
                    _voxelSizeZ));

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.FloatField(
                new GUIContent("Total depth", "World-space total depth of the mesh."),
                totalLayers * _voxelSizeZ);
            EditorGUI.EndDisabledGroup();

            // -- Output mode ---------------------------------------------------
            EditorGUILayout.Space(8);
            DrawSeparator();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Output Mode", EditorStyles.boldLabel);

            _outputMode = (OutputMode)EditorGUILayout.EnumPopup(
                new GUIContent("Mode",
                    "Paletted Texture: UVs + generated palette atlas.\n" +
                    "Vertex Color: per-vertex colour, no texture."),
                _outputMode);

            switch (_outputMode)
            {
                case OutputMode.PalettedTexture:
                    EditorGUILayout.HelpBox(
                        "Generates a palette texture that maps each unique voxel colour " +
                        "to a UV coordinate. Use any standard lit material with the " +
                        "generated texture as Albedo (set Filter Mode to Point).",
                        MessageType.None);
                    break;
                case OutputMode.VertexColor:
                    EditorGUILayout.HelpBox(
                        "Stores colour directly in vertex data. No extra texture asset. " +
                        "Requires a vertex-colour shader (e.g. Particles/Standard Lit).",
                        MessageType.None);
                    _srgbVertexColor = EditorGUILayout.Toggle(
                        new GUIContent("Source is sRGB",
                            "Unity's GetPixels() returns linear values. Enable this when " +
                            "the source PNG is sRGB (the default for all PNGs) so colours " +
                            "are converted back to gamma space before being stored as " +
                            "vertex data. Disable only if your source texture is already " +
                            "in linear colour space."),
                        _srgbVertexColor);
                    break;
            }

            _generateMaterial = EditorGUILayout.Toggle(
                new GUIContent("Generate Material",
                    "Save a Material asset (sword_material.mat) pre-configured " +
                    "with the palette texture and a suitable shader."),
                _generateMaterial);

            // -- Generate button ------------------------------------------------
            EditorGUILayout.Space(10);
            DrawSeparator();
            EditorGUILayout.Space(6);

            GUI.backgroundColor = new Color(0.4f, 0.75f, 0.4f);
            if (GUILayout.Button("Generate", GUILayout.Height(36)))
                RunGenerate();
            GUI.backgroundColor = Color.white;

            // -- Status --------------------------------------------------------
            if (!string.IsNullOrEmpty(_lastStatus))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(_lastStatus, EditorStyles.helpBox);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------------------------
        // Generation
        // ---------------------------------------------------------------------

        private void RunGenerate()
        {
            _lastStatus  = "";

            if (_sourceTexture == null)
            {
                _lastStatus = "No source texture selected.";
                return;
            }

            try
            {
                // Read pixels without modifying import settings
                var (pixels, texW, texH) =
                    VoxelBuilder.ReadPixelsSafe(_sourceTexture, _sourcePath);

                var voxelSize = new Vector3(_voxelSizeX, _voxelSizeY, _voxelSizeZ);

                VoxelData voxels = VoxelBuilder.BuildFromTexture(
                    pixels, texW, texH, _xyGridSize, _zGridSize, voxelSize);

                string baseName  = Path.GetFileNameWithoutExtension(_sourcePath);
                string outputDir = Path.GetDirectoryName(_sourcePath);

                switch (_outputMode)
                {
                    case OutputMode.PalettedTexture:
                        VoxelMeshExporter.ExportWithPalette(
                            voxels, baseName, outputDir, _generateMaterial);
                        break;
                    case OutputMode.VertexColor:
                        VoxelMeshExporter.ExportWithVertexColor(
                            voxels, baseName, outputDir, _generateMaterial,
                            _srgbVertexColor);
                        break;
                }

                AssetDatabase.Refresh();
                _lastStatus  = $"Assets saved to  {outputDir}/";
            }
            catch (System.Exception ex)
            {
                _lastStatus  = $"Error: {ex.Message}";
                Debug.LogException(ex);
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        private static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
        }

        private void EnsureStyles()
        {
            if (_statusStyle != null) return;
            _statusStyle = new GUIStyle(EditorStyles.helpBox) { wordWrap = true, fontSize = 11 };
        }

        [MenuItem("Unity Common/Voxel Extrude", priority = 10)]
        private static void OpenFromMenu()
        {
            var win = GetWindow<VoxelExtruderWindow>(true, "Voxel Extrude", true);
            win.minSize = new Vector2(400, 380);
            win.Show();
        }
    }
}

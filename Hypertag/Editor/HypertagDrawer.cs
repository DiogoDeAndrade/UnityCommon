using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    [InitializeOnLoad]
    internal static class HypertagCache
    {
        private static bool _dirty = true;

        private static string[] _paths = Array.Empty<string>();
        private static string[] _guids = Array.Empty<string>();
        private static Dictionary<UnityEngine.Object, int> _indexByObject = new();

        static HypertagCache()
        {
            EditorApplication.projectChanged += MarkDirty;
        }

        public static void MarkDirty()
        {
            _dirty = true;
        }

        public static void EnsureBuilt(Type hypertagType)
        {
            if (!_dirty) return;

            _dirty = false;
            _indexByObject.Clear();

            var search = $"t:{hypertagType.Name}";
            var foundGuids = AssetDatabase.FindAssets(search);

            var items = new List<(string path, string guid, UnityEngine.Object obj)>(foundGuids.Length);

            foreach (var guid in foundGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<Hypertag>(assetPath);
                if (!obj) continue;

                var category = NormalizeCategory(obj.category);
                var fullPath = string.IsNullOrEmpty(category) ? obj.displayName : $"{category}/{obj.displayName}";
                items.Add((fullPath, guid, obj));
            }

            items.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            _paths = items.Select(i => i.path).ToArray();
            _guids = items.Select(i => i.guid).ToArray();

            for (int i = 0; i < items.Count; i++)
                _indexByObject[items[i].obj] = i;
        }

        public static string[] Paths => _paths;

        public static int IndexOf(UnityEngine.Object obj)
        {
            if (!obj) return -1;
            return _indexByObject.TryGetValue(obj, out var idx) ? idx : -1;
        }

        public static UnityEngine.Object ObjectAt(int index, Type hypertagType)
        {
            if (index < 0 || index >= _guids.Length) return null;
            var assetPath = AssetDatabase.GUIDToAssetPath(_guids[index]);
            return AssetDatabase.LoadAssetAtPath(assetPath, hypertagType);
        }

        public static string NormalizeCategory(string s)
        {
            s ??= string.Empty;
            s = s.Trim();
            s = s.Trim('/');
            s = s.Replace("\\", "/");
            while (s.Contains("//")) s = s.Replace("//", "/");
            return s;
        }
    }

    internal class HypertagAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if ((importedAssets?.Length ?? 0) > 0 ||
                (deletedAssets?.Length ?? 0) > 0 ||
                (movedAssets?.Length ?? 0) > 0)
            {
                HypertagCache.MarkDirty();
            }
        }
    }

    internal static class HypertagCreationUtility
    {
        private const string DefaultFolderEditorPrefKey = "UC.Hypertag.DefaultFolder";

        public static string GetOrAskForDefaultFolder()
        {
            var saved = EditorPrefs.GetString(DefaultFolderEditorPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(saved) && AssetDatabase.IsValidFolder(saved))
                return saved;

            var absolute = EditorUtility.OpenFolderPanel("Choose default folder for Hypertags", Application.dataPath, "");
            if (string.IsNullOrEmpty(absolute))
                return null;

            absolute = absolute.Replace("\\", "/");
            var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace("\\", "/");

            if (!absolute.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid Folder",
                    "The selected folder must be inside this Unity project.",
                    "OK");
                return null;
            }

            var relative = absolute.Substring(projectPath.Length).TrimStart('/');
            if (!relative.StartsWith("Assets", StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("Invalid Folder",
                    "The selected folder must be inside the Assets folder.",
                    "OK");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(relative))
            {
                EditorUtility.DisplayDialog("Invalid Folder",
                    "That folder is not a valid Unity asset folder.",
                    "OK");
                return null;
            }

            EditorPrefs.SetString(DefaultFolderEditorPrefKey, relative);
            return relative;
        }

        public static void ClearRememberedDefaultFolder()
        {
            EditorPrefs.DeleteKey(DefaultFolderEditorPrefKey);
        }

        public static Hypertag CreateHypertagAsset(Type hypertagType, string typedPath)
        {
            var folder = GetOrAskForDefaultFolder();
            if (string.IsNullOrEmpty(folder))
                return null;

            ParseTypedPath(typedPath, out var category, out var displayName);

            if (string.IsNullOrWhiteSpace(displayName))
            {
                EditorUtility.DisplayDialog("Invalid Name",
                    "Please enter a hypertag name. Example: Gameplay/Enemies/Boss",
                    "OK");
                return null;
            }

            var asset = ScriptableObject.CreateInstance(hypertagType) as Hypertag;
            if (!asset)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Could not create asset of type {hypertagType.Name}.",
                    "OK");
                return null;
            }

            asset.name = displayName;

            var so = new SerializedObject(asset);
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_category").stringValue = category;
            so.ApplyModifiedPropertiesWithoutUndo();

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{displayName}.asset");
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(asset);
            Selection.activeObject = asset;

            HypertagCache.MarkDirty();

            return asset;
        }

        private static void ParseTypedPath(string input, out string category, out string displayName)
        {
            input ??= string.Empty;
            input = input.Trim();
            input = input.Replace("\\", "/");
            while (input.Contains("//")) input = input.Replace("//", "/");
            input = input.Trim('/');

            if (string.IsNullOrEmpty(input))
            {
                category = string.Empty;
                displayName = string.Empty;
                return;
            }

            var parts = input.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                             .Select(p => p.Trim())
                             .Where(p => !string.IsNullOrEmpty(p))
                             .ToArray();

            if (parts.Length == 0)
            {
                category = string.Empty;
                displayName = string.Empty;
                return;
            }

            displayName = parts[^1];
            category = parts.Length > 1
                ? string.Join("/", parts, 0, parts.Length - 1)
                : string.Empty;

            category = HypertagCache.NormalizeCategory(category);
        }
    }

    internal class HypertagCreateWindow : EditorWindow
    {
        private Action<Hypertag> _onCreated;
        private Type _hypertagType;
        private string _typedPath = string.Empty;

        public static void Open(Type hypertagType, Action<Hypertag> onCreated)
        {
            var window = CreateInstance<HypertagCreateWindow>();
            window.titleContent = new GUIContent("Create Hypertag");
            window._hypertagType = hypertagType;
            window._onCreated = onCreated;
            window.minSize = new Vector2(420f, 90f);
            window.maxSize = new Vector2(420f, 90f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("New Hypertag", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _typedPath = EditorGUILayout.TextField(
                new GUIContent("Path", "Example: Gameplay/Enemies/Boss"),
                _typedPath);

            EditorGUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create"))
                {
                    var created = HypertagCreationUtility.CreateHypertagAsset(_hypertagType, _typedPath);
                    if (created != null)
                    {
                        _onCreated?.Invoke(created);
                        Close();
                    }
                }

                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }
            }
        }
    }

    [CustomPropertyDrawer(typeof(Hypertag), true)]
    public class HypertagDrawer : PropertyDrawer
    {
        private const float RefreshButtonWidth = 22f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var hypertagType = fieldInfo.FieldType;
            if (!typeof(UnityEngine.Object).IsAssignableFrom(hypertagType))
                hypertagType = typeof(Hypertag);

            HypertagCache.EnsureBuilt(hypertagType);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var line = position;
                line.height = EditorGUIUtility.singleLineHeight;

                var popupRect = line;
                popupRect.width -= (RefreshButtonWidth + 2f);

                var refreshRect = line;
                refreshRect.x = popupRect.xMax + 2f;
                refreshRect.width = RefreshButtonWidth;

                popupRect = EditorGUI.PrefixLabel(popupRect, label);

                var currentObj = property.objectReferenceValue as Hypertag;
                var currentIndex = HypertagCache.IndexOf(currentObj);

                string buttonText;
                if (currentIndex >= 0 && currentIndex < HypertagCache.Paths.Length)
                    buttonText = HypertagCache.Paths[currentIndex];
                else if (currentObj != null)
                    buttonText = currentObj.displayName;
                else
                    buttonText = "None";

                if (EditorGUI.DropdownButton(popupRect, new GUIContent(buttonText), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();

                    menu.AddItem(new GUIContent("None"), currentObj == null, () =>
                    {
                        property.serializedObject.Update();
                        property.objectReferenceValue = null;
                        property.serializedObject.ApplyModifiedProperties();
                    });

                    menu.AddSeparator("");

                    var paths = HypertagCache.Paths;
                    for (int i = 0; i < paths.Length; i++)
                    {
                        var idx = i;
                        var path = paths[i];
                        var isOn = idx == currentIndex;

                        menu.AddItem(new GUIContent(path), isOn, () =>
                        {
                            var picked = HypertagCache.ObjectAt(idx, hypertagType);
                            property.serializedObject.Update();
                            property.objectReferenceValue = picked;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Create New..."), false, () =>
                    {
                        HypertagCreateWindow.Open(hypertagType, created =>
                        {
                            property.serializedObject.Update();
                            property.objectReferenceValue = created;
                            property.serializedObject.ApplyModifiedProperties();

                            HypertagCache.MarkDirty();
                        });
                    });

                    menu.AddItem(new GUIContent("Set Default Folder..."), false, () =>
                    {
                        HypertagCreationUtility.ClearRememberedDefaultFolder();
                        HypertagCreationUtility.GetOrAskForDefaultFolder();
                    });

                    menu.DropDown(popupRect);
                }

                var refreshIcon = EditorGUIUtility.IconContent("Refresh");
                if (GUI.Button(refreshRect, refreshIcon, EditorStyles.iconButton))
                {
                    HypertagCache.MarkDirty();
                    HypertagCache.EnsureBuilt(hypertagType);
                }
            }
        }
    }
}
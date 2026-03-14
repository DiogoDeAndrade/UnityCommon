using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    [InitializeOnLoad]
    internal static class HypertagCache
    {
        private static bool _dirty = true;

        private static string[] _paths = Array.Empty<string>();                // "Category/Sub/TagName"
        private static string[] _guids = Array.Empty<string>();                // GUID per entry
        private static Dictionary<UnityEngine.Object, int> _indexByObject = new();

        static HypertagCache()
        {
            // Auto-refresh whenever Unity thinks the Project changed (imports, deletes, renames, etc.)
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

            // Find all assets of type Hypertag (by name), even if your class is in another assembly.
            // If your type name differs, change "Hypertag" below.
            var search = $"t:{hypertagType.Name}";
            var foundGuids = AssetDatabase.FindAssets(search);

            var items = new List<(string path, string guid, UnityEngine.Object obj)>(foundGuids.Length);

            foreach (var guid in foundGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<Hypertag>(assetPath);
                if (!obj) continue;

                var category = obj.category;
                category = NormalizeCategory(category);

                var fullPath = string.IsNullOrEmpty(category) ? obj.displayName : $"{category}/{obj.displayName}";
                items.Add((fullPath, guid, obj));
            }

            // Sort by path for nicer menu ordering
            items.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            _paths = items.Select(i => i.path).ToArray();
            _guids = items.Select(i => i.guid).ToArray();

            for (int i = 0; i < items.Count; i++)
            {
                _indexByObject[items[i].obj] = i;
            }
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

        private static string NormalizeCategory(string s)
        {
            // Allow "Folder/SubFolder" style. Trim slashes/spaces.
            s = s.Trim();
            s = s.Trim('/');
            s = s.Replace("\\", "/");
            while (s.Contains("//")) s = s.Replace("//", "/");
            return s;
        }
    }

    internal class HypertagAssetPostprocessor : AssetPostprocessor
    {
        // This catches imports/deletes/moves and forces a refresh of the cache.
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

    [CustomPropertyDrawer(typeof(Hypertag), true)]
    public class HypertagDrawer : PropertyDrawer
    {
        private const float RefreshButtonWidth = 22f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // We only handle object references (Hypertag assets).
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var hypertagType = fieldInfo.FieldType;
            // For arrays/lists, fieldInfo is Hypertag[]/List<Hypertag>, but the drawer is invoked for the element,
            // so fieldInfo.FieldType should still be Hypertag in practice. Still, be defensive:
            if (!typeof(UnityEngine.Object).IsAssignableFrom(hypertagType))
                hypertagType = typeof(Hypertag);

            HypertagCache.EnsureBuilt(hypertagType);

            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var line = position;
                line.height = EditorGUIUtility.singleLineHeight;

                // Split: popup + refresh button
                var popupRect = line;
                popupRect.width -= (RefreshButtonWidth + 2f);

                var refreshRect = line;
                refreshRect.x = popupRect.xMax + 2f;
                refreshRect.width = RefreshButtonWidth;

                popupRect = EditorGUI.PrefixLabel(popupRect, label);

                // Current selection
                var currentObj = property.objectReferenceValue as Hypertag;
                var currentIndex = HypertagCache.IndexOf(currentObj);

                // Dropdown button text
                string buttonText;
                if (currentIndex >= 0 && currentIndex < HypertagCache.Paths.Length)
                    buttonText = HypertagCache.Paths[currentIndex];
                else if (currentObj != null)
                    buttonText = currentObj.displayName; // fallback
                else
                    buttonText = "None";

                // Dropdown
                if (EditorGUI.DropdownButton(popupRect, new GUIContent(buttonText), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();

                    // None option
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
                        var path = paths[i]; // already "Folder/Sub/Name"
                        var isOn = (idx == currentIndex);

                        menu.AddItem(new GUIContent(path), isOn, () =>
                        {
                            var picked = HypertagCache.ObjectAt(idx, hypertagType);
                            property.serializedObject.Update();
                            property.objectReferenceValue = picked;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.DropDown(popupRect);
                }

                // Refresh button
                var refreshIcon = EditorGUIUtility.IconContent("Refresh");
                if (GUI.Button(refreshRect, refreshIcon, EditorStyles.iconButton))
                {
                    HypertagCache.MarkDirty();
                    // Force rebuild next draw
                    HypertagCache.EnsureBuilt(hypertagType);
                }
            }
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UC;
using UC.Interaction;
using UC.Interaction.Editor;
using NaughtyAttributes.Editor;

[CustomEditor(typeof(ModularScriptableObject), true)]
public class ModularScriptableObjectEditor : Editor
{
    // "Clipboard" for modules (JSON + type name)
    private static string s_ModuleClipboardJson;
    private static string s_ModuleClipboardTypeName;

    private static bool HasClipboard => (!string.IsNullOrEmpty(s_ModuleClipboardJson)) && (!string.IsNullOrEmpty(s_ModuleClipboardTypeName));

    private SerializedProperty _parentsProp;
    private SerializedProperty _modulesProp;

    private void OnEnable()
    {
        _parentsProp = serializedObject.FindProperty("_parents");
        _modulesProp = serializedObject.FindProperty("_modules");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var mso = (ModularScriptableObject)target;

        DrawHeader();
        EditorGUILayout.Space();

        DrawParentsSection();
        EditorGUILayout.Space();

        // Draw all other fields except parents/modules + script
        DrawPropertiesExcluding(serializedObject, "_parents", "_modules", "m_Script");
        EditorGUILayout.Space();

        DrawModulesSection(mso);

        serializedObject.ApplyModifiedProperties();
    }

    private new void DrawHeader()
    {
    }

    private void DrawParentsSection()
    {
        EditorGUILayout.PropertyField(_parentsProp, includeChildren: true);
    }

    private void DrawModulesSection(ModularScriptableObject mso)
    {
        EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);

        if ((_modulesProp == null) || (_modulesProp.arraySize == 0))
        {
            EditorGUILayout.HelpBox("No modules added.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < _modulesProp.arraySize; i++)
            {
                var element = _modulesProp.GetArrayElementAtIndex(i);
                DrawModulePanel(mso, element, i);
                EditorGUILayout.Space(); // separation between modules
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Module", GUILayout.Height(22)))
            {
                ShowAddModuleMenu(mso);
            }

            using (new EditorGUI.DisabledScope(!HasClipboard))
            {
                if (GUILayout.Button("Paste Module from Clipboard", GUILayout.Height(22)))
                {
                    PasteModuleAsNew(mso);
                }
            }
        }
    }

    private void DrawModulePanel(ModularScriptableObject mso, SerializedProperty element, int index)
    {
        if (element == null)
            return;

        object boxedObj = element.managedReferenceValue;
        if (boxedObj == null)
            return;

        var module = boxedObj as SOModule;
        Type type = boxedObj.GetType();
        string headerName = GetModuleHeaderName(module, type);

        // Use the serialized _open field to persist foldout state
        var openProp = element.FindPropertyRelative("_open");
        bool expanded = openProp != null ? openProp.boolValue : true;

        // ---------- HEADER BACKGROUND ----------
        const float headerHeight = 24f;
        Rect headerRect = GUILayoutUtility.GetRect(0f, headerHeight, GUILayout.ExpandWidth(true));
        Rect colorBar = headerRect;
        colorBar.x -= 20;
        colorBar.width += 20;

        Color bg = new Color(0.32f, 0.32f, 0.32f);
        Color line = new Color(0f, 0f, 0f, 0.6f);

        EditorGUI.DrawRect(colorBar, bg);
        EditorGUI.DrawRect(new Rect(colorBar.xMin, colorBar.yMax - 1f, colorBar.width, 1f), line);
        EditorGUI.DrawRect(new Rect(colorBar.xMin, colorBar.yMax - 1f - colorBar.height, colorBar.width, 1f), line);

        // Slight inset for content
        Rect contentRect = new Rect(headerRect.x + 4f, headerRect.y + 1f,
                                    headerRect.width - 8f, headerRect.height - 2f);

        // Layout rects: [Foldout][Checkbox][Name]
        const float iconWidth = 16f;
        float x = contentRect.x;
        float y = contentRect.y;
        float h = contentRect.height;

        Rect foldRect = new Rect(x, y, iconWidth, h);
        x += iconWidth + 2f;

        Rect toggleRect = new Rect(x, y, iconWidth, h);
        x += iconWidth + 4f;

        Rect labelRect = new Rect(x, y, contentRect.xMax - x, h);

        // Foldout arrow (no label)
        expanded = EditorGUI.Foldout(foldRect, expanded, GUIContent.none, true);
        if (openProp != null)
        {
            openProp.boolValue = expanded;
        }

        // Enabled toggle from SOModule._enabled (or .enabled)
        var enabledProp = element.FindPropertyRelative("_enabled") ??
                          element.FindPropertyRelative("enabled");
        if (enabledProp != null)
        {
            bool enabled = enabledProp.boolValue;
            bool newEnabled = EditorGUI.Toggle(toggleRect, enabled);
            if (newEnabled != enabled)
            {
                enabledProp.boolValue = newEnabled;
            }
        }

        // Name label
        EditorGUI.LabelField(labelRect, headerName, EditorStyles.boldLabel);

        // Context menu on right-click anywhere on the header
        Event e = Event.current;
        if ((e.type == EventType.ContextClick) && (headerRect.Contains(e.mousePosition)))
        {
            var menu = new GenericMenu();

            if (module != null)
            {
                menu.AddItem(new GUIContent("Edit Script"), false, () =>
                {
                    var script = GUIUtils.FindScriptForType(type);
                    if (script != null)
                        AssetDatabase.OpenAsset(script);
                });

                menu.AddItem(new GUIContent("Ping Script"), false, () =>
                {
                    var script = GUIUtils.FindScriptForType(type);
                    if (script != null)
                        EditorGUIUtility.PingObject(script);
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Edit Script"));
                menu.AddDisabledItem(new GUIContent("Ping Script"));
            }

            menu.AddSeparator("");

            // Copy
            if (module != null)
                menu.AddItem(new GUIContent("Copy"), false, () => CopyModuleToClipboard(module));
            else
                menu.AddDisabledItem(new GUIContent("Copy"));

            // Paste (overwrite)
            if (HasClipboard)
            {
                menu.AddItem(new GUIContent("Paste"), false, () =>
                {
                    if (EditorUtility.DisplayDialog(
                            "Overwrite Module",
                            $"Are you sure you want to overwrite the module {headerName}?",
                            "Overwrite", "Cancel"))
                    {
                        PasteModuleOver(mso, element);
                    }
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("Paste"));
            }

            menu.AddSeparator("");

            // Move Up
            if (index > 0)
                menu.AddItem(new GUIContent("Move Up"), false, () => MoveModule(mso, index, index - 1));
            else
                menu.AddDisabledItem(new GUIContent("Move Up"));

            // Move Down
            if (index < _modulesProp.arraySize - 1)
                menu.AddItem(new GUIContent("Move Down"), false, () => MoveModule(mso, index, index + 1));
            else
                menu.AddDisabledItem(new GUIContent("Move Down"));

            menu.AddSeparator("");

            // Delete
            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                if (EditorUtility.DisplayDialog("Remove Module",
                        $"Remove module {headerName}?",
                        "Remove", "Cancel"))
                {
                    RemoveModuleAt(mso, index);
                }
            });

            menu.ShowAsContext();
            e.Use();
        }

        // ---------- BODY ----------
        if (!expanded)
        {
            return;
        }

        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(element, GUIContent.none, includeChildren: true);

        EditorGUI.indentLevel--;
    }

    private void RemoveModuleAt(ModularScriptableObject mso, int index)
    {
        var moduleProp = _modulesProp.GetArrayElementAtIndex(index);
        var moduleObj = moduleProp.managedReferenceValue as SOModule;

        Undo.RecordObject(mso, "Remove Module");
        mso.RemoveModule(moduleObj);

        // Keep serialized array in sync
        _modulesProp.DeleteArrayElementAtIndex(index);

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(mso);
    }

    private void MoveModule(ModularScriptableObject mso, int from, int to)
    {
        if ((from == to) || (from < 0) || (to < 0) || (from >= _modulesProp.arraySize) || (to >= _modulesProp.arraySize))
            return;

        Undo.RecordObject(mso, "Reorder Module");
        _modulesProp.MoveArrayElement(from, to);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(mso);
    }

    private void ShowAddModuleMenu(ModularScriptableObject mso)
    {
        var menu = new GenericMenu();

        var entries = ManagedReferenceTypeCache.GetAssignableConcreteTypes(typeof(SOModule));

        if ((entries == null) || (entries.Count == 0))
        {
            menu.AddDisabledItem(new GUIContent("No SOModule types found"));
            menu.ShowAsContext();
            return;
        }

        foreach (var (displayName, type) in entries)
        {
            menu.AddItem(new GUIContent(displayName), false, () =>
            {
                AddModuleOfType(mso, type);
            });
        }

        menu.ShowAsContext();
    }

    private void AddModuleOfType(ModularScriptableObject mso, Type t)
    {
        Undo.RecordObject(mso, "Add Module");

        mso.AddModule(t);

        EditorUtility.SetDirty(mso);
        serializedObject.Update();
    }

    private static string GetModuleHeaderName(SOModule module, Type type)
    {
        // 1) Ask the module itself
        if (module != null)
        {
            string custom = module.GetModuleHeaderString();
            if (!string.IsNullOrWhiteSpace(custom))
                return custom;
        }

        // 2) Fall back to PolymorphicNameAttribute.Path (last segment)
        if (type != null)
        {
            var attrs = type.GetCustomAttributes(typeof(PolymorphicNameAttribute), inherit: false);
            if ((attrs != null) && (attrs.Length > 0))
            {
                if ((attrs[0] is PolymorphicNameAttribute attr) && (!string.IsNullOrWhiteSpace(attr.Path)))
                {
                    var path = attr.Path.Trim();
                    var lastSlash = path.LastIndexOf('/');
                    var lastSegment = lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
                    return ObjectNames.NicifyVariableName(lastSegment);
                }
            }

            // 3) Final fallback: nicer type name
            return ObjectNames.NicifyVariableName(type.Name);
        }

        return "<Null Module>";
    }

    private void CopyModuleToClipboard(SOModule module)
    {
        if (module == null)
            return;

        s_ModuleClipboardJson = JsonUtility.ToJson(module);
        s_ModuleClipboardTypeName = module.GetType().AssemblyQualifiedName;
    }

    private void PasteModuleOver(ModularScriptableObject mso, SerializedProperty element)
    {
        if (!HasClipboard || element == null)
            return;

        var targetModule = element.managedReferenceValue as SOModule;
        if (targetModule == null)
            return;

        Undo.RecordObject(mso, "Paste Module (Overwrite)");

        JsonUtility.FromJsonOverwrite(s_ModuleClipboardJson, targetModule);

        // Owner (_scriptableObject) will be corrected by OnValidate in ModularScriptableObject.
        EditorUtility.SetDirty(mso);
        serializedObject.Update();
    }

    private void PasteModuleAsNew(ModularScriptableObject mso)
    {
        if (!HasClipboard)
            return;

        var type = System.Type.GetType(s_ModuleClipboardTypeName);
        if (type == null || !typeof(SOModule).IsAssignableFrom(type))
            return;

        Undo.RecordObject(mso, "Paste Module");

        // Create a new module of the same type and then overwrite with JSON
        var newModule = mso.AddModule(type) as SOModule;
        if (newModule == null)
            return;

        JsonUtility.FromJsonOverwrite(s_ModuleClipboardJson, newModule);

        // Owner will again be re-established in OnValidate.
        EditorUtility.SetDirty(mso);
        serializedObject.Update();
    }
}

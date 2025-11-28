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
        EditorGUILayout.LabelField(target.name, EditorStyles.boldLabel);
    }

    private void DrawParentsSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Parents", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_parentsProp, includeChildren: true);
            EditorGUI.indentLevel--;
        }
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

        // Big "Add Module" button at the end, like "Add Component"
        if (GUILayout.Button("Add Module", GUILayout.Height(22)))
        {
            ShowAddModuleMenu(mso);
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

        // Layout rects: [Foldout][Checkbox][Name] ...... [Up][Down][X]
        const float iconWidth = 16f;
        const float btnWidth = headerHeight;
        float x = contentRect.x;
        float y = contentRect.y;
        float h = contentRect.height;

        Rect foldRect = new Rect(x, y, iconWidth, h);
        x += iconWidth + 2f;

        Rect toggleRect = new Rect(x, y, iconWidth, h);
        x += iconWidth + 4f;

        float buttonsTotalWidth = btnWidth * 3 + 4f; // 3 buttons + small padding
        Rect labelRect = new Rect(x, y, contentRect.xMax - x - buttonsTotalWidth, h);

        Rect upRect = new Rect(contentRect.xMax - (btnWidth * 3), y, btnWidth, h);
        Rect downRect = new Rect(contentRect.xMax - (btnWidth * 2), y, btnWidth, h);
        Rect xRect = new Rect(contentRect.xMax - btnWidth, y, btnWidth, h);

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

        // Up button
        EditorGUI.BeginDisabledGroup(index <= 0);
        if (GUI.Button(upRect, "\u25B2", EditorStyles.miniButton))
        {
            MoveModule(mso, index, index - 1);
            return;
        }
        EditorGUI.EndDisabledGroup();

        // Down button
        EditorGUI.BeginDisabledGroup(index >= _modulesProp.arraySize - 1);
        if (GUI.Button(downRect, "\u25BC", EditorStyles.miniButton))
        {
            MoveModule(mso, index, index + 1);
            return;
        }
        EditorGUI.EndDisabledGroup();

        // Remove button
        if (GUI.Button(xRect, "X", EditorStyles.miniButton))
        {
            if (EditorUtility.DisplayDialog("Remove Module", $"Remove module {headerName}?", "Remove", "Cancel"))
            {
                RemoveModuleAt(mso, index);
                return;
            }
        }

        // ---------- BODY ----------
        if (!expanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        DrawManagedReferenceChildren(element);
        EditorGUI.indentLevel--;
    }

    private static void DrawManagedReferenceChildren(SerializedProperty managedRefProp)
    {
        if (managedRefProp == null)
        {
            return;
        }

        SerializedProperty iterator = managedRefProp.Copy();
        SerializedProperty end = managedRefProp.GetEndProperty();

        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;

            // Skip the internal SerializeReference bookkeeping fields
            if ((iterator.name == "managedReferenceFullTypename") || (iterator.name == "managedReferenceData"))
                continue;

            // Skip the enabled field - we already show it in the header
            if ((iterator.name == "_enabled") || (iterator.name == "enabled"))
                continue;

            NaughtyEditorGUI.PropertyField_Layout(iterator, true);
        }
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
}

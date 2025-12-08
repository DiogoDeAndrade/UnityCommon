using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    public class FunctionCallTypeSearchWindow : EditorWindow
    {
        private Action<Type> _onSelect;
        private string _search = "";
        private Vector2 _scroll;
        private List<Type> _allTypes;

        public static void Show(Rect activatorRect, Action<Type> onSelect)
        {
            var window = CreateInstance<FunctionCallTypeSearchWindow>();
            window._onSelect = onSelect;
            window.titleContent = new GUIContent("Select Component Type");
            window.InitTypes();
            var size = new Vector2(Mathf.Max(250f, activatorRect.width), 300f);
            window.ShowAsDropDown(activatorRect, size);
        }

        void InitTypes()
        {
            _allTypes = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var asm in assemblies)
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }

                if (types == null) continue;

                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (!typeof(Component).IsAssignableFrom(t)) continue;
                    if (t.IsAbstract) continue;
                    _allTypes.Add(t);
                }
            }

            _allTypes.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
        }

        void OnGUI()
        {
            if (_allTypes == null)
                InitTypes();

            EditorGUILayout.Space(2f);
            GUI.SetNextControlName("SearchField");
            _search = EditorGUILayout.TextField(_search);
            if (Event.current.type == EventType.Layout)
                EditorGUI.FocusTextInControl("SearchField");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var t in _allTypes)
            {
                string fullname = t.FullName;
                if (!string.IsNullOrEmpty(_search) &&
                    fullname.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (GUILayout.Button(fullname, EditorStyles.label))
                {
                    _onSelect?.Invoke(t);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void OnLostFocus()
        {
            Close();
        }
    }
}
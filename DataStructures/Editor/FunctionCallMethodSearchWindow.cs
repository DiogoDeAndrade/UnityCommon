using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{
    public class FunctionCallMethodSearchWindow : EditorWindow
    {
        private Action<MethodInfo> _onSelect;
        private Type _targetType;
        private string _search = "";
        private Vector2 _scroll;
        private List<MethodInfo> _methods;

        private bool _filterBySignature;
        private Type[] _signatureTypes;

        public static void Show(
            Rect activatorRect,
            Type targetType,
            bool filterBySignature,
            Type[] signatureTypes,
            Action<MethodInfo> onSelect)
        {
            var window = CreateInstance<FunctionCallMethodSearchWindow>();
            window._targetType = targetType;
            window._onSelect = onSelect;
            window._filterBySignature = filterBySignature;
            window._signatureTypes = signatureTypes ?? Array.Empty<Type>();
            window.titleContent = new GUIContent($"Select Method ({targetType.Name})");
            window.InitMethods();
            var size = new Vector2(Mathf.Max(300f, activatorRect.width), 300f);
            window.ShowAsDropDown(activatorRect, size);
        }

        void InitMethods()
        {
            _methods = new List<MethodInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var m in _targetType.GetMethods(flags))
            {
                if (m.IsSpecialName) continue; // skip property getters/setters etc.

                if (_filterBySignature && _signatureTypes.Length > 0)
                {
                    var ps = m.GetParameters();
                    if (ps.Length != _signatureTypes.Length)
                        continue;

                    bool match = true;
                    for (int i = 0; i < ps.Length; ++i)
                    {
                        if (ps[i].ParameterType != _signatureTypes[i])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (!match)
                        continue;
                }

                _methods.Add(m);
            }

            _methods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        }

        string BuildSignature(MethodInfo m)
        {
            var ps = m.GetParameters();
            string[] parts = new string[ps.Length];
            for (int i = 0; i < ps.Length; ++i)
                parts[i] = $"{ps[i].ParameterType.Name} {ps[i].Name}";

            return $"{m.Name}({string.Join(", ", parts)})";
        }

        // Helper just to show the type filter nicely
        string BuildFilterSignature()
        {
            if (_signatureTypes == null || _signatureTypes.Length == 0)
                return "(no signature filter)";

            string[] names = new string[_signatureTypes.Length];
            for (int i = 0; i < _signatureTypes.Length; ++i)
                names[i] = _signatureTypes[i] != null ? _signatureTypes[i].Name : "<null>";

            return $"({string.Join(", ", names)})";
        }

        void OnGUI()
        {
            if (_methods == null)
                InitMethods();

            EditorGUILayout.Space(2f);

            // Show active filter info, if any
            if (_filterBySignature && _signatureTypes != null && _signatureTypes.Length > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Filtering methods by signature {BuildFilterSignature()}",
                    MessageType.Info
                );
            }

            // Search field
            GUI.SetNextControlName("SearchField");
            _search = EditorGUILayout.TextField(_search);
            if (Event.current.type == EventType.Layout)
                EditorGUI.FocusTextInControl("SearchField");

            // If no methods at all (after filter), show a clear message and bail
            if (_methods.Count == 0)
            {
                EditorGUILayout.Space(4f);

                if (_filterBySignature && _signatureTypes.Length > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"No methods found on '{_targetType.Name}' matching signature {BuildFilterSignature()}.\n" +
                        "Try changing the filter or selecting another component type.",
                        MessageType.Warning
                    );
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"No instance methods found on '{_targetType.Name}'.",
                        MessageType.Warning
                    );
                }

                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var m in _methods)
            {
                string sig = BuildSignature(m);
                if (!string.IsNullOrEmpty(_search) &&
                    sig.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (GUILayout.Button(sig, EditorStyles.label))
                {
                    _onSelect?.Invoke(m);
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
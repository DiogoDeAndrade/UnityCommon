using System;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UC.Editor
{ 
    [CustomPropertyDrawer(typeof(FunctionCall))]
    public class FunctionCallDrawer : PropertyDrawer
    {
        FunctionCallOptionsAttribute GetOptions()
        {
            if (fieldInfo == null)
                return null;

            return (FunctionCallOptionsAttribute)Attribute.GetCustomAttribute(fieldInfo, typeof(FunctionCallOptionsAttribute));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var options = GetOptions();
            bool showParams = options?.ShowParameters ?? true;

            float lineH = EditorGUIUtility.singleLineHeight;
            float h = lineH + 4f; // first row

            if (!showParams)
                return h;

            var parametersProp = property.FindPropertyRelative("parameters");
            int paramCount = parametersProp != null ? parametersProp.arraySize : 0;

            h += lineH + 2f;               // "Parameters" label
            h += paramCount * (lineH + 2f);
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var options = GetOptions();
            bool showParams = options?.ShowParameters ?? true;
            bool filterBySig = options?.FilterBySignature ?? false;
            Type[] signatureTypes = options?.SignatureTypes ?? Array.Empty<Type>();

            EditorGUI.BeginProperty(position, label, property);

            var componentNameProp = property.FindPropertyRelative("componentName");
            var functionNameProp = property.FindPropertyRelative("functionName");
            var parametersProp = property.FindPropertyRelative("parameters");

            float lineH = EditorGUIUtility.singleLineHeight;
            float y = position.y;

            // --- FIRST ROW: label + component + function ---
            var rowRect = new Rect(position.x, y, position.width, lineH);
            float labelWid = EditorGUIUtility.labelWidth;

            EditorGUI.LabelField(new Rect(rowRect.x, rowRect.y, labelWid, lineH), label);

            float remaining = rowRect.width - labelWid;
            float half = (remaining - 6f) * 0.5f;

            var compFieldRect = new Rect(rowRect.x + labelWid, y, half - 20f, lineH);
            var compBtnRect = new Rect(compFieldRect.xMax + 2f, y, 18f, lineH);

            var funcFieldRect = new Rect(compBtnRect.xMax + 6f, y, half - 20f, lineH);
            var funcBtnRect = new Rect(funcFieldRect.xMax + 2f, y, 18f, lineH);

            // ---- COMPONENT FIELD ----
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(
                    compFieldRect,
                    string.IsNullOrEmpty(componentNameProp.stringValue)
                        ? "<Component>"
                        : componentNameProp.stringValue
                );
            }

            if (GUI.Button(compBtnRect, "..."))
            {
                var screenRect = GUIUtility.GUIToScreenRect(compBtnRect);
                FunctionCallTypeSearchWindow.Show(screenRect, t =>
                {
                    componentNameProp.stringValue = t != null ? t.FullName : "";
                    functionNameProp.stringValue = "";
                    parametersProp.arraySize = 0;
                    property.serializedObject.ApplyModifiedProperties();
                });
            }

            // ---- FUNCTION FIELD ----
            var componentType = FunctionCall.GetTypeFromString(componentNameProp.stringValue);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(
                    funcFieldRect,
                    string.IsNullOrEmpty(functionNameProp.stringValue)
                        ? "<Function>"
                        : functionNameProp.stringValue
                );
            }

            using (new EditorGUI.DisabledScope(componentType == null))
            {
                if (GUI.Button(funcBtnRect, "..."))
                {
                    var screenRect = GUIUtility.GUIToScreenRect(funcBtnRect);
                    FunctionCallMethodSearchWindow.Show(
                        screenRect,
                        componentType,
                        filterBySig,
                        signatureTypes,
                        m =>
                        {
                            if (m != null)
                            {
                                functionNameProp.stringValue = m.Name;

                                var ps = m.GetParameters();
                                parametersProp.arraySize = ps.Length;
                                for (int i = 0; i < ps.Length; ++i)
                                {
                                    parametersProp.GetArrayElementAtIndex(i).stringValue = "";
                                }
                            }
                            else
                            {
                                functionNameProp.stringValue = "";
                                parametersProp.arraySize = 0;
                            }

                            property.serializedObject.ApplyModifiedProperties();
                        });
                }
            }

            if (!showParams)
            {
                EditorGUI.EndProperty();
                return;
            }

            // --- PARAMETERS BLOCK ---
            y += lineH + 4f;

            // Find the specific method for labels/types
            MethodInfo methodInfo = null;
            if (componentType != null && !string.IsNullOrEmpty(functionNameProp.stringValue))
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var m in componentType.GetMethods(flags))
                {
                    if (m.Name != functionNameProp.stringValue)
                        continue;

                    if (filterBySig && signatureTypes.Length > 0)
                    {
                        var ps = m.GetParameters();
                        if (ps.Length != signatureTypes.Length)
                            continue;

                        bool match = true;
                        for (int i = 0; i < ps.Length; ++i)
                        {
                            if (ps[i].ParameterType != signatureTypes[i])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (!match) continue;
                    }

                    if (m.GetParameters().Length == parametersProp.arraySize)
                    {
                        methodInfo = m;
                        break;
                    }
                }
            }

            var labelRect = new Rect(position.x, y, position.width, lineH);
            EditorGUI.LabelField(labelRect, "Parameters");
            y += lineH + 2f;

            EditorGUI.indentLevel++;

            for (int i = 0; i < parametersProp.arraySize; ++i)
            {
                var element = parametersProp.GetArrayElementAtIndex(i);

                Type paramType = null;
                string paramLabel = $"Param {i}";
                if (methodInfo != null)
                {
                    var p = methodInfo.GetParameters()[i];
                    paramType = p.ParameterType;
                    paramLabel = $"{p.Name} ({p.ParameterType.Name})";
                }

                var row = new Rect(position.x, y, position.width, lineH);
                DrawParameterField(row, element, paramType, paramLabel);
                y += lineH + 2f;
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        // --- Draw a typed widget while storing in string field ---

        static void DrawParameterField(Rect rect, SerializedProperty element, Type paramType, string label)
        {
            if (paramType == null || paramType == typeof(string))
            {
                element.stringValue = EditorGUI.TextField(rect, label, element.stringValue);
                return;
            }

            var ci = CultureInfo.InvariantCulture;
            string s = element.stringValue ?? "";

            if (paramType == typeof(int))
            {
                int.TryParse(s, NumberStyles.Integer, ci, out int v);
                v = EditorGUI.IntField(rect, label, v);
                element.stringValue = v.ToString(ci);
                return;
            }

            if (paramType == typeof(float))
            {
                float.TryParse(s, NumberStyles.Float, ci, out float v);
                v = EditorGUI.FloatField(rect, label, v);
                element.stringValue = v.ToString(ci);
                return;
            }

            if (paramType == typeof(bool))
            {
                bool.TryParse(s, out bool v);
                v = EditorGUI.Toggle(rect, label, v);
                element.stringValue = v ? "true" : "false";
                return;
            }

            if (paramType == typeof(Vector2))
            {
                Vector2 v = FunctionCall.ParseVector2(s, ci);
                v = EditorGUI.Vector2Field(rect, label, v);
                element.stringValue = $"{v.x.ToString(ci)},{v.y.ToString(ci)}";
                return;
            }

            if (paramType == typeof(Vector3))
            {
                Vector3 v = FunctionCall.ParseVector3(s, ci);
                v = EditorGUI.Vector3Field(rect, label, v);
                element.stringValue = $"{v.x.ToString(ci)},{v.y.ToString(ci)},{v.z.ToString(ci)}";
                return;
            }

            if (paramType == typeof(Vector4))
            {
                Vector4 v = FunctionCall.ParseVector4(s, ci);
                v = EditorGUI.Vector4Field(rect, label, v);
                element.stringValue = $"{v.x.ToString(ci)},{v.y.ToString(ci)},{v.z.ToString(ci)},{v.w.ToString(ci)}";
                return;
            }

            if (paramType == typeof(Quaternion))
            {
                // store as x,y,z,w – edit as Vector4
                Vector4 v4 = FunctionCall.ParseVector4(s, ci);
                v4 = EditorGUI.Vector4Field(rect, label, v4);
                element.stringValue = $"{v4.x.ToString(ci)},{v4.y.ToString(ci)},{v4.z.ToString(ci)},{v4.w.ToString(ci)}";
                return;
            }

            if (paramType == typeof(Color))
            {
                // store as HTML "#RRGGBBAA"
                Color c = FunctionCall.ParseColor(s, ci);
                c = EditorGUI.ColorField(rect, label, c);
                element.stringValue = "#" + ColorUtility.ToHtmlStringRGBA(c);
                return;
            }

            // Unsupported type -> show disabled text
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(rect, label, element.stringValue);
            }
        }
    }
}
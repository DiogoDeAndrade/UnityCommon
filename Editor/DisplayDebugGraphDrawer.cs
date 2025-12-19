using System;
using System.Reflection;
using UC.RPG;
using UnityEditor;
using UnityEngine;
using System.Collections;
using NaughtyAttributes.Editor;

namespace UC.Editor
{
    [CustomPropertyDrawer(typeof(DisplayDebugGraphAttribute))]
    public class DisplayDebugGraphDrawer : PropertyDrawer
    {
        const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var attr = (DisplayDebugGraphAttribute)attribute;

            if ((!TryGetTargetObject(property, out var target)) || (target == null))
                return 0f;

            if (!GetBoolMember(target, attr.showMember, defaultValue: true))
                return 0f;

            // one line for the title + graph block
            return EditorGUIUtility.standardVerticalSpacing + attr.height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (DisplayDebugGraphAttribute)attribute;

            var target = GetParentObject(property);
            if (target == null)
                return;

            if (!GetBoolMember(target, attr.showMember, defaultValue: true))
                return;

            float xMin = GetFloatMember(target, attr.minMember, 0f);
            float xMax = GetFloatMember(target, attr.maxMember, 1f);

            // Basic safety
            if (xMax < xMin) (xMin, xMax) = (xMax, xMin);
            if (Mathf.Approximately(xMax, xMin)) xMax = xMin + 1f;

            // Graph rect
            var graphRect = new Rect(position.x, position.y, position.width, attr.height);

            // Resolve eval method: float Eval(float x)
            var evalMethod = FindMethod(target, attr.evalMember, new[] { typeof(float) }, typeof(float));
            if (evalMethod == null)
            {
                DrawError(graphRect, $"DisplayDebugGraph: missing evaluation method '{attr.evalMember}(float)'.");
                return;
            }

            MethodInfo labelMethod = null;
            if (!string.IsNullOrEmpty(attr.labelFunction))
            {
                labelMethod = FindMethod(target, attr.labelFunction, new[] { typeof(float), typeof(float), typeof(bool) }, typeof(string));
                if (labelMethod == null)
                {
                    DrawError(graphRect, $"DisplayDebugGraph: missing label method '{attr.labelFunction}(float, float, bool)'.");
                    return;
                }
            }

            float spacing = attr.sampleSpacing;
            if (spacing <= 0f)
            {
                spacing = (xMax - xMin) / 64f;
            }
            float hSpacing = attr.hSpacing;
            if (hSpacing <= 0f)
            {
                hSpacing = (xMax - xMin) * 0.25f;
            }

            GUIUtils.DrawPreviewGraph(graphRect, attr.title, attr.padding, xMin, xMax, spacing, hSpacing, (x) => InvokeEval(target, evalMethod, x), (x, y, isVertical) => InvokeLabel(target, labelMethod, x, y, isVertical), vDivs : attr.vDivs);
        }

        // ---------------- helpers ----------------

        static bool TryGetTargetObject(SerializedProperty property, out object target)
        {
            target = property.serializedObject.targetObject;
            return target != null;
        }

        static MethodInfo FindMethod(object target, string methodName, Type[] types, Type returnType)
        {
            if (string.IsNullOrEmpty(methodName)) return null;

            var t = target.GetType();

            // Prefer exact float -> float
            var mi = t.GetMethod(methodName, FLAGS, null, types, null);
            if (mi == null) return null;
            if (mi.ReturnType != returnType) return null;
            return mi;
        }

        static float InvokeEval(object target, MethodInfo mi, float x)
        {
            try
            {
                return (float)mi.Invoke(target, new object[] { x });
            }
            catch
            {
                return 0f;
            }
        }

        static string InvokeLabel(object target, MethodInfo mi, float x, float y, bool isVertical)
        {
            try
            {
                return (string)mi.Invoke(target, new object[] { x, y, isVertical });
            }
            catch
            {
                return null;
            }
        }

        static bool GetBoolMember(object target, string name, bool defaultValue)
        {
            if (string.IsNullOrEmpty(name)) return defaultValue;

            var t = target.GetType();

            var p = t.GetProperty(name, FLAGS);
            if (p != null && p.PropertyType == typeof(bool))
                return (bool)p.GetValue(target);

            var f = t.GetField(name, FLAGS);
            if (f != null && f.FieldType == typeof(bool))
                return (bool)f.GetValue(target);

            return defaultValue;
        }

        static float GetFloatMember(object target, string name, float defaultValue)
        {
            if (string.IsNullOrEmpty(name)) return defaultValue;

            var t = target.GetType();

            var p = t.GetProperty(name, FLAGS);
            if (p != null && p.PropertyType == typeof(float))
                return (float)p.GetValue(target);

            var f = t.GetField(name, FLAGS);
            if (f != null && f.FieldType == typeof(float))
                return (float)f.GetValue(target);

            return defaultValue;
        }

        static void DrawError(Rect r, string msg)
        {
            EditorGUI.HelpBox(r, msg, MessageType.Warning);
        }

        static object GetParentObject(SerializedProperty property)
        {
            object obj = property.serializedObject.targetObject;
            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            // walk up to the parent of the property
            for (int i = 0; i < elements.Length - 1; i++)
            {
                string element = elements[i];

                if (element.Contains("["))
                {
                    string elementName = element[..element.IndexOf("[", StringComparison.Ordinal)];
                    int index = int.Parse(element[(element.IndexOf("[", StringComparison.Ordinal) + 1)..element.IndexOf("]", StringComparison.Ordinal)]);

                    obj = GetMemberValue(obj, elementName);
                    if (obj is IList list)
                        obj = list[index];
                    else
                        return null;
                }
                else
                {
                    obj = GetMemberValue(obj, element);
                }

                if (obj == null)
                    return null;
            }

            return obj;
        }

        static object GetMemberValue(object source, string name)
        {
            if (source == null) return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = source.GetType();

            while (type != null)
            {
                var f = type.GetField(name, flags);
                if (f != null) return f.GetValue(source);

                var p = type.GetProperty(name, flags);
                if (p != null) return p.GetValue(source);

                type = type.BaseType;
            }

            return null;
        }
    }
}

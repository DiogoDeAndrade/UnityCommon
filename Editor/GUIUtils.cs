using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UC
{

    static public class GUIUtils
    {
        public delegate void GenTexture(string name);

        static public Rect DrawOutlineLabel(string text, GUIStyle style, Color outlineColor, Color backgroundColor, Color textColor)
        {
            Rect titleRect = EditorGUILayout.BeginVertical("box");
            Rect baseRect = new Rect(titleRect.x, titleRect.y, EditorGUIUtility.currentViewWidth - 20 - titleRect.x, style.fontSize + 14);
            EditorGUI.DrawRect(baseRect, outlineColor);
            EditorGUI.DrawRect(new Rect(titleRect.x + 2, titleRect.y + 2, EditorGUIUtility.currentViewWidth - 20 - titleRect.x - 4, style.fontSize + 10), backgroundColor);
            var prevColor = style.normal.textColor;
            style.normal.textColor = textColor;
            EditorGUI.LabelField(new Rect(titleRect.x + 10, titleRect.y + 6, EditorGUIUtility.currentViewWidth - 20 - titleRect.x - 4, style.fontSize), text, style);
            style.normal.textColor = prevColor;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(style.fontSize + 14);

            return baseRect;
        }

        static Dictionary<string, GUIStyle> styles;

        static public GUIStyle GetLabelStyle(string color)
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            string n = $"Label{color}";
            GUIStyle labelStyle;
            styles.TryGetValue(n, out labelStyle);
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle();
                labelStyle.fontSize = 12;
                labelStyle.fixedHeight = 12;
                labelStyle.clipping = TextClipping.Overflow;
                labelStyle.normal.textColor = ColorFromHex(color);
                styles.Add(n, labelStyle);
            }
            return labelStyle;
        }
        static public GUIStyle GetCenteredLabelStyle(string color, Texture2D texture = null)
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            string n = $"CenteredLabel{color}";
            GUIStyle labelStyle;
            styles.TryGetValue(n, out labelStyle);
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle();
                labelStyle.fontSize = 12;
                labelStyle.fixedHeight = 12;
                labelStyle.clipping = TextClipping.Overflow;
                labelStyle.normal.textColor = ColorFromHex(color);
                labelStyle.normal.background = texture;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                styles.Add(n, labelStyle);
            }
            return labelStyle;
        }

        static public GUIStyle GetTooltipTextStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle titleStyle;
            styles.TryGetValue("TooltipTextStyle", out titleStyle);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 12;
                titleStyle.fixedHeight = 12;
                titleStyle.clipping = TextClipping.Overflow;
                titleStyle.alignment = TextAnchor.UpperLeft;
                titleStyle.normal.textColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                titleStyle.hover.textColor = new Color(0.3f, 0.3f, 0.3f, 1.0f);
                titleStyle.wordWrap = true;
                styles.Add("TooltipTextStyle", titleStyle);
            }
            return titleStyle;
        }

        static public GUIStyle GetActionDelayTextStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle titleStyle;
            styles.TryGetValue("ActionDelayText", out titleStyle);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 8;
                titleStyle.fixedHeight = 8;
                titleStyle.clipping = TextClipping.Overflow;
                styles.Add("ActionDelayText", titleStyle);
            }
            return titleStyle;
        }


        static public GUIStyle GetBehaviourTitleStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle titleStyle;
            styles.TryGetValue("BehaviourTitle", out titleStyle);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 24;
                titleStyle.fixedHeight = 24;
                titleStyle.normal.textColor = ColorFromHex("#0e1a51");
                titleStyle.clipping = TextClipping.Overflow;
                titleStyle.wordWrap = false;
                styles.Add("BehaviourTitle", titleStyle);
            }
            return titleStyle;
        }

        static public GUIStyle GetTriggerTitleStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle titleStyle;
            styles.TryGetValue("TriggerTitle", out titleStyle);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 24;
                titleStyle.fixedHeight = 24;
                titleStyle.normal.textColor = ColorFromHex("#0e1a51");
                titleStyle.clipping = TextClipping.Overflow;
                titleStyle.wordWrap = false;
                styles.Add("TriggerTitle", titleStyle);
            }
            return titleStyle;
        }

        static public GUIStyle GetActionExplanationStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle explanationStyle;
            styles.TryGetValue("ActionExplanation", out explanationStyle);
            if (explanationStyle == null)
            {
                explanationStyle = new GUIStyle(GUI.skin.label);
                explanationStyle.fontSize = 10;
                explanationStyle.fixedHeight = 10;
                explanationStyle.alignment = TextAnchor.UpperLeft;
                explanationStyle.normal.textColor = ColorFromHex("#0e1a51");
                explanationStyle.clipping = TextClipping.Overflow;
                explanationStyle.wordWrap = false;
                styles.Add("ActionExplanation", explanationStyle);
            }
            return explanationStyle;
        }

        static public GUIStyle GetLogStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle explanationStyle;
            styles.TryGetValue("Log", out explanationStyle);
            if (explanationStyle == null)
            {
                explanationStyle = new GUIStyle(GUI.skin.label);
                explanationStyle.fontSize = 12;
                explanationStyle.fixedHeight = 12;
                explanationStyle.alignment = TextAnchor.UpperLeft;
                explanationStyle.normal.textColor = ColorFromHex("#000000");
                explanationStyle.clipping = TextClipping.Overflow;
                explanationStyle.wordWrap = false;
                styles.Add("Log", explanationStyle);
            }
            return explanationStyle;
        }

        static public GUIStyle GetTriggerExplanationStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle explanationStyle;
            styles.TryGetValue("TriggerExplanation", out explanationStyle);
            if (explanationStyle == null)
            {
                explanationStyle = new GUIStyle(GUI.skin.label);
                explanationStyle.fontSize = 10;
                explanationStyle.fixedHeight = 10;
                explanationStyle.alignment = TextAnchor.UpperLeft;
                explanationStyle.normal.textColor = ColorFromHex("#0e1a51");
                explanationStyle.clipping = TextClipping.Overflow;
                explanationStyle.wordWrap = false;
                styles.Add("TriggerExplanation", explanationStyle);
            }
            return explanationStyle;
        }

        static public GUIStyle GetTriggerActionExplanationStyle()
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle explanationStyle;
            styles.TryGetValue("TriggerActionExplanation", out explanationStyle);
            if (explanationStyle == null)
            {
                explanationStyle = new GUIStyle(GUI.skin.label);
                explanationStyle.fontSize = 10;
                explanationStyle.fixedHeight = 10;
                explanationStyle.alignment = TextAnchor.UpperLeft;
                explanationStyle.normal.textColor = ColorFromHex("#A0A0A0");
                explanationStyle.clipping = TextClipping.Overflow;
                explanationStyle.wordWrap = false;
                styles.Add("TriggerActionExplanation", explanationStyle);
            }
            return explanationStyle;
        }

        static public GUIStyle GetButtonStyle(string name)
        {
            if (styles == null) styles = new Dictionary<string, GUIStyle>();

            GUIStyle style;
            styles.TryGetValue(name, out style);
            if (style == null)
            {
                style = CreateButtonStyle(name);
                styles.Add(name, style);
            }
            else
            {
                // Check if style has become invalid (no textures)
                if ((style.normal.background == null) ||
                    (style.hover.background == null))
                {
                    styles.Remove(name);
                    style = CreateButtonStyle(name);
                    styles.Add(name, style);
                }
            }
            return style;
        }

        static public GUIStyle CreateButtonStyle(string name)
        {
            var style = new GUIStyle("Button");
            style.normal.background = GetTexture($"{name}Normal");
            style.normal.scaledBackgrounds = null;
            style.hover.background = GetTexture($"{name}Hover");
            style.hover.scaledBackgrounds = null;

            return style;
        }

        static public Color ColorFromHex(string htmlColor)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(htmlColor, out color)) return color;

            return Color.magenta;
        }

        static public Texture2D GetColorTexture(string name, Color color)
        {
            var ret = GetTexture(name);
            if (ret != null) return ret;

            var bitmap = new GUIBitmap(4, 4);
            bitmap.Fill(color);

            return BitmapToTexture(name, bitmap);
        }

        static Dictionary<string, Texture2D> textures;
        static public Texture2D AddTexture(string name, Texture2D texture)
        {
            if (textures == null) textures = new Dictionary<string, Texture2D>();

            textures[name] = texture;

            return texture;
        }

        static public Texture2D AddTexture(string name, GUIBitmap bitmap)
        {
            return BitmapToTexture(name, bitmap);
        }

        static public Texture2D GetTexture(string name)
        {
            if (textures == null) textures = new Dictionary<string, Texture2D>();

            Texture2D texture;
            if (textures.TryGetValue(name, out texture))
            {
                if (texture) return texture;
            }

            // Find the singleton scriptable object
            var config = UnityCommonEditorConfig.Instance;
            if (config != null)
            {
                texture = config.GetTexture(name);
                if (texture)
                {
                    AddTexture(name, texture);
                    return texture;
                }
            }

            // Find path
            string path = $"Assets/OkapiKit/UI/{name}.png";
            if (!File.Exists(path))
            {
                path = System.IO.Path.GetFullPath($"Packages/com.videojogoslusofona.okapikit/UI/{name}.png");
                if (!File.Exists(path))
                {
                    path = $"Assets/Externals/UnityCommon/Editor/Icons/{name}.png";
                    if (!File.Exists(path))
                    {
                        return null;
                    }
                }
            }

            texture = new Texture2D(1, 1);
            if (texture.LoadImage(File.ReadAllBytes(path)))
            {
                texture.Apply();
                AddTexture(name, texture);
                return texture;
            }

            return null;
        }

        static public Texture2D BitmapToTexture(string name, GUIBitmap bitmap)
        {
            Texture2D result = new Texture2D(bitmap.width, bitmap.height);
            result.SetPixels(bitmap.bitmap);
            result.filterMode = FilterMode.Point;
            result.Apply();

            if (name != "")
            {
                AddTexture(name, result);
            }

            return result;
        }

        static readonly Dictionary<Type, MonoScript> _cache = new();

        public static MonoScript FindScriptForType(Type type)
        {
            if (type == null) return null;

            if (_cache.TryGetValue(type, out var cached))
                return cached; // can be null too (negative cache)

            // 1) Fast path: likely filename match
            var guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                    return _cache[type] = script;
            }

            // 2) Robust fallback: scan all scripts and compare GetClass()
            guids = AssetDatabase.FindAssets("t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) continue;

                // GetClass() returns the main class Unity associates with that script asset.
                if (script.GetClass() == type)
                {
                    _cache[type] = script;
                    return script;
                }
            }

            // 3) Extra robus fallback: scan all files for the actual text
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var text = File.ReadAllText(path);

                // naive but effective: class/struct record declarations
                if (text.Contains($"class {type.Name}") ||
                    text.Contains($"struct {type.Name}") ||
                    text.Contains($"record {type.Name}"))
                {
                    _cache[type] = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    return _cache[type];
                }
            }

            // Cache miss (so we don’t keep scanning)
            _cache[type] = null;
            return null;
        }
        public static void ClearCache() => _cache.Clear();

        static void DrawHGrid(Rect plot, float y01, LabelFunctionDelegate labelFunction)
        {
            float y = Mathf.Lerp(plot.yMax, plot.yMin, y01);
            Handles.color = new Color(1, 1, 1, y01 == 0.5f ? 0.18f : 0.10f);
            Handles.DrawLine(new Vector3(plot.xMin, y), new Vector3(plot.xMax, y));

            var str = labelFunction != null ? labelFunction(0, y01, false) : null;
            if (!string.IsNullOrEmpty(str))
                GUI.Label(new Rect(plot.xMin - 34, y - 7, 32, 14), str, EditorStyles.miniLabel);
        }

        public delegate float GraphEvaluateFunction(float x);
        public delegate string LabelFunctionDelegate(float x, float y, bool isVerticalAxis);

        public static void DrawPreviewGraph(Rect rect, string title, float padding, float xMin, float xMax, float sampleSpacing, float hGridSpacing,
                                            GraphEvaluateFunction graphEvaluate, LabelFunctionDelegate labelFunction,
                                            bool fixedY = false, float yMin = 0f, float yMax = 1f,
                                            float? xCenter = null, Color? centralColor = null)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 0.12f));

            // Header
            if (!string.IsNullOrEmpty(title))
                EditorGUI.LabelField(new Rect(rect.x + 6, rect.y + 2, rect.width - 12, 16), title, EditorStyles.miniLabel);

            // Plot area
            Rect plot = rect;
            plot.xMin += padding;
            plot.xMax -= padding;
            plot.yMin += 18f;
            plot.yMax -= 16f;

            Handles.BeginGUI();

            // Border
            Handles.color = new Color(1, 1, 1, 0.15f);
            Handles.DrawAAPolyLine(1f, new Vector3[]
            {
                new(plot.xMin, plot.yMin),
                new(plot.xMax, plot.yMin),
                new(plot.xMax, plot.yMax),
                new(plot.xMin, plot.yMax),
                new(plot.xMin, plot.yMin),
            });

            // Horizontal grid (fixed 0..1)
            DrawHGrid(plot, 0.0f, labelFunction);
            DrawHGrid(plot, 0.25f, labelFunction);
            DrawHGrid(plot, 0.5f, labelFunction);
            DrawHGrid(plot, 0.75f, labelFunction);
            DrawHGrid(plot, 1.0f, labelFunction);

            // Vertical grid (integer diffs)
            float xRange = xMax - xMin;
            int gridPointCount = Mathf.FloorToInt((xMax - xMin) / hGridSpacing) + 1;
            int samplePointCount = Mathf.FloorToInt((xMax - xMin) / sampleSpacing) + 1;

            bool hasCenter = xCenter.HasValue && centralColor.HasValue;
            int centerIdx = -1;
            float centerX = 0f;

            if (hasCenter)
            {
                centerX = xCenter.Value;

                // If it’s outside the plotted range, ignore
                if (centerX < xMin || centerX > xMax)
                    hasCenter = false;
                else
                {
                    // Choose the closest sample index
                    centerIdx = Mathf.RoundToInt((centerX - xMin) / sampleSpacing);
                    centerIdx = Mathf.Clamp(centerIdx, 0, samplePointCount - 1);
                }
            }

            for (int i = 0; i < gridPointCount; i++)
            {
                float d = xMin + i * hGridSpacing;
                float u = (d - xMin) / xRange;
                float x = Mathf.Lerp(plot.xMin, plot.xMax, u);

                bool isCenter = hasCenter && Mathf.Abs(d - centerX) <= (hGridSpacing * 0.5f);

                Handles.color = isCenter ? new Color(centralColor.Value.r, centralColor.Value.g, centralColor.Value.b, 0.35f) : new Color(1, 1, 1, d == 0 ? 0.22f : 0.10f);

                Handles.DrawLine(new Vector3(x, plot.yMin), new Vector3(x, plot.yMax));
            }

            // Curve: connect the integer points directly (no stepping)
            Vector3[] pts = new Vector3[samplePointCount];

            // Compute vertical range
            if (!fixedY)
            {
                yMin = float.MaxValue;
                yMax = -float.MaxValue;

                for (int i = 0; i < samplePointCount; i++)
                {
                    float d = xMin + i * sampleSpacing;
                    float p = graphEvaluate(d);

                    if (p < yMin) yMin = p;
                    if (p > yMax) yMax = p;
                }
            }

            // Draw curve (normalized to vertical range)
            int idx = 0;
            float yRange = yMax - yMin;
            bool flat = Mathf.Abs(yRange) < 1e-6f;
            if (flat) 
            { 
                yMin -= 0.5f; 
                yMax += 0.5f; 
                yRange = yMax - yMin; 
            }

            for (int i = 0; i < samplePointCount; i++)
            {
                float d = xMin + i * sampleSpacing;
                float u = (d - xMin) / xRange;
                float p = (graphEvaluate(d) - yMin) / yRange;

                float x = Mathf.Lerp(plot.xMin, plot.xMax, u);
                float y = Mathf.Lerp(plot.yMax, plot.yMin, p);

                pts[idx++] = new Vector3(x, y);
            }

            Handles.color = new Color(0.35f, 0.9f, 1f, 0.9f);
            Handles.DrawAAPolyLine(2f, pts);

            // Markers + labels
            for (int i = 0; i < samplePointCount; i++)
            {
                float d = xMin + i * sampleSpacing;
                float u = (d - xMin) / xRange;
                float v = graphEvaluate(d);
                float p = (v - yMin) / yRange;

                float x = Mathf.Lerp(plot.xMin, plot.xMax, u);
                float y = Mathf.Lerp(plot.yMax, plot.yMin, p);
            }

            for (int i = 0; i < gridPointCount; i++)
            {
                float d = xMin + i * hGridSpacing;
                float u = (d - xMin) / xRange;
                float v = graphEvaluate(d);
                float p = (v - yMin) / yRange;

                float x = Mathf.Lerp(plot.xMin, plot.xMax, u);
                float y = Mathf.Lerp(plot.yMax, plot.yMin, p);

                bool isCenter = (hasCenter) && (Mathf.Abs(d - centerX) <= (hGridSpacing * 0.5f)); // closest grid line

                Handles.color = isCenter ? centralColor.Value : Color.white; 
                Handles.DrawSolidDisc(new Vector3(x, y), Vector3.forward, 2.4f);

                float labelW = 60.0f;
                GUI.Label(new Rect(x - labelW * 0.5f, rect.yMax - 16, labelW, 16), d.ToString("0.00"), EditorStyles.centeredGreyMiniLabel);

                if (labelFunction != null)
                {
                    var str = labelFunction(d, v, false);
                    if (!string.IsNullOrEmpty(str))
                    {
                        GUI.Label(new Rect(x - labelW * 0.5f, y - 14, labelW, 16), str, EditorStyles.centeredGreyMiniLabel);
                    }
                }
            }

            Handles.EndGUI();
        }

        public static object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            if (prop == null)
                return null;

            object obj = prop.serializedObject.targetObject;
            string path = prop.propertyPath.Replace(".Array.data[", "[");

            var elements = path.Split('.');

            foreach (var element in elements)
            {
                if (obj == null)
                    return null;

                if (element.Contains("["))
                {
                    string name = element.Substring(0, element.IndexOf("["));
                    int index = Convert.ToInt32(
                        element.Substring(element.IndexOf("[") + 1).TrimEnd(']')
                    );

                    obj = GetFieldValue(obj, name);

                    if (obj is IList list)
                    {
                        if (index < 0 || index >= list.Count)
                            return null;

                        obj = list[index];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    obj = GetFieldValue(obj, element);
                }
            }

            return obj;
        }

        static object GetFieldValue(object source, string name)
        {
            if (source == null)
                return null;

            Type type = source.GetType();

            while (type != null)
            {
                FieldInfo f = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (f != null)
                    return f.GetValue(source);

                type = type.BaseType;
            }

            return null;
        }
    }
}
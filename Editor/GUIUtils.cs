using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

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
    }
}
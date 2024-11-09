using UnityEditor;
using UnityEngine;
using System.Linq;

public abstract class UnityCommonEditor : UnityEditor.Editor
{
    protected abstract GUIStyle GetTitleSyle();

    protected abstract string GetTitle();

    protected abstract Texture2D GetIcon();

    protected abstract (Color, Color, Color) GetColors();

    protected virtual void OnEnable()
    {
    }

    protected Rect titleRect;

    protected virtual bool WriteTitle()
    {
        GUIStyle styleTitle = GetTitleSyle();

        (var backgroundColor, var textColor, var separatorColor) = GetColors();

        // Background and title
        float inspectorWidth = EditorGUIUtility.currentViewWidth - 20;
        titleRect = EditorGUILayout.BeginVertical("box");
        Rect rect = new Rect(titleRect.x, titleRect.y, inspectorWidth - titleRect.x, styleTitle.fontSize + 14);
        Rect fullRect = rect;
        Color barColor = backgroundColor;

        EditorGUI.DrawRect(fullRect, barColor);
        var prevColor = styleTitle.normal.textColor;
        styleTitle.normal.textColor = textColor;
        GUI.DrawTexture(new Rect(titleRect.x + 10, titleRect.y + 4, 32, 32), GetIcon(), ScaleMode.ScaleToFit, true, 1.0f);
        EditorGUI.LabelField(new Rect(titleRect.x + 50, titleRect.y + 6, inspectorWidth - 20 - titleRect.x - 4, styleTitle.fontSize), GetTitle(), styleTitle);
        styleTitle.normal.textColor = prevColor;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(fullRect.height);

        return true;
    }
}

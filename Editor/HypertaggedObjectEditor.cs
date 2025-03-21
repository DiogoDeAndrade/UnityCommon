using UnityEngine;
using UnityEditor;

namespace OkapiKit.Editor
{
    [CustomEditor(typeof(HypertaggedObject), true)]
    [CanEditMultipleObjects]
    public class HypertaggedObjectEditor : UnityCommonEditor
    {
        SerializedProperty propHypertags;

        protected override void OnEnable()
        {
            base.OnEnable();

            propHypertags = serializedObject.FindProperty("hypertag");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (WriteTitle())
            {
                StdEditor(false);
            }
        }

        protected void StdEditor(bool useOriginalEditor = true)
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propHypertags, new GUIContent("Tags", "Tags assigned to this object"), true);

            EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();
        }

        protected override GUIStyle GetTitleSyle()
        {
            return GUIUtils.GetBehaviourTitleStyle();
        }

        protected override string GetTitle()
        {
            var obj = target as HypertaggedObject;
            if ((obj != null) && (obj.hypertag != null)) return obj.hypertag.name;

            return "Hypertag";
        }


        protected override (Texture2D, Rect) GetIcon()
        {
            var varTexture = GUIUtils.GetTexture("Tag");
            return (varTexture, new Rect(0.0f, 0.0f, 1.0f, 1.0f));
        }

        protected override (Color, Color, Color) GetColors() => (GUIUtils.ColorFromHex("#fdd0f6"), GUIUtils.ColorFromHex("#2f4858"), GUIUtils.ColorFromHex("#fea0f0"));

    }
}
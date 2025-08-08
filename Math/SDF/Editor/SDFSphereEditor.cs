using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UC
{
    [CustomEditor(typeof(SDFSphere), true)]
    public class SDFSphereEditor : Editor
    {
        SerializedProperty offsetProp;
        SerializedProperty radiusProp;

        void OnEnable()
        {
            offsetProp = serializedObject.FindProperty("offset");
            radiusProp = serializedObject.FindProperty("radius");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(offsetProp);
            EditorGUILayout.PropertyField(radiusProp);

            serializedObject.ApplyModifiedProperties();
        }
    }   
}

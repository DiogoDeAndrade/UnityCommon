using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UC
{
    [CustomEditor(typeof(SDFComposite), true)]
    public class SDFCompositeEditor : Editor
    {
        SerializedProperty op;
        SerializedProperty operands;

        void OnEnable()
        {
            op = serializedObject.FindProperty("op");
            operands = serializedObject.FindProperty("operands");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(op);

            // Manually draw each element of 'operands'
            EditorGUILayout.LabelField("Operands:");
            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < operands.arraySize; i++)
                {
                    var element = operands.GetArrayElementAtIndex(i);

                    // Draw a box around each operand
                    EditorGUILayout.BeginVertical("box");
                    // Show the object reference field but make it read-only:
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(element, new GUIContent($"Operand #{i}"), true);
                    }
                    EditorGUILayout.EndVertical();
                }
            }


            if (GUILayout.Button("Update Operands"))
            {
                var sdf = (target as SDF);
                if (sdf != null)
                {
                    if (sdf.ownerGameObject == null)
                    {
                        Debug.LogWarning("No owner game object for SDF scriptable object!");
                    }
                    else
                    {
                        sdf.ownerGameObject.GetComponent<SDFComponent>().UpdateArgs();
                    }
                }                
            }

            serializedObject.ApplyModifiedProperties();
        }
    }   
}

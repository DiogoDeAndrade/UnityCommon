using UnityEngine;
using UnityEditor;

namespace UC.Editor
{

    [CustomEditor(typeof(AgroList))]
    public class AgroListEditor : UnityEditor.Editor
    {
        private const int MaxDisplayed = 10;
        private SerializedProperty refreshTimeProp;
        private SerializedProperty agroDecayPerTimeProp;
        private SerializedProperty agroDecayPerDistanceProp;
        private SerializedProperty is3DProp;
        private SerializedProperty agroMaxDistanceProp;

        private void OnEnable()
        {
            refreshTimeProp = serializedObject.FindProperty("refreshTime");
            agroDecayPerTimeProp = serializedObject.FindProperty("agroDecayPerTime");
            agroDecayPerDistanceProp = serializedObject.FindProperty("agroDecayPerDistance");
            is3DProp = serializedObject.FindProperty("is3D");
            agroMaxDistanceProp = serializedObject.FindProperty("agroMaxDistance");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(refreshTimeProp);
            EditorGUILayout.PropertyField(agroDecayPerTimeProp);
            EditorGUILayout.PropertyField(agroDecayPerDistanceProp);

            if (agroDecayPerDistanceProp.floatValue > 0.0f)
            {
                EditorGUILayout.PropertyField(is3DProp);
                EditorGUILayout.PropertyField(agroMaxDistanceProp);
            }

            serializedObject.ApplyModifiedProperties();

            // Draw live agro list
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Current Agro List", EditorStyles.boldLabel);

            AgroList agroListTarget = (AgroList)target;
            var listField = typeof(AgroList).GetField("agroList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var agroList = listField?.GetValue(agroListTarget) as System.Collections.IList;

            if (agroList == null || agroList.Count == 0)
            {
                EditorGUILayout.HelpBox("No active agro entries.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Agro", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            int count = Mathf.Min(agroList.Count, MaxDisplayed);
            for (int i = 0; i < count; i++)
            {
                var elem = agroList[i];
                var agroTargetField = elem.GetType().GetField("agroTarget");
                var agroValueField = elem.GetType().GetField("agroValue");

                var targetGO = agroTargetField?.GetValue(elem) as GameObject;
                var agroValue = (float)(agroValueField?.GetValue(elem) ?? 0.0f);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.ObjectField(targetGO, typeof(GameObject), true);
                EditorGUILayout.LabelField(agroValue.ToString("0.00"));
                EditorGUILayout.EndHorizontal();
            }

            if (agroList.Count > MaxDisplayed)
            {
                EditorGUILayout.LabelField($"... and {agroList.Count - MaxDisplayed} more", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
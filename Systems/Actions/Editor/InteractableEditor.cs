// Assets/Editor/InteractableEditor.cs
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace UC.Interaction.Editor
{

    [CustomEditor(typeof(Interactable), true)]
    public class InteractableEditor : UnityEditor.Editor
    {
        // Serialized fields (names taken from Interactable.cs)
        SerializedProperty _interactionVerb;
        SerializedProperty _priority;
        SerializedProperty _overrideCursor;
        SerializedProperty _cursorDef;

        SerializedProperty _conditions;       // [SerializeReference] WSKit.Condition[]
        SerializedProperty _actions;
        SerializedProperty _cooldown;
        SerializedProperty _canRetrigger;

        ReorderableList _conditionsList;
        ReorderableList _actionsList;

        void OnEnable()
        {
            // Top block (matches WSKit.Interactable fields) :contentReference[oaicite:0]{index=0}
            _interactionVerb = serializedObject.FindProperty("interactionVerb");
            _priority = serializedObject.FindProperty("_priority");
            _overrideCursor = serializedObject.FindProperty("overrideCursor");
            _cursorDef = serializedObject.FindProperty("cursorDef");

            // Action block (matches WSKit.Interactable fields) :contentReference[oaicite:1]{index=1}
            _conditions = serializedObject.FindProperty("conditions");
            _actions = serializedObject.FindProperty("actions");
            _cooldown = serializedObject.FindProperty("cooldown");
            _canRetrigger = serializedObject.FindProperty("canRetrigger");

            _conditionsList = ManagedReferenceListHelper.Build(serializedObject, _conditions, typeof(Condition), "Conditions", "Add Condition", "No conditions");

            _actionsList = ManagedReferenceListHelper.Build(serializedObject, _actions, typeof(GameAction), "Actions", "Add Action", "No actions", rightHeader: "Wait", rightHeaderWidth: BaseGameActionDrawer.WaitColumnWidth);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            
            EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(_interactionVerb, new GUIContent("Interaction Verb"));
            EditorGUILayout.PropertyField(_priority, new GUIContent("Priority"));
            EditorGUILayout.PropertyField(_overrideCursor, new GUIContent("Override Cursor"));
            if (_overrideCursor.boolValue)
            {
                EditorGUILayout.PropertyField(_cursorDef, new GUIContent("Cursor"));
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);

            // Action 
            EditorGUI.indentLevel++;

            _conditionsList.DoLayoutList();
            _actionsList.DoLayoutList();

            EditorGUILayout.PropertyField(_canRetrigger, new GUIContent("Can Retrigger"));
            if (_canRetrigger.boolValue)
            {
                EditorGUILayout.PropertyField(_cooldown, new GUIContent("Cooldown"));
            }

            EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();
        }
    }
}
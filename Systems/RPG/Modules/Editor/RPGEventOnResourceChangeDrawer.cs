using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UC.Interaction;
using UC.Interaction.Editor;

namespace UC.RPG.Editor
{
    [CustomPropertyDrawer(typeof(RPGEventOnResourceChange))]
    public class RPGEventOnResourceChangeDrawer : SOModuleDrawer
    {
        // One list per property path (per object)
        private readonly Dictionary<string, ReorderableList> _actionsLists = new Dictionary<string, ReorderableList>();
        private readonly Dictionary<string, ReorderableList> _conditionsLists= new Dictionary<string, ReorderableList>();

        private ReorderableList GetActionsList(SerializedProperty actionsProp)
        {
            if (actionsProp == null)
                return null;

            // Unique key per instance + property
            var so = actionsProp.serializedObject;
            string key = so.targetObject.GetInstanceID() + "/" + actionsProp.propertyPath;

            if ((_actionsLists.TryGetValue(key, out var list)) && (list != null))
                return list;

            // Build list just like InteractableEditor does for GameAction[]
            list = ManagedReferenceListHelper.Build(so, actionsProp, typeof(GameAction), "Actions", "Add Action", "No actions", rightHeader: "Wait", rightHeaderWidth: BaseGameActionDrawer.WaitColumnWidth);

            _actionsLists[key] = list;

            return list;
        }

        private ReorderableList GetConditionsList(SerializedProperty conditionsProp)
        {
            if (conditionsProp == null)
                return null;

            // Unique key per instance + property
            var so = conditionsProp.serializedObject;
            string key = so.targetObject.GetInstanceID() + "/" + conditionsProp.propertyPath;

            if ((_conditionsLists.TryGetValue(key, out var list)) && (list != null))
                return list;

            // Build list just like InteractableEditor does for GameAction[]
            list = ManagedReferenceListHelper.Build(so, conditionsProp, typeof(Condition), "Conditions", "Add Condition", "No conditions", rightHeader: null);

            _conditionsLists[key] = list;

            return list;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var resTypeProp = property.FindPropertyRelative("resourceType");
            var changeTypeProp = property.FindPropertyRelative("changeType");
            var conditionsProp = property.FindPropertyRelative("conditions");
            var actionsProp = property.FindPropertyRelative("actions");

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = 0f;

            // ResourceType line
            if (resTypeProp != null)
                height += EditorGUI.GetPropertyHeight(resTypeProp, true) + spacing;
            if (changeTypeProp != null)
                height += EditorGUI.GetPropertyHeight(changeTypeProp, true) + spacing;

            // Conditions list height
            if (conditionsProp != null)
            {
                var list = GetConditionsList(conditionsProp);
                if (list != null)
                {
                    height += list.GetHeight() + spacing;
                }
                else
                {
                    height += EditorGUI.GetPropertyHeight(conditionsProp, true) + spacing;
                }
            }

            // Actions list height
            if (actionsProp != null)
            {
                var list = GetActionsList(actionsProp);
                if (list != null)
                {
                    height += list.GetHeight() + spacing;
                }
                else
                {
                    height += EditorGUI.GetPropertyHeight(actionsProp, true) + spacing;
                }
            }

            if (height > 0f)
                height -= spacing;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var resTypeProp = property.FindPropertyRelative("resourceType");
            var changeTypeProp = property.FindPropertyRelative("changeType");
            var conditionsProp = property.FindPropertyRelative("conditions");
            var actionsProp = property.FindPropertyRelative("actions");

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect r = new Rect(position.x, position.y, position.width, 0f);

            // --- Resource Type ---
            if (resTypeProp != null)
            {
                float rh = EditorGUI.GetPropertyHeight(resTypeProp, true);
                r.height = rh;
                EditorGUI.PropertyField(r, resTypeProp, new GUIContent("Resource Type"));
                r.y += rh + spacing;
            }

            if (changeTypeProp != null)
            {
                float rh = EditorGUI.GetPropertyHeight(changeTypeProp, true);
                r.height = rh;
                EditorGUI.PropertyField(r, changeTypeProp, new GUIContent("Type"));
                r.y += rh + spacing;
            }

            // --- Conditions(Conditions[] as in Interactable) ---
            if (conditionsProp != null)
            {
                var list = GetConditionsList(conditionsProp);
                if (list != null)
                {
                    float lh = list.GetHeight();
                    r.height = lh;
                    r.x += 20.0f;
                    r.width -= 20.0f;
                    list.DoList(r);   // Non-layout version
                    r.y += lh + spacing;
                    r.x -= 20.0f;
                    r.width += 20.0f;
                }
                else
                {
                    float ah = EditorGUI.GetPropertyHeight(conditionsProp, true);
                    r.height = ah;
                    EditorGUI.PropertyField(r, conditionsProp, new GUIContent("Conditions"), true);
                    r.y += ah + spacing;
                }
            }

            // --- Actions (GameAction[] as in Interactable) ---
            if (actionsProp != null)
            {
                var list = GetActionsList(actionsProp);
                if (list != null)
                {
                    float lh = list.GetHeight();
                    r.height = lh;
                    r.x += 20.0f;
                    r.width -= 20.0f;
                    list.DoList(r);   // Non-layout version
                    r.y += lh + spacing;
                    r.x -= 20.0f;
                    r.width += 20.0f;
                }
                else
                {
                    float ah = EditorGUI.GetPropertyHeight(actionsProp, true);
                    r.height = ah;
                    EditorGUI.PropertyField(r, actionsProp, new GUIContent("Actions"), true);
                    r.y += ah + spacing;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}

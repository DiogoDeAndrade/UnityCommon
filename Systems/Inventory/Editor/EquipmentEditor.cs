using UnityEngine;
using UnityEditor;
using UC;
using UC.Editor;

namespace UC.Editor
{
    [CustomEditor(typeof(Equipment))]
    public class EquipmentEditor : UnityCommonEditor
    {
        private SerializedProperty linkedInventoryProperty;
        private SerializedProperty availableSlotsProperty;
        private SerializedProperty combatTextEnableProperty;
        private SerializedProperty combatTextDurationProperty;
        private SerializedProperty combatTextEquippedItemColorProperty;
        private SerializedProperty combatTextUnequippedItemColorProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            linkedInventoryProperty = serializedObject.FindProperty("linkedInventory");
            availableSlotsProperty = serializedObject.FindProperty("availableSlots");
            combatTextEnableProperty = serializedObject.FindProperty("combatTextEnable");
            combatTextDurationProperty = serializedObject.FindProperty("combatTextDuration");
            combatTextEquippedItemColorProperty = serializedObject.FindProperty("combatTextEquippedItemColor");
            combatTextUnequippedItemColorProperty = serializedObject.FindProperty("combatTextUnequippedItemColor");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Equipment equip = (Equipment)target;

            if (WriteTitle())
            {
                EditorGUI.BeginChangeCheck();

                // --- Base settings ---
                EditorGUILayout.PropertyField(linkedInventoryProperty);

                EditorGUILayout.PropertyField(availableSlotsProperty, true);

                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(combatTextEnableProperty);
                if (combatTextEnableProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(combatTextDurationProperty);
                    EditorGUILayout.PropertyField(combatTextEquippedItemColorProperty);
                    EditorGUILayout.PropertyField(combatTextUnequippedItemColorProperty);
                }

                EditorGUILayout.Space(10);

                // --- Runtime equipment view ---
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Equipped Items", EditorStyles.boldLabel);

                    int count = availableSlotsProperty.arraySize;
                    if (count == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "No available slots configured.",
                            MessageType.Info
                        );
                    }
                    else
                    {
                        EditorGUI.indentLevel++;

                        for (int i = 0; i < count; i++)
                        {
                            var slotProp = availableSlotsProperty.GetArrayElementAtIndex(i);
                            var slot = slotProp.objectReferenceValue as Hypertag;

                            string slotName = slot != null ? slot.name : "<None>";

                            Item equippedItem = null;
                            if (slot != null)
                            {
                                // Will also ensure the instance exists internally
                                equippedItem = equip.GetItem(slot);
                            }

                            string itemLabel = (equippedItem != null)
                                ? equippedItem.name
                                : "Empty";

                            // slotName : itemLabel
                            EditorGUILayout.LabelField(slotName, itemLabel);
                        }

                        EditorGUI.indentLevel--;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Enter Play Mode to see which item is equipped in each slot.",
                        MessageType.None
                    );
                }

                EditorGUI.EndChangeCheck();
            }

            serializedObject.ApplyModifiedProperties();
        }

        // --- Title bar styling, same style family as Inventory ---

        protected override GUIStyle GetTitleSyle()
        {
            return GUIUtils.GetBehaviourTitleStyle();
        }

        protected override string GetTitle()
        {
            return "Equipment";
        }

        protected override (Texture2D, Rect) GetIcon()
        {
            // If you have an "Equipment" icon registered in GUIUtils, this will use it.
            var tex = GUIUtils.GetTexture("Equipment");
            return (tex, new Rect(0f, 0f, 1f, 1f));
        }

        protected override (Color, Color, Color) GetColors()
        {
            // Same gold / yellow / brown palette as Inventory
            var main = GUIUtils.ColorFromHex("#F2D27A"); // warm gold
            var dark = GUIUtils.ColorFromHex("#8B5E2B"); // dark brown
            var light = GUIUtils.ColorFromHex("#FFE8A3"); // pale golden highlight

            return (main, dark, light);
        }

        protected override bool HasTitleShadow()
        {
            return true;
        }
    }
}

using UnityEngine;
using UnityEditor;
using UC.Editor;

namespace UC.RPG.Editor
{
    [CustomEditor(typeof(InventoryRPG))]
    public class InventoryRPGEditor : UnityCommonEditor
    {
        private SerializedProperty limitedProperty;
        private SerializedProperty maxSlotsProperty;
        private SerializedProperty enableInputProperty;
        private SerializedProperty playerInputProperty;
        private SerializedProperty inventoryButtonProperty;

        protected override void OnEnable()
        {
            base.OnEnable();

            limitedProperty = serializedObject.FindProperty("limited");
            maxSlotsProperty = serializedObject.FindProperty("maxSlots");
            enableInputProperty = serializedObject.FindProperty("enableInput");
            playerInputProperty = serializedObject.FindProperty("playerInput");
            inventoryButtonProperty = serializedObject.FindProperty("inventoryButton");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            Inventory inv = (Inventory)target;

            if (WriteTitle())
            {
                EditorGUI.BeginChangeCheck();

                // --- Base settings ---
                EditorGUILayout.PropertyField(limitedProperty);
                if (limitedProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(maxSlotsProperty);
                }

                EditorGUILayout.Space(5);

                EditorGUILayout.PropertyField(enableInputProperty);
                if (enableInputProperty.boolValue)
                {
                    EditorGUILayout.PropertyField(playerInputProperty);
                    EditorGUILayout.PropertyField(inventoryButtonProperty);
                }

                EditorGUILayout.Space(10);

                // --- Runtime contents ---
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Contents", EditorStyles.boldLabel);

                    // ensure instance exists
                    var instance = inv.instance;

                    bool hasAny = false;
                    EditorGUI.indentLevel++;
                    foreach (var tuple in inv)
                    {
                        hasAny = true;

                        var (slot, item, count) = tuple;
                        if (item == null || count <= 0)
                            continue;

                        string label = $"Slot {slot}: {item.name}";
                        if (count > 1)
                            label += $" x{count}";

                        EditorGUILayout.LabelField(label);
                    }
                    EditorGUI.indentLevel--;

                    if (!hasAny)
                    {
                        EditorGUILayout.HelpBox("Inventory is empty.", MessageType.Info);
                    }
                }
                else
                {
                    // Small hint in edit mode
                    EditorGUILayout.HelpBox(
                        "Enter Play Mode to see the inventory contents.",
                        MessageType.None
                    );
                }

                EditorGUI.EndChangeCheck();
            }

            serializedObject.ApplyModifiedProperties();
        }

        // --- Title bar styling, similar to ResourceHandlerEditor ---

        protected override GUIStyle GetTitleSyle()
        {
            return GUIUtils.GetBehaviourTitleStyle();
        }

        protected override string GetTitle()
        {
            return "Inventory";
        }

        protected override (Texture2D, Rect) GetIcon()
        {
            // If you have an "Inventory" icon registered in GUIUtils, this will use it.
            var tex = GUIUtils.GetTexture("Inventory");

            return (tex, new Rect(0f, 0f, 1f, 1f));
        }

        protected override (Color, Color, Color) GetColors()
        {
            // Gold / Yellow / Brown palette
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

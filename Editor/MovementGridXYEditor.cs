using UnityEditor;
using UnityEngine;

namespace UC
{
    [CustomEditor(typeof(MovementGridXY))]
    public class MovementGridXYEditor : UnityCommonEditor
    {
        SerializedProperty propSpeed;
        SerializedProperty propCooldown;
        SerializedProperty propUseRotation;
        SerializedProperty propTurnToDirection;
        SerializedProperty propMaxTurnSpeed;
        SerializedProperty propAxisToAlign;
        SerializedProperty propInputEnabled;
        SerializedProperty propPlayerInput;
        SerializedProperty propMovementInput;

        protected override void OnEnable()
        {
            base.OnEnable();

            propSpeed = serializedObject.FindProperty("speed");
            propCooldown = serializedObject.FindProperty("cooldown");            
            propUseRotation = serializedObject.FindProperty("useRotation");
            propTurnToDirection = serializedObject.FindProperty("turnToDirection");
            propMaxTurnSpeed = serializedObject.FindProperty("maxTurnSpeed");
            propAxisToAlign = serializedObject.FindProperty("axisToAlign");
            propInputEnabled = serializedObject.FindProperty("inputEnabled");
            propPlayerInput = serializedObject.FindProperty("playerInput");
            propMovementInput = serializedObject.FindProperty("movementInput");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (WriteTitle())
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(propSpeed, new GUIContent("Speed", "Maximum movement speed in world units (pixels)/second"));
                EditorGUILayout.PropertyField(propCooldown, new GUIContent("Cooldown", "Time between grid movements"));

                EditorGUILayout.PropertyField(propUseRotation, new GUIContent("Use Rotation?", "If true, the X and Y speed is relative to the rotation of the object.\nThis means that if the object is turned they refer to the right and up of the object, and not the absolute screen coordinates."));
                if (!propUseRotation.boolValue)
                {
                    EditorGUILayout.PropertyField(propTurnToDirection, new GUIContent("Turn To Movement Direction?", "If active, the object will turn towards the movement direction."));
                    if (propTurnToDirection.boolValue)
                    {
                        EditorGUILayout.PropertyField(propAxisToAlign, new GUIContent("Axis to align", "Is the object pointing right or up?"));
                        EditorGUILayout.PropertyField(propMaxTurnSpeed, new GUIContent("Max turn speed", "What's the maximum rotation speed (degrees/second)?"));
                    }
                }

                EditorGUILayout.PropertyField(propInputEnabled, new GUIContent("Use Input?", "Is the object controlled by the player?"));
                if (propInputEnabled.boolValue)
                {
                    MovementGridXY gridMovement = target as MovementGridXY;

                    if (gridMovement.needNewInputSystem)
                        EditorGUILayout.PropertyField(propPlayerInput, new GUIContent("Player Input", "Player Input to use, when needed"));

                    EditorGUILayout.PropertyField(propMovementInput, new GUIContent("Movement Input", "Dual axis movement for tile-based movement"));
                }

                EditorGUI.EndChangeCheck();

                serializedObject.ApplyModifiedProperties();
            }
        }

        protected override GUIStyle GetTitleSyle()
        {
            return GUIUtils.GetBehaviourTitleStyle();
        }

        protected override string GetTitle()
        {
            return "Grid Movement XY";
        }

        protected override (Texture2D, Rect) GetIcon()
        {
            var varTexture = GUIUtils.GetTexture("Movement");
            return (varTexture, new Rect(0.0f, 0.0f, 1.0f, 1.0f));
        }

        protected override (Color, Color, Color) GetColors()
        {
            return (GUIUtils.ColorFromHex("#D0FFFF"), GUIUtils.ColorFromHex("#2f4858"), GUIUtils.ColorFromHex("#86CBFF"));
        }
    }
}
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MovementPlatformer))]
public class MovementPlatformerEditor : UnityCommonEditor
{
    SerializedProperty propSpeed;
    SerializedProperty propHorizontalInputType;
    SerializedProperty propHorizontalAxis;
    SerializedProperty propHorizontalButtonPositive;
    SerializedProperty propHorizontalButtonNegative;
    SerializedProperty propHorizontalKeyPositive;
    SerializedProperty propHorizontalKeyNegative;
    SerializedProperty propGravityScale;
    SerializedProperty propUseTerminalVelocity;
    SerializedProperty propTerminalVelocity;
    SerializedProperty propCoyoteTime;
    SerializedProperty propJumpBehaviour;
    SerializedProperty propMaxJumpCount;
    SerializedProperty propJumpBufferingTime;
    SerializedProperty propJumpHoldMaxTime;
    SerializedProperty propJumpInputType;
    SerializedProperty propJumpAxis;
    SerializedProperty propJumpButton;
    SerializedProperty propJumpKey;
    SerializedProperty propEnableAirControl;
    SerializedProperty propAirCollider;
    SerializedProperty propGroundCollider;
    SerializedProperty propGlideBehaviour;
    SerializedProperty propGlideMaxTime;
    SerializedProperty propMaxGlideSpeed;
    SerializedProperty propGlideInputType;
    SerializedProperty propGlideAxis;
    SerializedProperty propGlideButton;
    SerializedProperty propGlideKey;
    SerializedProperty propGroundCheckCollider;
    SerializedProperty propGroundLayerMask;
    SerializedProperty propFlipBehaviour;
    SerializedProperty propUseAnimator;
    SerializedProperty propAnimator;
    SerializedProperty propHorizontalVelocityParameter;
    SerializedProperty propAbsoluteHorizontalVelocityParameter;
    SerializedProperty propVerticalVelocityParameter;
    SerializedProperty propAbsoluteVerticalVelocityParameter;
    SerializedProperty propIsGroundedParameter;
    SerializedProperty propIsGlidingParameter;

    protected override void OnEnable()
    {
        base.OnEnable();

        propSpeed = serializedObject.FindProperty("speed");
        propHorizontalInputType = serializedObject.FindProperty("horizontalInputType");
        propHorizontalAxis = serializedObject.FindProperty("horizontalAxis");
        propHorizontalButtonPositive = serializedObject.FindProperty("horizontalButtonPositive");
        propHorizontalButtonNegative = serializedObject.FindProperty("horizontalButtonNegative");
        propHorizontalKeyPositive = serializedObject.FindProperty("horizontalKeyPositive");
        propHorizontalKeyNegative = serializedObject.FindProperty("horizontalKeyNegative");
        propGravityScale = serializedObject.FindProperty("gravityScale");
        propUseTerminalVelocity = serializedObject.FindProperty("useTerminalVelocity");
        propTerminalVelocity = serializedObject.FindProperty("terminalVelocity");
        propCoyoteTime = serializedObject.FindProperty("coyoteTime");
        propJumpBehaviour = serializedObject.FindProperty("jumpBehaviour");
        propMaxJumpCount = serializedObject.FindProperty("maxJumpCount");
        propJumpBufferingTime = serializedObject.FindProperty("jumpBufferingTime");
        propJumpHoldMaxTime = serializedObject.FindProperty("jumpHoldMaxTime");
        propJumpInputType = serializedObject.FindProperty("jumpInputType");
        propJumpAxis = serializedObject.FindProperty("jumpAxis");
        propJumpButton = serializedObject.FindProperty("jumpButton");
        propJumpKey = serializedObject.FindProperty("jumpKey");
        propEnableAirControl = serializedObject.FindProperty("enableAirControl");
        propAirCollider = serializedObject.FindProperty("airCollider");
        propGroundCollider = serializedObject.FindProperty("groundCollider");
        propGlideBehaviour = serializedObject.FindProperty("glideBehaviour");
        propGlideMaxTime = serializedObject.FindProperty("glideMaxTime");
        propMaxGlideSpeed = serializedObject.FindProperty("maxGlideSpeed");
        propGlideInputType = serializedObject.FindProperty("glideInputType");
        propGlideAxis = serializedObject.FindProperty("glideAxis");
        propGlideButton = serializedObject.FindProperty("glideButton");
        propGlideKey = serializedObject.FindProperty("glideKey");
        propGroundCheckCollider = serializedObject.FindProperty("groundCheckCollider");
        propGroundLayerMask = serializedObject.FindProperty("groundLayerMask");
        propFlipBehaviour = serializedObject.FindProperty("flipBehaviour");
        propUseAnimator = serializedObject.FindProperty("useAnimator");
        propAnimator = serializedObject.FindProperty("animator");
        propHorizontalVelocityParameter = serializedObject.FindProperty("horizontalVelocityParameter");
        propAbsoluteHorizontalVelocityParameter = serializedObject.FindProperty("absoluteHorizontalVelocityParameter");
        propVerticalVelocityParameter = serializedObject.FindProperty("verticalVelocityParameter");
        propAbsoluteVerticalVelocityParameter = serializedObject.FindProperty("absoluteVerticalVelocityParameter");
        propIsGroundedParameter = serializedObject.FindProperty("isGroundedParameter");
        propIsGlidingParameter = serializedObject.FindProperty("isGlidingParameter");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (WriteTitle())
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(propSpeed, new GUIContent("Speed", "Maximum movement speed.\nX component is the maximum horizontal velocity\nY component is the jump velocity"));

            EditorGUILayout.PropertyField(propHorizontalInputType, new GUIContent("Horizontal Input Type", "Control type for horizontal movement.\nAxis: Use an axis for movement\nButton: Use a button for the movement\nKey: Use a key for the movement"));

            var inputType = (MovementPlatformer.InputType)propHorizontalInputType.intValue;

            switch (inputType)
            {
                case MovementPlatformer.InputType.Axis:
                    EditorGUILayout.PropertyField(propHorizontalAxis, new GUIContent("Horizontal Axis", "Horizontal axis name"));
                    break;
                case MovementPlatformer.InputType.Button:
                    EditorGUILayout.PropertyField(propHorizontalButtonPositive, new GUIContent("Horizontal Positive Button", "Positive button, press this to go right"));
                    EditorGUILayout.PropertyField(propHorizontalButtonNegative, new GUIContent("Horizontal Negative Button", "Negative button, press this to go left"));
                    break;
                case MovementPlatformer.InputType.Key:
                    EditorGUILayout.PropertyField(propHorizontalKeyPositive, new GUIContent("Horizontal Positive Key", "Positive key, press this to go right"));
                    EditorGUILayout.PropertyField(propHorizontalKeyNegative, new GUIContent("Horizontal Negative Key", "Negative key, press this to go left"));
                    break;
                default:
                    break;
            }

            EditorGUILayout.PropertyField(propGroundCheckCollider, new GUIContent("Ground Check Collider", "Link to a collider that identifies the ground.\nCircle or box collider below the player, when it touches ground, the character is grounded and can jump."));
            EditorGUILayout.PropertyField(propGroundLayerMask, new GUIContent("Ground Layer Mask", "In which layers are the objects that are considered ground?"));
            EditorGUILayout.PropertyField(propGravityScale, new GUIContent("Gravity Scale", "What's the gravity like? This is multiplied by the project's 2d gravity settings"));
            EditorGUILayout.PropertyField(propUseTerminalVelocity, new GUIContent("Use Terminal Velocity", "Does the object have a top speed while falling?"));
            if (propUseTerminalVelocity.boolValue)
            {
                EditorGUILayout.PropertyField(propTerminalVelocity, new GUIContent("Terminal Velocity", "What's the top speed while falling, in world units (pixels)/second?"));
            }
            EditorGUILayout.PropertyField(propCoyoteTime, new GUIContent("Coyote Time", "How long does it take until the character start falling when not grounded?"));
            EditorGUILayout.PropertyField(propEnableAirControl, new GUIContent("Air Control", "Can the player control the character while in the air?"));
            EditorGUILayout.PropertyField(propAirCollider, new GUIContent("Air Collider", "What is the object's collider while in the air (not grounded)?"));
            EditorGUILayout.PropertyField(propGroundCollider, new GUIContent("Ground Collider", "What's the object's collider while on the ground?"));

            EditorGUILayout.PropertyField(propJumpBehaviour, new GUIContent("Jump Type", "Type of jump.\nNone: Player can't jump\nFixed: Player always jumps the same height\nVariable: The longer the player holds the jump button, the higher he jumps"));
            if (propJumpBehaviour.intValue != (int)MovementPlatformer.JumpBehaviour.None)
            {
                EditorGUILayout.PropertyField(propJumpInputType, new GUIContent("Jump Input Type", "What's the input to jump?\nAxis: Use an axis for movement\nButton: Use a button for the movement\nKey: Use a key for the movement"));

                var jumpInputType = (MovementPlatformer.InputType)propJumpInputType.intValue;

                switch (jumpInputType)
                {
                    case MovementPlatformer.InputType.Axis:
                        EditorGUILayout.PropertyField(propJumpAxis, new GUIContent("Jump Axis", "Axis to jump.\nAs soon as this axis' value is larger than zero, character will jump."));
                        break;
                    case MovementPlatformer.InputType.Button:
                        EditorGUILayout.PropertyField(propJumpButton, new GUIContent("Jump Button", "Jump button."));
                        break;
                    case MovementPlatformer.InputType.Key:
                        EditorGUILayout.PropertyField(propJumpKey, new GUIContent("Jump Key", "Jump key"));
                        break;
                    default:
                        break;
                }

                EditorGUILayout.PropertyField(propMaxJumpCount, new GUIContent("Max Jump Count", "How many jumps can the player do before having to touch the ground?\nFor example, for double jump, use 2.\nIf zero, no jumping allowed."));
                EditorGUILayout.PropertyField(propJumpBufferingTime, new GUIContent("Jump Buffering Time", "If the player presses the jump key before being on the ground, but hits the ground in less than this time, he will jump automatically.\nThis reduces player frustration and provides tighter controls."));
                if (propJumpBehaviour.intValue == (int)MovementPlatformer.JumpBehaviour.Variable)
                {
                    EditorGUILayout.PropertyField(propJumpHoldMaxTime, new GUIContent("Jump Max. Hold Time", "For how long can the player press jump and still be considered a single jump."));
                }
            }
            EditorGUILayout.PropertyField(propGlideBehaviour, new GUIContent("Glide Behaviour", "Glide behaviour.\nNone: No gliding allowed.\nEnabled: Player can glide at will\nTimer: Player can glide for a certain amount of time.\nGliding is basically falling slower while pressing the glide input."));
            if (propGlideBehaviour.intValue != (int)MovementPlatformer.GlideBehaviour.None)
            {
                EditorGUILayout.PropertyField(propGlideInputType, new GUIContent("Glide Input Type", "What's the input to glide?\nAxis: Use an axis for movement\nButton: Use a button for the movement\nKey: Use a key for the movement"));

                var glideInputType = (MovementPlatformer.InputType)propGlideInputType.intValue;

                switch (glideInputType)
                {
                    case MovementPlatformer.InputType.Axis:
                        EditorGUILayout.PropertyField(propGlideAxis, new GUIContent("Glide Axis", "What's the glide axis?\nAs soon as this axis any amount of positive, it's considered triggered."));
                        break;
                    case MovementPlatformer.InputType.Button:
                        EditorGUILayout.PropertyField(propGlideButton, new GUIContent("Glide Button", "Glide button"));
                        break;
                    case MovementPlatformer.InputType.Key:
                        EditorGUILayout.PropertyField(propGlideKey, new GUIContent("Glide Key", "Glide key"));
                        break;
                    default:
                        break;
                }

                if (propGlideBehaviour.intValue == (int)MovementPlatformer.GlideBehaviour.Timer)
                {
                    EditorGUILayout.PropertyField(propGlideMaxTime, new GUIContent("Glide Max. Time", "What's the maximum amount of glide time?"));
                }

                EditorGUILayout.PropertyField(propMaxGlideSpeed, new GUIContent("Max Glide Speed", "This is the maximum fall speed while gliding, in world units (pixels)/second."));
            }

            // Separator
            Rect separatorRect = GUILayoutUtility.GetLastRect();
            separatorRect.yMin = separatorRect.yMax + 5;
            separatorRect.height = 5.0f;
            EditorGUI.DrawRect(separatorRect, GUIUtils.ColorFromHex("#ff6060"));
            EditorGUILayout.Space(separatorRect.height + 5);

            EditorGUILayout.PropertyField(propFlipBehaviour, new GUIContent("Flip Behaviour", "What to do visually when we turn?\nVelocity Flips Sprite: When horizontal velocity is negative (moving left), the sprite renderer is flipped horizontal\nVelocity Inverts Scale: When horizontal velocity is negative (moving left), the horizontal scale is negated\nInput Flips Sprite: When players presses left, the sprite renderer is flipped horizontal\nInput Inverts Scale: When player presses left, the horizontal scale is negated\nVelocity Rotate Sprite: When horizontal velocity is negative (moving left), the object is rotated 180 degrees around the Y axis.\nInput Rotate Sprite: When the player presses left, the object is rotated 180 degrees around the Y axis.\nScaling doesn't affect the internal object's direction, while rotating does."));
            EditorGUILayout.PropertyField(propUseAnimator, new GUIContent("Use Animator", "Should we drive an animator with this movement controller?"));
            if (propUseAnimator.boolValue)
            {
                EditorGUILayout.PropertyField(propAnimator, new GUIContent("Animator", "What animator to use?"));
                EditorGUILayout.PropertyField(propHorizontalVelocityParameter, new GUIContent("Horizontal Velocity Parameter", "What is the parameter to set to the horizontal velocity?"));
                EditorGUILayout.PropertyField(propAbsoluteHorizontalVelocityParameter, new GUIContent("Absolute Horizontal Velocity Parameter", "What is the parameter to set to the absolute horizontal velocity?"));
                EditorGUILayout.PropertyField(propVerticalVelocityParameter, new GUIContent("Vertical Velocity Parameter", "What is the parameter to set to the vertical velocity?"));
                EditorGUILayout.PropertyField(propAbsoluteVerticalVelocityParameter, new GUIContent("Absolute Vertical Velocity Parameter", "What is the parameter to set to the absolute horizontal velocity?"));
                EditorGUILayout.PropertyField(propIsGroundedParameter, new GUIContent("Is Grounded Parameter", "What is the parameter to set to true/false when the player is grounded?"));
                EditorGUILayout.PropertyField(propIsGlidingParameter, new GUIContent("Is Glidding Parameter", "What is the parameter to set to true/false when the player is gliding?"));
            }

            EditorGUI.EndChangeCheck();

            serializedObject.ApplyModifiedProperties();

            //base.OnInspectorGUI();
        }
    }

    protected override GUIStyle GetTitleSyle()
    {
        return GUIUtils.GetBehaviourTitleStyle();
    }

    protected override string GetTitle()
    {
        return "Platformer Controller";
    }

    protected override Texture2D GetIcon()
    {
        var varTexture = GUIUtils.GetTexture("Movement");
        return varTexture;
    }

    protected override (Color, Color, Color) GetColors()
    {
        return (GUIUtils.ColorFromHex("#D0FFFF"), GUIUtils.ColorFromHex("#2f4858"), GUIUtils.ColorFromHex("#86CBFF"));
    }
}

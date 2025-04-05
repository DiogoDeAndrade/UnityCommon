using NaughtyAttributes;
using UnityEditor;
using UnityEngine;

namespace UC
{

    [CustomEditor(typeof(MovementPlatformer))]
    public class MovementPlatformerEditor : UnityCommonEditor
    {
        SerializedProperty propSpeed;
        SerializedProperty propPlayerInput;
        SerializedProperty propHorizontalInput;
        SerializedProperty propGravityScale;
        SerializedProperty propUseTerminalVelocity;
        SerializedProperty propTerminalVelocity;
        SerializedProperty propCoyoteTime;
        SerializedProperty propJumpBehaviour;
        SerializedProperty propMaxJumpCount;
        SerializedProperty propJumpBufferingTime;
        SerializedProperty propJumpHoldMaxTime;
        SerializedProperty propJumpInput;
        SerializedProperty propEnableAirControl;
        SerializedProperty propAirCollider;
        SerializedProperty propGroundCollider;
        SerializedProperty propGlideBehaviour;
        SerializedProperty propGlideMaxTime;
        SerializedProperty propMaxGlideSpeed;
        SerializedProperty propGlideInput;
        SerializedProperty propGroundCheckCollider;
        SerializedProperty propGroundLayerMask;
        SerializedProperty propClimbBehaviour;
        SerializedProperty propClimbCheckCollider;
        SerializedProperty propClimbSpeed;
        SerializedProperty propClimbCooldown;
        SerializedProperty propClimbMask;
        SerializedProperty propClimbInput;
        SerializedProperty propFlipBehaviour;
        SerializedProperty propUseAnimator;
        SerializedProperty propAnimator;
        SerializedProperty propHorizontalVelocityParameter;
        SerializedProperty propAbsoluteHorizontalVelocityParameter;
        SerializedProperty propVerticalVelocityParameter;
        SerializedProperty propAbsoluteVerticalVelocityParameter;
        SerializedProperty propIsGroundedParameter;
        SerializedProperty propIsGlidingParameter;
        SerializedProperty propIsClimbingParameter;
        SerializedProperty propCanJumpFromClimb;

        protected override void OnEnable()
        {
            base.OnEnable();

            propSpeed = serializedObject.FindProperty("speed");
            propPlayerInput = serializedObject.FindProperty("playerInput");
            propHorizontalInput = serializedObject.FindProperty("horizontalInput");
            propGravityScale = serializedObject.FindProperty("gravityScale");
            propUseTerminalVelocity = serializedObject.FindProperty("useTerminalVelocity");
            propTerminalVelocity = serializedObject.FindProperty("terminalVelocity");
            propCoyoteTime = serializedObject.FindProperty("coyoteTime");
            propJumpBehaviour = serializedObject.FindProperty("jumpBehaviour");
            propMaxJumpCount = serializedObject.FindProperty("maxJumpCount");
            propJumpBufferingTime = serializedObject.FindProperty("jumpBufferingTime");
            propJumpHoldMaxTime = serializedObject.FindProperty("jumpHoldMaxTime");
            propJumpInput = serializedObject.FindProperty("jumpInput");
            propEnableAirControl = serializedObject.FindProperty("enableAirControl");
            propAirCollider = serializedObject.FindProperty("airCollider");
            propGroundCollider = serializedObject.FindProperty("groundCollider");
            propGlideBehaviour = serializedObject.FindProperty("glideBehaviour");
            propGlideMaxTime = serializedObject.FindProperty("glideMaxTime");
            propMaxGlideSpeed = serializedObject.FindProperty("maxGlideSpeed");
            propGlideInput = serializedObject.FindProperty("glideInput");
            propGroundCheckCollider = serializedObject.FindProperty("groundCheckCollider");
            propGroundLayerMask = serializedObject.FindProperty("groundLayerMask");
            propClimbBehaviour = serializedObject.FindProperty("climbBehaviour");
            propClimbCheckCollider = serializedObject.FindProperty("climbCheckCollider");
            propClimbSpeed = serializedObject.FindProperty("climbSpeed");
            propClimbCooldown = serializedObject.FindProperty("climbCooldown");
            propClimbMask = serializedObject.FindProperty("climbMask");
            propClimbInput = serializedObject.FindProperty("climbInput");
            propCanJumpFromClimb = serializedObject.FindProperty("canJumpFromClimb");
            propFlipBehaviour = serializedObject.FindProperty("flipBehaviour");
            propUseAnimator = serializedObject.FindProperty("useAnimator");
            propAnimator = serializedObject.FindProperty("animator");
            propHorizontalVelocityParameter = serializedObject.FindProperty("horizontalVelocityParameter");
            propAbsoluteHorizontalVelocityParameter = serializedObject.FindProperty("absoluteHorizontalVelocityParameter");
            propVerticalVelocityParameter = serializedObject.FindProperty("verticalVelocityParameter");
            propAbsoluteVerticalVelocityParameter = serializedObject.FindProperty("absoluteVerticalVelocityParameter");
            propIsGroundedParameter = serializedObject.FindProperty("isGroundedParameter");
            propIsGlidingParameter = serializedObject.FindProperty("isGlidingParameter");
            propIsClimbingParameter = serializedObject.FindProperty("isClimbingParameter");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (WriteTitle())
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(propSpeed, new GUIContent("Speed", "Maximum movement speed.\nX component is the maximum horizontal velocity\nY component is the jump velocity"));

                MovementPlatformer movementPlatformer = target as MovementPlatformer;

                if (movementPlatformer.needNewInputSystem)
                {
                    EditorGUILayout.PropertyField(propPlayerInput, new GUIContent("Player Input", "Player input component.\nNeeded if we're using the new input system."));
                }

                EditorGUILayout.PropertyField(propHorizontalInput, new GUIContent("Horizontal Input Type", "Control type for horizontal movement.\nAxis: Use an axis for movement\nButton: Use a button for the movement\nKey: Use a key for the movement"));

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
                    EditorGUILayout.PropertyField(propJumpInput, new GUIContent("Jump Input Type", "What's the input to jump?\nAxis: Use an axis for movement\nButton: Use a button for the movement\nKey: Use a key for the movement"));

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
                    EditorGUILayout.PropertyField(propGlideInput, new GUIContent("Glide Input Type", "What's the input to glide?\nAxis: Use an axis for movement\nButton: Use a button for the movement\nKey: Use a key for the movement"));

                    if (propGlideBehaviour.intValue == (int)MovementPlatformer.GlideBehaviour.Timer)
                    {
                        EditorGUILayout.PropertyField(propGlideMaxTime, new GUIContent("Glide Max. Time", "What's the maximum amount of glide time?"));
                    }

                    EditorGUILayout.PropertyField(propMaxGlideSpeed, new GUIContent("Max Glide Speed", "This is the maximum fall speed while gliding, in world units (pixels)/second."));
                }

                EditorGUILayout.PropertyField(propClimbBehaviour, new GUIContent("Climb Behaviour", "Climb behaviour.\nNone: No climbing allowed.\nEnabled: Player can climb"));
                if (propClimbBehaviour.intValue != (int)MovementPlatformer.ClimbBehaviour.None)
                {
                    EditorGUILayout.PropertyField(propClimbSpeed, new GUIContent("Climb Speed", "Up/Down speed while climbing"));
                    EditorGUILayout.PropertyField(propClimbCooldown, new GUIContent("Climb Cooldown", "How much time must pass before we can climb again?\nThis mostly avoids the stutter on the top when we just keep pressing up on top of stairs."));
                    EditorGUILayout.PropertyField(propClimbCheckCollider, new GUIContent("Climb Check Collider", "Collider to use for climb tests"));
                    EditorGUILayout.PropertyField(propClimbMask, new GUIContent("Climbable Layer Mask", "Ladders or ropes must be on this layer"));
                    EditorGUILayout.PropertyField(propCanJumpFromClimb, new GUIContent("Can Jump From Ladder", "Can the player jump from ladder. If enable, jump is also recharged on ladders"));

                    EditorGUILayout.PropertyField(propClimbInput, new GUIContent("Climb Input Type", "What's the input to climb?\nAxis: Use an axis for movement\nButton: Use a button for the movement\nKey: Use a key for the movement"));
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
                    EditorGUILayout.PropertyField(propIsClimbingParameter, new GUIContent("Is Climbing Parameter", "What is the parameter to set to true/false when the player is climbing?"));
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
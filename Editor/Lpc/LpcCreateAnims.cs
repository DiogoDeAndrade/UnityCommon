#if UNITY_2D_SPRITE_AVAILABLE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;


// Must enable this if we want it - can't always have because the Sprite2d package might not be present and this
// code won't compile without it
namespace UC
{
    public class LpcCreateAnims
    {
        struct AnimKey
        {
            public LpcSpriteProcessor.LpcAnimationState state;
            public char                                 direction;
        };

        public class AnimationClipOverrides : List<KeyValuePair<AnimationClip, AnimationClip>>
        {
            public AnimationClipOverrides(int capacity) : base(capacity) { }

            public AnimationClip this[string name]
            {
                get
                {
                    return this.Find(x => x.Key.name.Equals(name)).Value;
                }
                set
                {
                    int index = this.FindIndex(x => x.Key.name.Equals(name));
                    if (index != -1)
                    {
                        this[index] = new KeyValuePair<AnimationClip, AnimationClip>(this[index].Key, value);
                    }
                }
            }
        }

        [MenuItem("Assets/Create/LPC/Create Animations")]
        private static void LPCCreateAnimations()
        {
            // Get all sprites
            Texture2D texture = Selection.activeObject as Texture2D;

            string assetPath = AssetDatabase.GetAssetPath(texture);

            var      assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var      sprites = new List<Sprite>();

            foreach (var asset in assetsAtPath)
            {
                if (asset.GetType() == typeof(Sprite))
                {
                    sprites.Add(asset as Sprite);
                }
            }

            if (sprites.Count > 0)
            {
                Dictionary<AnimKey, List<Sprite>> anims = new Dictionary<AnimKey, List<Sprite>>();

                for (int i = 0; i < sprites.Count; i++)
                {
                    Rect rect = sprites[i].rect;
                    int row = (int)(rect.y / 64);
                    var animState = LpcSpriteProcessor.GetAnimationState(row);

                    AnimKey ak = new AnimKey();
                    ak.state = animState;
                    if (sprites[i].name.IndexOf("N_") != -1) ak.direction = 'N';
                    else if (sprites[i].name.IndexOf("E_") != -1) ak.direction = 'E';
                    else if (sprites[i].name.IndexOf("S_") != -1) ak.direction = 'S';
                    else if (sprites[i].name.IndexOf("W_") != -1) ak.direction = 'W';

                    if (!anims.ContainsKey(ak)) anims.Add(ak, new List<Sprite>());

                    if (anims[ak] == null) anims[ak] = new List<Sprite>();
                    anims[ak].Add(sprites[i]);
                }

                int     framesPerSecond = 12;
                string  filename = "";
                string  path = System.IO.Path.GetDirectoryName(assetPath);

                // Create animations
                foreach (var anim in anims)
                {
                    bool    loop = false;

                    if (anim.Key.state == LpcSpriteProcessor.LpcAnimationState.Walk)
                    {
                        loop = true;

                        // Build "Idle" animation from this as well
                        var idleClip = CreateAnimationClip("Idle" + anim.Key.direction, framesPerSecond, loop, new List<Sprite>(anim.Value.Take(1)));

                        filename = path + "/" + idleClip.name + ".anim";
                        AssetUtils.CreateOrReplaceAsset<AnimationClip>(idleClip, filename);

                        var clip = CreateAnimationClip(anim.Key.state.ToString() + anim.Key.direction, framesPerSecond, loop, new List<Sprite>(anim.Value.Skip(1).Take(8)));

                        filename = path + "/" + clip.name + ".anim";
                        AssetUtils.CreateOrReplaceAsset<AnimationClip>(clip, filename);
                    }
                    else if (anim.Key.state == LpcSpriteProcessor.LpcAnimationState.Shoot)
                    {
                        // Split frames in two
                        var shootBegin = CreateAnimationClip("ShootBegin" + anim.Key.direction, framesPerSecond, false, new List<Sprite>(anim.Value.Take(5)));

                        filename = path + "/" + shootBegin.name + ".anim";
                        AssetUtils.CreateOrReplaceAsset<AnimationClip>(shootBegin, filename);

                        var clip = CreateAnimationClip("Shoot" + anim.Key.direction, framesPerSecond, true, new List<Sprite>(anim.Value.Skip(5).Take(7)));

                        filename = path + "/" + clip.name + ".anim";
                        AssetUtils.CreateOrReplaceAsset<AnimationClip>(clip, filename);
                    }
                    else
                    {
                        var clip = CreateAnimationClip(anim.Key.state.ToString() + anim.Key.direction, framesPerSecond, loop, anim.Value);

                        filename = path + "/" + clip.name + ".anim";
                        AssetUtils.CreateOrReplaceAsset<AnimationClip>(clip, filename);
                    }
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var runtimeController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animation/Base/BaseAnimations.controller");
                AnimatorOverrideController controller = new AnimatorOverrideController(runtimeController);

                var overrides = new AnimationClipOverrides(controller.overridesCount);
                controller.GetOverrides(overrides);

                List<char> dirs = new List<char> { 'N', 'E', 'S', 'W' };

                foreach (var c in dirs)
                {
                    AnimationClip anim;

                    anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "/Hurt" + c + ".anim");
                    if (anim != null) overrides["Die" + c] = anim;
                    anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "/Idle" + c + ".anim");
                    if (anim != null) overrides["Idle" + c] = anim;
                    anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "/ShootBegin" + c + ".anim");
                    if (anim != null) overrides["ShootBegin" + c] = anim;
                    anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "/Shoot" + c + ".anim");
                    if (anim != null) overrides["Shoot" + c] = anim;
                    anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "/Slash" + c + ".anim");
                    if (anim != null) overrides["Slash" + c] = anim;
                    anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "/Thrust" + c + ".anim");
                    if (anim != null) overrides["Thrust" + c] = anim;
                    anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(path + "/Walk" + c + ".anim");
                    if (anim != null) overrides["Walk" + c] = anim;
                }

                controller.ApplyOverrides(overrides);

                filename = path + "/" + System.IO.Path.GetFileNameWithoutExtension(assetPath) + ".controller";
                AssetUtils.CreateOrReplaceAsset<AnimatorOverrideController>(controller, filename);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        static AnimationClip CreateAnimationClip(string name, int framesPerSecond, bool loop, List<Sprite> sprites)
        {
            float timePerFrame = 1.0f / framesPerSecond;

            AnimationClip clip = new AnimationClip();
            clip.name = name;
            clip.frameRate = framesPerSecond;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            EditorCurveBinding spriteBinding = new EditorCurveBinding();
            spriteBinding.type = typeof(SpriteRenderer);
            spriteBinding.path = "";
            spriteBinding.propertyName = "m_Sprite";

            ObjectReferenceKeyframe[] spriteKeyFrames = new ObjectReferenceKeyframe[sprites.Count];
            for (int i = 0; i < sprites.Count; i++)
            {
                spriteKeyFrames[i] = new ObjectReferenceKeyframe();
                spriteKeyFrames[i].time = i * timePerFrame;
                spriteKeyFrames[i].value = sprites[i];
            }

            AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, spriteKeyFrames);

            return clip;
        }

        [MenuItem("Assets/Create/LPC/Create Animations", validate = true)]
        private static bool NewMenuOptionValidation()
        {
            var selectedObject = Selection.activeObject;
            if (selectedObject == null) return false;
            if (selectedObject.GetType() != typeof(Texture2D))
            {
                Debug.LogWarning("Can only create LPC animations from texture!");
                return false;
            }

            Texture2D texture = selectedObject as Texture2D;

            if (texture.name.Length < 4) return false;

            if (texture.name.Substring(0,4) != "LPC_")
            {
                Debug.LogWarning("Texture name needs to start with LPC_!");
                return false;
            }

            return true;
        }
    }
}
#endif

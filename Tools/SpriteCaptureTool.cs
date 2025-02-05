// Must enable this if we want it - can't always have because the Sprite2d package might not be present and this
// code won't compile without it
#if UNITY_2D_SPRITE_AVAILABLE
using NaughtyAttributes;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D.Sprites;
#endif

using UnityEngine;
using UnityEngine.Rendering.Universal;
using File = System.IO.File;

// To use:
// 1. place this on a camera that encompasses what we want to shoot
//   1.1. Confirm camera has background color set with full alpha
//   1.2. If the camera uses post-process, there are two captures needed - one to capture the alpha, the other to capture the color - this only works well with URP at the moment
// 2. setup options
// 3. run application

public class SpriteCaptureTool : MonoBehaviour
{
    public enum CaptureMode { Single, Frames, Time, Manual }
    public enum OutputMode { Single, Spritesheet };
    public enum RecolorMode { None, Bake, Realtime };
    public enum DitherMode { None, Bayer };

    [SerializeField] 
    private CaptureMode     captureMode = CaptureMode.Single;
    [SerializeField, ShowIf("needOutputMode")] 
    private OutputMode      outputMode = OutputMode.Single;
    [SerializeField]
    private bool            sRGB = true;
    [SerializeField] 
    private int             spriteHeight = 128;
    [SerializeField, ShowIf("hasRT")] 
    private bool            useExistingRT;
    [SerializeField] 
    private string          targetFilename = "SpriteSheet.png";
    [SerializeField] 
    private bool            stopOnEnd;
    [SerializeField, ShowIf("needFullAnimation")] 
    private bool            extractFullAnimation;
    [SerializeField, ShowIf("needFrameCount")]
    private int             frameCount = 10;
    [SerializeField, ShowIf("captureMode", CaptureMode.Frames)]
    private int             skipFrames = 0;
    [SerializeField, ShowIf("needDuration")]
    private float           duration = 2.0f;
    [SerializeField, ShowIf("captureMode", CaptureMode.Time)]
    private float           timePerFrame = 0.25f;
    [SerializeField, ShowIf("needAnimator")]
    private Animator        animator;
    [SerializeField]
    private RecolorMode     recolorMode;
    [SerializeField, ShowIf("hasRecolor")]
    private DitherMode      ditherMode; 
    [SerializeField, ShowIf("hasRecolor")]
    private ColorPalette    colorPalette;

    private Camera          captureCamera;
    private RenderTexture   renderTexture;
    private RenderTexture   createdRenderTexture;
    private string          filename;
    private string          extension;
    private int             frameIndex = 0;

    struct SaveData
    {
        public Texture2D    texture;
        public int          index;
        public float        time;
    }
    List<SaveData>          frames;

    private int spriteWidth => Mathf.FloorToInt(spriteHeight * captureCamera.aspect);
    private bool hasRT
    {
        get
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) return false;
            return (cam.targetTexture != null);
        }
    }
    private bool needOutputMode => (captureMode == CaptureMode.Frames) || (captureMode == CaptureMode.Time);
    private bool isManual => (captureMode == CaptureMode.Manual);
    private bool needFullAnimation => (captureMode == CaptureMode.Frames) || (captureMode == CaptureMode.Time);
    private bool needFrameCount => (captureMode == CaptureMode.Frames) && (!extractFullAnimation);
    private bool needDuration => (captureMode == CaptureMode.Time) && (!extractFullAnimation);
    private bool needAnimator => (needFullAnimation) && (extractFullAnimation);
    private bool hasRecolor => (recolorMode != RecolorMode.None);

#if UNITY_EDITOR
    private void Start()
    {
        captureCamera = GetComponent<Camera>();
        captureCamera.enabled = false;

        filename = Path.Combine(Path.GetDirectoryName(targetFilename), Path.GetFileNameWithoutExtension(targetFilename));
        extension = Path.GetExtension(targetFilename);

        if ((useExistingRT) && (captureCamera.targetTexture != null))
        {
            renderTexture = captureCamera.targetTexture;
        }
        else
        {
            // Create the RenderTexture
            createdRenderTexture = renderTexture = new RenderTexture(spriteWidth, spriteHeight, 24, RenderTextureFormat.ARGB32, (sRGB) ? (RenderTextureReadWrite.sRGB) : (RenderTextureReadWrite.Linear));
            renderTexture.useMipMap = false;
            renderTexture.filterMode = FilterMode.Point;

            // Assign the RenderTexture to the camera
            captureCamera.targetTexture = renderTexture;

            useExistingRT = false;
        }

        StartCoroutine(CaptureCR());
    }

    static float Frac(float f)
    {
        return f - Mathf.Floor(f);
    }

    IEnumerator CaptureCR()
    {
        var texture = RunCapture();

        if (captureMode == CaptureMode.Single)
        {
            // Save the texture to a file
            Store(texture, -1);

            if (createdRenderTexture != null)
            {
                captureCamera.targetTexture = null;
                yield return null;
                FinishCapture();
            }
        }
        else if (captureMode == CaptureMode.Frames)
        {
            frameIndex = 0;
            Store(texture, frameIndex);
            frameIndex++;

            int fTotal = frameCount - 1;
            int fSkip = skipFrames;

            if ((extractFullAnimation) && (animator))
            {
                fTotal = int.MaxValue;
            }

            var animState = animator.GetCurrentAnimatorStateInfo(0);

            while (fTotal > 0)
            {
                yield return null;

                var nextState = animator.GetCurrentAnimatorStateInfo(0);
                if (animState.fullPathHash != nextState.fullPathHash)
                {
                    // Changed animation, exit
                    break;
                }
                if (Frac(animState.normalizedTime) > Frac(nextState.normalizedTime))
                {
                    // Looped animation, exit
                    break;
                }
                animState = nextState;

                fTotal--;
                if (fSkip == 0)
                {
                    texture = RunCapture();
                    Store(texture, frameIndex);
                    frameIndex++;
                    fSkip = skipFrames;
                }
                else fSkip--;
            }

            FinishCapture();
        }
        else if (captureMode == CaptureMode.Time)
        {
            frameIndex = 0;
            Store(texture, frameIndex, 0.0f);
            frameIndex++;

            float elapsedTime = 0.0f;
            float cooldown = timePerFrame;
            float maxDuration = duration;
            if ((extractFullAnimation) && (animator))
            {
                maxDuration = float.MaxValue;
            }

            var animState = animator.GetCurrentAnimatorStateInfo(0);

            while (elapsedTime <= maxDuration)
            {
                yield return null;

                var nextState = animator.GetCurrentAnimatorStateInfo(0);
                if (animState.fullPathHash != nextState.fullPathHash)
                {
                    // Changed animation, exit
                    break;
                }
                if (Frac(animState.normalizedTime) > Frac(nextState.normalizedTime))
                {
                    // Looped animation, exit
                    break;
                }
                animState = nextState;

                elapsedTime += Time.deltaTime;
                cooldown -= Time.deltaTime;
                if (cooldown <= 0.0f)
                { 
                    texture = RunCapture();
                    Store(texture, frameIndex, elapsedTime);
                    frameIndex++;
                    cooldown = timePerFrame;
                }
            }

            FinishCapture();
        }
        else if (captureMode == CaptureMode.Manual)
        {
            Store(texture, frameIndex);
            frameIndex++;
        }
    }

    [Button("Take Snapshot"), ShowIf("isManual")]
    void TakeSnapshot()
    {
        var texture = RunCapture();

        if (outputMode == OutputMode.Single) Store(texture, frameIndex);
        frameIndex++;
    }

    void FinishCapture()
    {
        captureCamera.enabled = true;

        // Clean up the RenderTexture when done
        if (createdRenderTexture)
        {
            captureCamera.targetTexture = null;
            createdRenderTexture.Release();
            Destroy(createdRenderTexture);
        }

        if (stopOnEnd)
        {
            EditorApplication.isPlaying = false;
        }

        StoreData();
        frames = null;
    }

    void StoreData()
    {
        if (frames == null) return;

        if (outputMode == OutputMode.Single)
        {
            foreach (var frame in frames)
            {
                Save(frame.texture, frame.index, frame.time, false);
            }
        }
        else if (outputMode == OutputMode.Spritesheet)
        {
            int nElemsHorizontal = Mathf.CeilToInt(Mathf.Sqrt(frames.Count));
            int nElemsVertical = Mathf.CeilToInt(frames.Count / (float)nElemsHorizontal);

            int atlasWidth = nElemsHorizontal * spriteWidth;
            int atlasHeight = nElemsVertical * spriteHeight;

            // Read the RenderTexture into a Texture2D
            var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);

            // Get background color of camera
            Color backColor = captureCamera.backgroundColor;
            Color[] backgroundPixels = Enumerable.Repeat(backColor, atlasWidth * atlasHeight).ToArray();
            atlas.SetPixels(backgroundPixels);

            // Copy each frame texture into the atlas
            for (int i = 0; i < frames.Count; i++)
            {
                int x = (i % nElemsHorizontal) * spriteWidth; // X position in the atlas
                int y = (atlasHeight - spriteHeight) - (i / nElemsVertical) * spriteHeight; // Flip Y position in the atlas

                var frameTexture = frames[i].texture;

                // Get the frame's pixels
                var pixels = frameTexture.GetPixels();

                // Set the pixels in the atlas
                atlas.SetPixels(x, y, spriteWidth, spriteHeight, pixels);
            }
            
            atlas.Apply();

            Save(atlas, -1, float.MaxValue, true);
        }
    }

    Texture2D RunCapture()
    {
        Texture2D alphaTexture = null;
        var currentRT = RenderTexture.active;

        var additionalCameraData = captureCamera.GetComponent<UniversalAdditionalCameraData>();
        if ((additionalCameraData != null) && (additionalCameraData.renderPostProcessing))
        {
            additionalCameraData.renderPostProcessing = false;

            captureCamera.Render();

            // Read the RenderTexture into a Texture2D
            alphaTexture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            alphaTexture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            alphaTexture.Apply();

            additionalCameraData.renderPostProcessing = true;
        }

        captureCamera.Render();

        // Read the RenderTexture into a Texture2D
        Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        RenderTexture.active = renderTexture;
        texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        if (alphaTexture)
        {
            var alphaBitmap = alphaTexture.GetPixels();
            var colorBitmap = texture.GetPixels();
            for (int i = 0; i < colorBitmap.Length; i++)
            {
                colorBitmap[i] = colorBitmap[i].ChangeAlpha(alphaBitmap[i].a);
            }
            texture.SetPixels(colorBitmap);
        }
        texture.Apply();

        RenderTexture.active = currentRT;

        return texture;
    }
    private void OnDestroy()
    {
        FinishCapture();
    }

    void Store(Texture2D texture, int index = -1, float time = float.MaxValue)
    {
        if (frames == null) frames = new();

        frames.Add(new SaveData()
        {
            texture = texture,
            index = index,
            time = time
        });
    }

    void Save(Texture2D texture, int index, float time, bool isAtlas)
    { 
        if (recolorMode == RecolorMode.Bake)
        {
            if (ditherMode == DitherMode.None)
            {
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    c = colorPalette.GetClosestColorRGB(c);
                    pixels[i] = c.ChangeAlpha(pixels[i].a);
                }
                texture.SetPixels(pixels);
            }
            else if (ditherMode == DitherMode.Bayer)
            {
                var pixels = texture.GetPixels();
                int i = 0;
                for (int y = 0; y < texture.height; y++)
                {
                    for (int x = 0; x < texture.width; x++)
                    {
                        Color c = pixels[i];
                        c = colorPalette.GetClosestColorRGB_Bayer(c, x, y);
                        pixels[i] = c.ChangeAlpha(pixels[i].a);
                        
                        i++;
                    }
                }
                texture.SetPixels(pixels);
            }
        }
        else if (recolorMode == RecolorMode.Realtime)
        {
            if (ditherMode == DitherMode.None)
            {
                var pixels = texture.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    int closestColorID = colorPalette.GetIndexClosestColorRGB(c);
                    float encodedID = (closestColorID + 0.5f) / (float)colorPalette.Count;
                    pixels[i] = new Color(encodedID, 0.0f, 0.0f, c.a);
                }
                texture.SetPixels(pixels);
            }
            else if (ditherMode == DitherMode.Bayer)
            {
                var pixels = texture.GetPixels();
                int i = 0;
                for (int y = 0; y < texture.height; y++)
                {
                    for (int x = 0; x < texture.width; x++)
                    {
                        Color c = pixels[i];
                        int closestColorID = colorPalette.GetIndexClosestColorRGB_Bayer(c, x, y);
                        float encodedID = (closestColorID + 0.5f) / (float)colorPalette.Count;
                        pixels[i] = new Color(encodedID, 0.0f, 0.0f, c.a);

                        i++;
                    }
                }
                texture.SetPixels(pixels);
            }
        }

        byte[] bytes = texture.EncodeToPNG();

        string path = "";
        if (index == -1)
        {
            path = $"{Path.Combine(Application.dataPath, filename)}{extension}";
        }
        else
        {
            if (time == float.MaxValue)
            {
                path = $"{Path.Combine(Application.dataPath, filename)}_{index.ToString("D4")}{extension}";
            }            
            else
            {
                path = $"{Path.Combine(Application.dataPath, filename)}_{index.ToString("D4")}_{Mathf.FloorToInt(time * 1000.0f).ToString("D6")}{extension}";
            }
        }
        File.WriteAllBytes(path, bytes);

        // Apply texture import settings
        string relativePath = path.Replace(Application.dataPath, "Assets");
        ApplyTextureImportSettings(relativePath, isAtlas, texture.width, texture.height);
    }
    void ApplyTextureImportSettings(string assetPath, bool isAtlas, int width, int height)
    {
        AssetDatabase.ImportAsset(assetPath);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 1;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Configure sRGB
            importer.sRGBTexture = (recolorMode != RecolorMode.Realtime) && (sRGB);

            // Configure sprite mode
            if (!isAtlas) // Single image
            {
                importer.spriteImportMode = SpriteImportMode.Single;
            }
            else // Sprite sheet
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;

                // Use ISpriteEditorDataProvider to set up sprite sheet slices
                importer.isReadable = true; // Ensure texture is readable
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                AssetDatabase.ImportAsset(assetPath); // Re-import to apply changes

                // Open the sprite data provider
                var spriteDataProvider = AssetImporter.GetAtPath(assetPath) as ISpriteEditorDataProvider;
                if (spriteDataProvider != null)
                {
                    spriteDataProvider.InitSpriteEditorDataProvider();

                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    int frameWidth = spriteWidth;
                    int frameHeight = spriteHeight;
                    int rows = height / frameHeight;
                    int columns = width / frameWidth;

                    // Set sprite sheet metadata
                    var sprites = new List<SpriteRect>();
                    for (int y = 0; y < rows; y++)
                    {
                        for (int x = 0; x < columns; x++)
                        {
                            int spriteIndex = y * columns + x;

                            var rect = new Rect(x * frameWidth, (height - (y + 1) * frameHeight), frameWidth, frameHeight);
                            var spriteRect = new SpriteRect
                            {
                                name = $"frame_{spriteIndex}",
                                rect = rect,
                                alignment = (int)SpriteAlignment.Center,
                                pivot = new Vector2(0.5f, 0.5f)
                            };

                            sprites.Add(spriteRect);
                        }
                    }

                    spriteDataProvider.SetSpriteRects(sprites.ToArray());
                    spriteDataProvider.Apply();
                }
            }

            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            AssetDatabase.Refresh();
        }
    }
#endif
}
#endif
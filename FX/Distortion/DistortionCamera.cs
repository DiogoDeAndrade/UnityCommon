using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[ExecuteInEditMode]
public class DistortionCamera : MonoBehaviour
{
    [SerializeField] 
    private LayerMask   distortionLayers;
    [SerializeField, Range(1, 5)] 
    private int         renderTargetDiv = 1;
    [SerializeField]
    private int         rendererIndex = 1;
    [SerializeField, Range(0.0f, 1.0f)]
    private float       distortionStrenght;    
    [SerializeField]
    private Material    distortPostProcessMaterial;

    Camera distortCamera;
    Camera                          mainCamera;
    RenderTexture                   distortionTexture;
    UniversalAdditionalCameraData   distortCameraData;

    private int     lastScreenWidth;
    private int     lastScreenHeight;
    private int     lastRenderTargetDiv;

    private float   renderTargetScale => 1.0f / Mathf.Pow(2, renderTargetDiv - 1);

    void Start()
    {
        mainCamera = GetComponentInParent<Camera>();
        distortCamera = GetComponent<Camera>();
        if (distortCamera == null)
        {
            distortCamera = gameObject.AddComponent<Camera>();
        }
        distortCamera.CopyFrom(mainCamera);
        distortCamera.depth = -100;
        distortCamera.cullingMask = distortionLayers;
        distortCamera.clearFlags = CameraClearFlags.SolidColor;
        distortCamera.backgroundColor = new Color(0.5f, 0.5f, 1.0f, 1.0f);
        distortCamera.transform.localPosition = Vector3.zero;
        distortCamera.transform.localRotation = Quaternion.identity;
        
        UpdateRenderTexture();
    }

    private void UpdateRenderTexture()
    {
        var prevDistortTexture = distortionTexture;

        int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * renderTargetScale));
        int height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * renderTargetScale));

        var desc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24, Texture.GenerateAllMips, RenderTextureReadWrite.Linear);
        desc.msaaSamples = 1;
        desc.useMipMap = false;
        desc.autoGenerateMips = false;

        distortionTexture = new RenderTexture(desc);
        distortionTexture.name = "_DistortionTexture";
        distortionTexture.Create();

        if (distortCamera != null)
        {
            distortCamera.targetTexture = distortionTexture;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        lastRenderTargetDiv = renderTargetDiv;

        if (prevDistortTexture != null)
        {
            prevDistortTexture.Release();
            DestroyImmediate(prevDistortTexture);
        }
    }

    void LateUpdate()
    {
        if ((Screen.width != lastScreenWidth) || (Screen.height != lastScreenHeight) || (renderTargetDiv != lastRenderTargetDiv))
        {
            UpdateRenderTexture();
        }

        if (distortCameraData == null)
        {
            distortCameraData = distortCamera.GetComponent<UniversalAdditionalCameraData>();
            if (distortCameraData != null)
            {
                distortCameraData.renderPostProcessing = false;
                distortCameraData.SetRenderer(rendererIndex);
            }
        }

        Shader.SetGlobalTexture("_DistortionMap", distortionTexture);
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (distortPostProcessMaterial == null)
            return;

        if (cam != null && cam.cameraType == CameraType.SceneView)
        {
            distortPostProcessMaterial.SetFloat("_DistortionStrength", 0f);
        }
        else
        {
            distortPostProcessMaterial.SetFloat("_DistortionStrength", distortionStrenght);
        }
    }
}

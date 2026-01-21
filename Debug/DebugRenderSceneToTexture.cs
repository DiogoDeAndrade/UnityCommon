using NaughtyAttributes;
using UC;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System;

[ExecuteAlways]
public class DebugRenderSceneToTexture : MonoBehaviour
{
    private enum CameraType
    {
        Perspective,
        Orthographic
    }

    private enum RenderTarget
    {
        Scene,
        Object,
        ObjectAndChildren
    }

    [SerializeField] 
    private CameraType  cameraType = CameraType.Perspective;
    [SerializeField, ShowIf(nameof(isPerspective))] 
    private float       fieldOfView = 60f;
    [SerializeField, ShowIf(nameof(isOrtographic))] 
    private Vector2     leftRight = new Vector2(-10f, 10f);
    [SerializeField, ShowIf(nameof(isOrtographic))] 
    private Vector2     bottomTop = new Vector2(-10f, 10f);
    [SerializeField, MinMaxSlider(0.0f, 1000.0f)]
    private Vector2     zRange = new Vector2(0.3f, 100.0f);
    [SerializeField]
    private RenderTarget    renderTargetType = RenderTarget.Scene;
    [SerializeField, ShowIf(nameof(hasTarget))]
    private Transform      targetObject = null;
    [SerializeField]
    private RenderTexture renderTarget;
    [SerializeField]
    private bool        shouldClearColor = true;
    [SerializeField, ShowIf(nameof(shouldClearColor))]
    private bool        randomColor = false;  
    [SerializeField, ShowIf(nameof(shouldClearColorAndNotRandom))]
    private Color       clearColor = Color.black;
    [SerializeField]
    private bool        shouldClearDepth = true;
    [SerializeField, ShowIf(nameof(shouldClearDepth))]
    private float       clearDepth = 1.0f;
    [SerializeField]
    private bool        overrideMaterials;
    [SerializeField, ShowIf(nameof(overrideMaterials))]
    private Material[]  ovrMaterials;

    private bool isPerspective => cameraType == CameraType.Perspective;
    private bool isOrtographic => cameraType == CameraType.Orthographic;
    private bool hasTarget => renderTargetType != RenderTarget.Scene;
    private bool shouldClearColorAndNotRandom => shouldClearColor && !randomColor;

    void Start()
    {
        
    }

    void Update()
    {
        if (renderTarget == null) return;

        Matrix4x4 viewMatrix = GraphicsHelper.GetUnityCameraMatrix(transform); 
        Matrix4x4 projMatrix = Matrix4x4.identity;
        switch (cameraType)
        {
            case CameraType.Perspective:
                projMatrix = Matrix4x4.Perspective(fieldOfView, (float)renderTarget.width / (float)renderTarget.height, zRange.x, zRange.y);
                break;
            case CameraType.Orthographic:
                projMatrix = Matrix4x4.Ortho(leftRight.x, leftRight.y, bottomTop.x, bottomTop.y, zRange.x, zRange.y);
                break;
        }//*/

        /*Camera camera = GetComponent<Camera>();
        if (camera)
        {
            Matrix4x4 viewMatrixCam = camera.worldToCameraMatrix;
            Matrix4x4 projMatrixCam = camera.projectionMatrix;

            //Debug.Log($"ViewMatrix =\n{viewMatrix}\nProjMatrix =\n{projMatrix}\nCamera (Correct):\nViewMatrix =\n{viewMatrixCam}\nProjMatrix =\n{projMatrixCam}");
        }*/

        Color c = (randomColor) ? (ColorExtensions.RandomRGB()) : (clearColor);

        var renderList = GetRenderList();

        GraphicsHelper.QuickDraw(renderTarget, viewMatrix, projMatrix, (CommandBuffer cmd) =>
        {
            foreach (var mr in renderList)
            {
                var mf = mr.GetComponent<MeshFilter>();
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;

                int         matCount = mesh.subMeshCount;   
                var         materials = mr.sharedMaterials;
                Material material = null;

                if (matCount == 1)
                {
                    if (overrideMaterials)
                    {
                        for (int i = 0; i < ovrMaterials.Length; i++)
                        {
                            material = ovrMaterials[i];
                            cmd.DrawMesh(mesh, mr.transform.localToWorldMatrix, material, 0, 0);
                        }
                    }
                    else
                    {
                        material = materials[0];
                        cmd.DrawMesh(mesh, mr.transform.localToWorldMatrix, material, 0, 0);
                    }
                }
                else
                {
                    for (int j = 0; j < matCount; j++)
                    {
                        if ((overrideMaterials) && (ovrMaterials.Length > 0))
                        {
                            material = ovrMaterials[j % ovrMaterials.Length];
                        }
                        else
                        {
                            if (materials.Length > j) material = materials[j];
                            else material = materials[0];
                        }

                        cmd.DrawMesh(mesh, mr.transform.localToWorldMatrix, material, j, 0);

                    }
                }
            }
        },
        shouldClearColor, c, shouldClearDepth, clearDepth);
    }

    List<MeshRenderer> GetRenderList()
    {
        var ret = new List<MeshRenderer>();

        switch (renderTargetType)
        {
            case RenderTarget.Scene:
                ret.AddRange(FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None));
                break;
            case RenderTarget.Object:
                {
                    var mr = targetObject?.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        ret.Add(mr);
                    }
                }
                break;
            case RenderTarget.ObjectAndChildren:
                ret.AddRange(targetObject.GetComponentsInChildren<MeshRenderer>());
                break;
            default:
                break;
        }

        return ret;
    }
}

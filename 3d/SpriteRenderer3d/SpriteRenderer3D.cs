using UnityEngine;
using Unity.Collections;
using UnityEngine.Rendering;
using NaughtyAttributes;
using System;

[ExecuteAlways]
public class SpriteRenderer3D : MonoBehaviour
{
    public enum BillboardMode { None, FaceCamera, AxisAlignedY }

    [SerializeField] 
    private    Sprite   _sprite;
    [SerializeField]
    private Color       _color = Color.white;
    [SerializeField] 
    private    Material _material;

    [SerializeField] 
    private    BillboardMode   _billboardMode = BillboardMode.FaceCamera;
    [SerializeField] 
    private    Vector3         _pivotOffset = Vector3.zero;
    [SerializeField, Tooltip("Rotation applied after billboarding, in billboard/local space (degrees). X=pitch, Y=yaw, Z=roll.")]
    private Vector3            _rotationOffsetEuler = Vector3.zero;

    [SerializeField, Layer] 
    private    int                 _layer = 0;
    [SerializeField] 
    private    ShadowCastingMode   _shadowCasting = ShadowCastingMode.Off;
    [SerializeField] 
    private    bool                _receiveShadows = false;
    [SerializeField] 
    private LightProbeUsage        _lightProbeUsage = LightProbeUsage.Off; // Note: DrawMesh won't do light probes like a Renderer

    Mesh                    _mesh;
    Sprite                  _meshSprite;
    MaterialPropertyBlock   _mpb;
    bool                    _prevReceiveShadows = true;

    private void Start()
    {
        _prevReceiveShadows = !_receiveShadows;
    }

    void OnEnable()
    {
        EnsureMesh();
        EnsureMPB();
        UpdateMPB();

        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    void OnValidate()
    {
        EnsureMesh();
        EnsureMPB();
        UpdateMPB();
    }

    void EnsureMPB()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    }

    void EnsureMesh()
    {
        if (_sprite == null)
        {
            _mesh = null;
            return;
        }

        if ((_mesh == null) || (_meshSprite != _sprite))
        {
            if (_mesh == null) _mesh = new Mesh { name = $"Sprite3D_{_sprite.name}" };

            BuildMeshFromSprite(_sprite, _mesh);
            _meshSprite = _sprite;
        }
    }

    static void BuildMeshFromSprite(Sprite s, Mesh m)
    {
        // Use the sprite’s real mesh & UVs (atlas/rotation-safe)
        // Sprite vertices are in sprite local units (already scaled by pixelsPerUnit).
        var verts2D = s.vertices;           // Vector2[]
        var tris = s.triangles;         // ushort[]
        var uvs = s.uv;                // Vector2[]

        // Expand to Vector3
        var verts3D = new Vector3[verts2D.Length];
        for (int i = 0; i < verts2D.Length; i++)
            verts3D[i] = new Vector3(verts2D[i].x, verts2D[i].y, 0f);

        m.Clear();
        m.SetVertices(verts3D);
        // Triangles are ushort in Sprite; convert to int[]
        var trisInt = new int[tris.Length];
        for (int i = 0; i < tris.Length; i++) trisInt[i] = tris[i];
        m.SetTriangles(trisInt, 0, true);
        m.SetUVs(0, uvs);

        // Optional: simple normals/tangents so it can be lit if your shader expects them
        var normals = new Vector3[verts3D.Length];
        for (int i = 0; i < normals.Length; i++) normals[i] = -Vector3.forward;
        m.SetNormals(normals);

        // Bounds: use sprite bounds (already in local units)
        m.bounds = s.bounds;
    }

    void UpdateMPB()
    {
        if (_mpb == null) return;
        _mpb.Clear();

        if (_sprite != null)
        {
            // Common property names for URP/Lit or Unlit:
            // Try set both _BaseMap and _MainTex to be friendly with various shaders.
            var tex = _sprite.texture;
            _mpb.SetTexture("_BaseMap", tex);
            _mpb.SetTexture("_MainTex", tex);

            // If your shader needs per-sprite color, add it here:
            _mpb.SetColor("_BaseColor", _color);

            ApplySecondaryTexturesToMPB(_sprite, _material, _mpb);
        }
    }

    void LateUpdate()
    {
        // Rebuild if sprite changed at runtime
        if ((_mesh != null) && (_sprite != null))
        {
            EnsureMesh();
            UpdateMPB();

            _material.EnableKeyword("ABCD");

            if (_prevReceiveShadows != _receiveShadows)
            {
                Debug.Log($"SpriteRenderer3D: Updating receiveShadows keyword to {_receiveShadows} on material {_material.name}.");
                if (_receiveShadows)
                {
                    _material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
                }
                else
                {
                    _material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                }
                _prevReceiveShadows = _receiveShadows;
            }
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (_mesh == null || _material == null) return;

        // Compute billboard rotation per camera
        Quaternion faceRot = transform.rotation;
        if (_billboardMode == BillboardMode.FaceCamera)
        {
            var dir = (transform.position + transform.rotation * _pivotOffset) - camera.transform.position;
            if (dir.sqrMagnitude > 1e-8f)
                faceRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else if (_billboardMode == BillboardMode.AxisAlignedY)
        {
            Vector3 toCam = camera.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-8f)
                faceRot = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
        }

        if (_rotationOffsetEuler != Vector3.zero)
        {
            faceRot = faceRot * Quaternion.Euler(_rotationOffsetEuler);
        }

        // Apply the pivotOffset in the billboarded space
        var worldPos = transform.position + faceRot * _pivotOffset;
        var worldScale = transform.lossyScale;
        var matrix = Matrix4x4.TRS(worldPos, faceRot, worldScale);

        // Draw once per camera
        // Note: Graphics.DrawMesh renders for the current frame; calling here ensures per-camera billboarding.
        Graphics.DrawMesh(_mesh, matrix, _material, _layer, camera, 0, _mpb, _shadowCasting, _receiveShadows, null, _lightProbeUsage);
    }

    public Material GetSharedMaterial() => _material;
    public void SetSharedMaterial(Material m) { _material = m; }

    static void ApplySecondaryTexturesToMPB(Sprite sprite, Material material, MaterialPropertyBlock mpb)
    {
        if ((sprite == null) || (mpb == null) || (material == null))
            return;

        var secondaryTextures = new SecondarySpriteTexture[4];
        int secondaryTextureCount = sprite.GetSecondaryTextures(secondaryTextures);
        if (secondaryTextureCount == 0)
        {
            material.DisableKeyword("_NORMALMAP");
            material.DisableKeyword("_METALLICGLOSSMAP");
            material.DisableKeyword("_EMISSIONMAP");

            return;
        }

        // Local helper: case-insensitive match, ignores leading underscore
        Texture2D FindTexture(string name)
        {
            string target = name.TrimStart('_');
            for (int i = 0; i < secondaryTextureCount; i++)
            {
                var sst = secondaryTextures[i];
                if (sst.texture == null) continue;  
                
                string entry = sst.name.TrimStart('_');
                if (string.Equals(entry, target, System.StringComparison.OrdinalIgnoreCase))
                {
                    return sst.texture;
                }
            }
            return null;
        }

        // Normal map
        var normal = FindTexture("_BumpMap") ?? FindTexture("_NormalMap");
        if (normal != null)
        {
            if (material.HasProperty("_BumpMap")) mpb.SetTexture("_BumpMap", normal);
            if (material.HasProperty("_NormalMap")) mpb.SetTexture("_NormalMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        else
        {
            if ((material.HasProperty("_BumpMap")) || (material.HasProperty("_NormalMap")))
            {
                material.DisableKeyword("_NORMALMAP");
            }
        }

        // Metallic/smoothness map
        var metallic = FindTexture("_MetallicGlossMap");
        if (metallic != null)
        {
            if (material.HasProperty("_MetallicGlossMap"))
            {
                mpb.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
        }
        else
        {
            if (material.HasProperty("_MetallicGlossMap"))
            {
                material.DisableKeyword("_METALLICGLOSSMAP");
            }
        }

        // --- Emission map ---
        var emission = FindTexture("_EmissionMap");
        if (emission != null)
        {
            if (material.HasProperty("_EmissionMap"))
            {
                mpb.SetTexture("_EmissionMap", emission);
                material.EnableKeyword("_EMISSIONMAP");
            }
        }
        else
        {
            material.DisableKeyword("_EMISSIONMAP");
        }
    }
}

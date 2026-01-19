using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static UC.SimpleOutline3d;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DebugOverrideMeshDraw : MonoBehaviour
{
    public enum DrawTarget
    {
        SceneViewOnly,
        GameViewOnly,
        SceneAndGame
    }

    public enum DisableOriginalsMode
    {
        Always,
        OnlyWhenDrawingSceneView,
        OnlyWhenDrawingGameView,
        Never
    }

    public enum Component
    {
        Position, Normal, Tangent, 
        Color,
        UV0, UV1, UV2, UV3, UV4, UV5, UV6, UV7,
    };

    [Header("Override")]
    [SerializeField] private bool _enabledOverride = false;
    [SerializeField] private Material _overrideMaterial;

    [Header("Where to draw")]
    [SerializeField] private DrawTarget _drawTarget = DrawTarget.SceneAndGame;

    [Header("Original renderers")]
    [SerializeField] private DisableOriginalsMode _disableOriginals = DisableOriginalsMode.Always;

    [Header("Scan Options")]
    [SerializeField] private bool _includeInactive = true;
    [SerializeField] private bool _includeSkinnedMeshRenderers = true;

    [Header("Rendering Options")]
    [SerializeField] private bool _castShadows = false;
    [SerializeField] private bool _receiveShadows = false;

    [Tooltip("Layer used for the draw call (affects lighting & some culling).")]
    [SerializeField] private int _drawLayer = 0;

    private readonly List<Entry> _entries = new();

    [Serializable]
    private sealed class Entry
    {
        public Renderer originalRenderer;
        public bool originalEnabled;

        public bool isSkinned;

        public MeshFilter meshFilter;
        public SkinnedMeshRenderer skinned;

        public Mesh meshInstance;
        public Transform tr;

        public bool dirty;
    }

    // We track per-camera whether originals were forced off, so we can restore safely.
    private readonly Dictionary<int, List<Renderer>> _disabledThisCamera = new();

    public bool EnabledOverride
    {
        get => _enabledOverride;
        set
        {
            if (_enabledOverride == value) return;
            _enabledOverride = value;
            ApplyState();
        }
    }

    public Material OverrideMaterial
    {
        get => _overrideMaterial;
        set
        {
            _overrideMaterial = value;
            ApplyState();
        }
    }

    public DrawTarget Target
    {
        get => _drawTarget;
        set => _drawTarget = value;
    }

    public DisableOriginalsMode DisableOriginals
    {
        get => _disableOriginals;
        set => _disableOriginals = value;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        ApplyState();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        // Ensure originals come back even if we were mid-frame.
        RestoreAllTemporarilyDisabled();
        Teardown();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyState();
    }
#endif

    [ContextMenu("Debug Override/Enable")]
    private void CtxEnable() { _enabledOverride = true; ApplyState(); }

    [ContextMenu("Debug Override/Disable")]
    private void CtxDisable() { _enabledOverride = false; ApplyState(); }

    [ContextMenu("Debug Override/Rebuild (Rescan)")]
    private void CtxRebuild()
    {
        if (!_enabledOverride) return;
        RestoreAllTemporarilyDisabled();
        Teardown();
        Build();
        ApplyState();
    }

    private void ApplyState()
    {
        if (!_enabledOverride || _overrideMaterial == null)
        {
            RestoreAllTemporarilyDisabled();
            Teardown();
            return;
        }

        if (_entries.Count == 0)
            Build();

        // If we're in "Always" mode, do the original behaviour (disable once, globally).
        if (_disableOriginals == DisableOriginalsMode.Always)
            SetOriginalsEnabled(false);
        else
            SetOriginalsEnabled(true); // keep originals generally on; we'll toggle per-camera if needed.
    }

    private void Build()
    {
        Teardown();

        var meshRenderers = GetComponentsInChildren<MeshRenderer>(_includeInactive);
        foreach (var mr in meshRenderers)
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            _entries.Add(new Entry
            {
                originalRenderer = mr,
                originalEnabled = mr.enabled,
                isSkinned = false,
                meshFilter = mf,
                skinned = null,
                meshInstance = Instantiate(mf.sharedMesh), // editable copy
                tr = mr.transform
            });
        }

        if (_includeSkinnedMeshRenderers)
        {
            var skinned = GetComponentsInChildren<SkinnedMeshRenderer>(_includeInactive);
            foreach (var smr in skinned)
            {
                if (smr.sharedMesh == null) continue;

                var baked = new Mesh { name = smr.sharedMesh.name + " (DEBUG BAKED)" };
                baked.MarkDynamic();

                _entries.Add(new Entry
                {
                    originalRenderer = smr,
                    originalEnabled = smr.enabled,
                    isSkinned = true,
                    meshFilter = null,
                    skinned = smr,
                    meshInstance = baked,
                    tr = smr.transform
                });
            }
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            var m = _entries[i].meshInstance;
            if (m != null && !m.name.Contains("DEBUG"))
                m.name += " (DEBUG COPY)";
        }
    }

    private void Teardown()
    {
        // Restore originals to captured state.
        SetOriginalsEnabled(true);

        for (int i = 0; i < _entries.Count; i++)
        {
            var m = _entries[i].meshInstance;
            if (m == null) continue;

#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(m);
            else Destroy(m);
#else
            Destroy(m);
#endif
        }

        _entries.Clear();
    }

    private void SetOriginalsEnabled(bool restore)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            var r = _entries[i].originalRenderer;
            if (r == null) continue;

            r.enabled = restore ? _entries[i].originalEnabled : false;
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!_enabledOverride || _overrideMaterial == null) return;
        if (_entries.Count == 0) return;

        if (!CameraMatchesTarget(cam, _drawTarget)) return;

        // Optionally disable originals only for this camera.
        if (ShouldDisableOriginalsForCamera(cam, _disableOriginals))
            TemporarilyDisableOriginalsForCamera(cam);

        // Draw all cached meshes
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if ((e.tr == null) || (e.meshInstance == null)) continue;
            if (!e.tr.gameObject) continue;

            if (e.isSkinned)
            {
                if (e.skinned == null) continue;
                e.skinned.BakeMesh(e.meshInstance);
            }

            if (e.dirty)
            {
                // If we had any data changed, upload to GPU now.
                e.meshInstance.UploadMeshData(false);
                e.dirty = false;
            }

            var matrix = e.tr.localToWorldMatrix;

            int subMeshCount = Mathf.Max(1, e.meshInstance.subMeshCount);
            for (int sub = 0; sub < subMeshCount; sub++)
            {
                Graphics.DrawMesh(e.meshInstance, matrix, _overrideMaterial, _drawLayer, cam, sub, properties: null, castShadows: _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off, receiveShadows: _receiveShadows);
            }
        }
    }

    private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        // Restore originals that we temporarily disabled for this camera.
        if (!_enabledOverride) return;
        RestoreTemporarilyDisabledForCamera(cam);
    }

    private static bool CameraMatchesTarget(Camera cam, DrawTarget target)
    {
        bool isSceneView = cam.cameraType == CameraType.SceneView;

        bool isGameView =
            cam.cameraType == CameraType.Game ||
            cam.cameraType == CameraType.VR ||
            cam.cameraType == CameraType.Reflection;

        return target switch
        {
            DrawTarget.SceneViewOnly => isSceneView,
            DrawTarget.GameViewOnly => isGameView,
            DrawTarget.SceneAndGame => isSceneView || isGameView,
            _ => true
        };
    }

    private static bool ShouldDisableOriginalsForCamera(Camera cam, DisableOriginalsMode mode)
    {
        if (mode == DisableOriginalsMode.Never) return false;
        if (mode == DisableOriginalsMode.Always) return false; // handled globally in ApplyState()

        bool isSceneView = cam.cameraType == CameraType.SceneView;
        bool isGameView =
            cam.cameraType == CameraType.Game ||
            cam.cameraType == CameraType.VR ||
            cam.cameraType == CameraType.Reflection;

        return mode switch
        {
            DisableOriginalsMode.OnlyWhenDrawingSceneView => isSceneView,
            DisableOriginalsMode.OnlyWhenDrawingGameView => isGameView,
            _ => false
        };
    }

    private void TemporarilyDisableOriginalsForCamera(Camera cam)
    {
        int id = cam.GetInstanceID();
        if (_disabledThisCamera.ContainsKey(id)) return; // already done this camera this frame

        var list = new List<Renderer>(_entries.Count);
        for (int i = 0; i < _entries.Count; i++)
        {
            var r = _entries[i].originalRenderer;
            if (r == null) continue;

            // Only disable those that are currently enabled (avoid stomping on other systems).
            if (r.enabled)
            {
                r.enabled = false;
                list.Add(r);
            }
        }

        _disabledThisCamera[id] = list;
    }

    private void RestoreTemporarilyDisabledForCamera(Camera cam)
    {
        int id = cam.GetInstanceID();
        if (!_disabledThisCamera.TryGetValue(id, out var list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            var r = list[i];
            if (r == null) continue;
            r.enabled = true; // restore to enabled; we only disabled those that were enabled
        }

        _disabledThisCamera.Remove(id);
    }

    private void RestoreAllTemporarilyDisabled()
    {
        foreach (var kv in _disabledThisCamera)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;
                r.enabled = true;
            }
        }
        _disabledThisCamera.Clear();
    }

    /// <summary>
    /// Editable meshes currently being drawn.
    /// For MeshFilter entries: duplicated copy you can deform.
    /// For Skinned entries: baked each frame (edits overwritten unless applied after bake).
    /// </summary>
    public IReadOnlyList<Mesh> GetEditableMeshes()
    {
        var list = new List<Mesh>(_entries.Count);
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].meshInstance != null)
            {
                list.Add(_entries[i].meshInstance);
            }
        }
        return list;
    }

    public int meshCount => _entries.Count;

    public int GetMeshVertexCount(int meshIndex)
    {
        if ((meshIndex < 0) || (meshIndex >= _entries.Count)) return 0;

        var mesh = _entries[meshIndex].meshInstance;
        return mesh != null ? mesh.vertexCount : 0;
    }
    public Mesh GetMesh(int meshIndex)
    {
        if ((meshIndex < 0) || (meshIndex >= _entries.Count)) return null;

        var mesh = _entries[meshIndex].meshInstance;

        return mesh;
    }

    public Matrix4x4 GetModelMatrix(int meshIndex)
    {
        if ((meshIndex < 0) || (meshIndex >= _entries.Count)) return Matrix4x4.identity;

        var matrix = _entries[meshIndex].tr.localToWorldMatrix;

        return matrix;
    }

    public void SetData(int meshIndex, Component component, Color[] colors)
    {
        if ((meshIndex < 0) || (meshIndex >= _entries.Count)) return;

        var entry = _entries[meshIndex];
        switch (component)
        {
            case Component.Color:
                entry.meshInstance.SetColors(colors);
                entry.dirty = true;
                break;
            default:
                Debug.LogWarning($"DebugOverrideMeshDraw: Can't set colors on component {component}!");
                break;
        }
    }

    public void SetData(int meshIndex, Component component, Vector2[] uvs)
    {
        if ((meshIndex < 0) || (meshIndex >= _entries.Count)) return;

        var entry = _entries[meshIndex];
        switch (component)
        {
            case Component.UV0:
                entry.meshInstance.SetUVs(0, uvs);
                entry.dirty = true;
                break;
            case Component.UV1:
                entry.meshInstance.SetUVs(1, uvs);
                entry.dirty = true;
                break;
            case Component.UV2:
                entry.meshInstance.SetUVs(2, uvs);
                entry.dirty = true;
                break;
            case Component.UV3:
                entry.meshInstance.SetUVs(3, uvs);
                entry.dirty = true;
                break;
            case Component.UV4:
                entry.meshInstance.SetUVs(4, uvs);
                entry.dirty = true;
                break;
            case Component.UV5:
                entry.meshInstance.SetUVs(5, uvs);
                entry.dirty = true;
                break;
            case Component.UV6:
                entry.meshInstance.SetUVs(6, uvs);
                entry.dirty = true;
                break;
            case Component.UV7:
                entry.meshInstance.SetUVs(7, uvs);
                entry.dirty = true;
                break;
            default:
                Debug.LogWarning($"DebugOverrideMeshDraw: Can't set Vector2 on component {component}!");
                break;
        }
    }

    public void SetData(int meshIndex, Component component, Vector3[] inVec3)
    {
        if ((meshIndex < 0) || (meshIndex >= _entries.Count)) return;

        var entry = _entries[meshIndex];
        switch (component)
        {
            case Component.Position:
                entry.meshInstance.SetVertices(inVec3);
                entry.dirty = true;
                break;
            case Component.Normal:
                entry.meshInstance.SetNormals(inVec3);
                entry.dirty = true;
                break;
            case Component.UV0:
                entry.meshInstance.SetUVs(0, inVec3);
                entry.dirty = true;
                break;
            case Component.UV1:
                entry.meshInstance.SetUVs(1, inVec3);
                entry.dirty = true;
                break;
            case Component.UV2:
                entry.meshInstance.SetUVs(2, inVec3);
                entry.dirty = true;
                break;
            case Component.UV3:
                entry.meshInstance.SetUVs(3, inVec3);
                entry.dirty = true;
                break;
            case Component.UV4:
                entry.meshInstance.SetUVs(4, inVec3);
                entry.dirty = true;
                break;
            case Component.UV5:
                entry.meshInstance.SetUVs(5, inVec3);
                entry.dirty = true;
                break;
            case Component.UV6:
                entry.meshInstance.SetUVs(6, inVec3);
                entry.dirty = true;
                break;
            case Component.UV7:
                entry.meshInstance.SetUVs(7, inVec3);
                entry.dirty = true;
                break;
            default:
                Debug.LogWarning($"DebugOverrideMeshDraw: Can't set Vector3 on component {component}!");
                break;
        }
    }

    public void SetData(int meshIndex, Component component, Vector4[] inVec4)
    {
        if ((meshIndex < 0) || (meshIndex >= _entries.Count)) return;

        var entry = _entries[meshIndex];
        switch (component)
        {
            case Component.Tangent:
                entry.meshInstance.SetTangents(inVec4);
                entry.dirty = true;
                break;
            case Component.UV0:
                entry.meshInstance.SetUVs(0, inVec4);
                entry.dirty = true;
                break;
            case Component.UV1:
                entry.meshInstance.SetUVs(1, inVec4);
                entry.dirty = true;
                break;
            case Component.UV2:
                entry.meshInstance.SetUVs(2, inVec4);
                entry.dirty = true;
                break;
            case Component.UV3:
                entry.meshInstance.SetUVs(3, inVec4);
                entry.dirty = true;
                break;
            case Component.UV4:
                entry.meshInstance.SetUVs(4, inVec4);
                entry.dirty = true;
                break;
            case Component.UV5:
                entry.meshInstance.SetUVs(5, inVec4);
                entry.dirty = true;
                break;
            case Component.UV6:
                entry.meshInstance.SetUVs(6, inVec4);
                entry.dirty = true;
                break;
            case Component.UV7:
                entry.meshInstance.SetUVs(7, inVec4);
                entry.dirty = true;
                break;
            default:
                Debug.LogWarning($"DebugOverrideMeshDraw: Can't set Vector4 on component {component}!");
                break;
        }
    }

}

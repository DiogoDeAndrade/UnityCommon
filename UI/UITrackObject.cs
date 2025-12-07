using NaughtyAttributes;
using System;
using UC;
using UnityEngine;

public class UITrackObject : MonoBehaviour
{
    [Flags]
    private enum UpdateMode { None = 0, FixedUpdate = 1, Update = 2, LateUpdate = 4};

    [SerializeField]
    private Transform   _trackedObject;
    [SerializeField, HideIf(nameof(hasCameraTag))]
    private Camera      mainCamera;
    [SerializeField, HideIf(nameof(hasCamera))]
    private Hypertag    cameraTag;
    [SerializeField, HideIf(nameof(hasCanvasTag))]
    private Canvas      mainCanvas;
    [SerializeField, HideIf(nameof(hasCanvas))]
    private Hypertag    canvasTag;
    [SerializeField]
    private UpdateMode  _updateMode = UpdateMode.Update;

    RectTransform rectTransform;

    public Transform trackedObject
    {
        get
        {
            return _trackedObject;
        }
        set
        {
            _trackedObject = value;
        }
    }

    bool hasCameraTag => cameraTag != null;
    bool hasCamera => mainCamera != null;
    bool hasCanvasTag => canvasTag != null;
    bool hasCanvas => mainCanvas != null;

    protected virtual void Start()
    {
        if (mainCamera == null)
        {
            if (cameraTag == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null) mainCamera = FindAnyObjectByType<Camera>();
            }
            else
            {
                mainCamera = Hypertag.FindFirstObjectWithHypertag<Camera>(cameraTag);
            }
        }
        if (mainCanvas == null)
        {
            if (canvasTag == null)
            {
                mainCanvas = GetComponentInParent<Canvas>();
                if (mainCanvas == null)
                {
                    mainCanvas = FindAnyObjectByType<Canvas>();
                }
            }
            else
            {
                mainCanvas = Hypertag.FindFirstObjectWithHypertag<Canvas>(canvasTag);
            }
        }

        rectTransform = transform as RectTransform;
        
        UpdateTrackedObject();
    }

    protected virtual void FixedUpdate()
    {
        if ((_updateMode & UpdateMode.FixedUpdate) != 0)
            UpdateTrackedObject();
    }

    protected virtual void Update()
    {
        if ((_updateMode & UpdateMode.Update) != 0)
            UpdateTrackedObject();
    }

    protected virtual void LateUpdate()
    {
        if ((_updateMode & UpdateMode.LateUpdate) != 0)
            UpdateTrackedObject();
    }

    /*void UpdateTrackedObject()
    {
        if (rectTransform == null) return;

        if (mainCamera != null)
        {
            // Convert the 3D position of the source to screen space
            Vector3 screenPos = mainCamera.WorldToScreenPoint(_trackedObject.position);

            // Check if the object is in front of the camera
            if (screenPos.z > 0)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform.parent.transform as RectTransform, screenPos, mainCamera, out var localPoint);

                rectTransform.anchoredPosition = localPoint;
            }
        }
    }*/

    void UpdateTrackedObject()
    {
        if (rectTransform == null || mainCamera == null || mainCanvas == null || _trackedObject == null)
            return;

        // 1. World -> Screen
        Vector3 screenPos = mainCamera.WorldToScreenPoint(_trackedObject.position);

        // If object is behind camera, you might want to hide the UI, etc.
        if (screenPos.z < 0f)
            return;

        // 2. Screen -> Root canvas local
        RectTransform canvasRect = mainCanvas.transform as RectTransform;
        Camera camera = mainCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, camera, out Vector2 canvasLocalPos))
        {
            // 3. Canvas local -> world, then assign world position
            Vector3 worldPos = mainCanvas.transform.TransformPoint(canvasLocalPos);
            rectTransform.position = worldPos;
        }
    }
}

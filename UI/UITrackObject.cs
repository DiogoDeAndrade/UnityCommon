using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.UI;

public class UITrackObject : MonoBehaviour
{
    [Flags]
    private enum UpdateMode { None, FixedUpdate, Update, LateUpdate };

    public delegate bool OnVisibilityCallback(UITrackObject trackedObject);
    public event OnVisibilityCallback onVisibilityCallback;


    [SerializeField, HideIf("hasCameraTag")]
    private Camera      mainCamera;
    [SerializeField, HideIf("hasCamera")]
    private Hypertag    cameraTag;
    [SerializeField, HideIf("hasCanvasTag")]
    private Canvas      mainCanvas;
    [SerializeField, HideIf("hasCanvas")]
    private Hypertag    canvasTag;
    [SerializeField]
    private UpdateMode  _updateMode = UpdateMode.Update;
    [SerializeField]
    private Sprite      _uiSprite;
    [SerializeField]
    private float       _uiSize = 32.0f;

    bool hasCameraTag => cameraTag != null;
    bool hasCamera => mainCamera != null;
    bool hasCanvasTag => canvasTag != null;
    bool hasCanvas => mainCanvas != null;

    RectTransform   uiImageRectTransform;
    Image           image;

    void Start()
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
                mainCamera = Hypertag.FindObjectWithHypertag<Camera>(cameraTag);
            }
        }
        if (mainCanvas == null)
        {
            if (canvasTag == null)
            {
                mainCanvas = FindAnyObjectByType<Canvas>();
            }
            else
            {
                mainCanvas = Hypertag.FindObjectWithHypertag<Canvas>(canvasTag);
            }
        }

        if (_uiSprite == null) return;

        // Create UI object
        GameObject go = new GameObject();
        go.transform.SetParent(mainCanvas.transform);
        go.name = $"UITracker ({_uiSprite.name})";
        image = go.AddComponent<Image>();
        image.sprite = _uiSprite;

        var rectTransform = image.rectTransform;
        rectTransform.anchorMin = Vector2.zero; // Bottom-left corner
        rectTransform.anchorMax = Vector2.zero; // Bottom-left corner
        rectTransform.pivot = new Vector2(0.5f, 0.5f);     // Set pivot to center
        rectTransform.sizeDelta = new Vector2(_uiSize, _uiSize);

        uiImageRectTransform = image.GetComponent<RectTransform>();

        UpdateTrackedObject();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if ((_updateMode & UpdateMode.FixedUpdate) != 0)
            UpdateTrackedObject();
    }

    private void Update()
    {
        if ((_updateMode & UpdateMode.Update) != 0)
            UpdateTrackedObject();
    }

    private void LateUpdate()
    {
        if ((_updateMode & UpdateMode.LateUpdate) != 0)
            UpdateTrackedObject();
    }

    private void OnDestroy()
    {
        if (image)
        {
            Destroy(image.gameObject);
        }
    }

    void UpdateTrackedObject()
    {
        if (uiImageRectTransform == null) return;

        if (mainCamera != null)
        {
            // Convert the 3D position of the source to screen space
            Vector3 screenPos = mainCamera.WorldToScreenPoint(transform.position);

            // Check if the object is in front of the camera
            if (screenPos.z > 0)
            {
                float scaleFactor = mainCanvas.scaleFactor;
                Vector2 adjustedPosition = new Vector2(screenPos.x / scaleFactor, screenPos.y / scaleFactor);

                uiImageRectTransform.anchoredPosition = adjustedPosition;

                image.enabled = GetVisibility();
            }
            else
            {
                image.enabled = false;
            }
        }
        else
        {
            image.enabled = false;
        }
    }

    protected virtual bool GetVisibility()
    {
        bool visibility = true;
        if (onVisibilityCallback != null)
        {
            // Iterate over each delegate in the invocation list
            foreach (OnVisibilityCallback callback in onVisibilityCallback.GetInvocationList())
            {
                // Perform logical AND; if any callback returns false, visibility becomes false
                visibility &= callback(this);
            }
        }

        return visibility;
    }
}

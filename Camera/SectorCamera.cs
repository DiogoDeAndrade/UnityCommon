using UnityEngine;
using System.Collections;

public class SectorCamera : MonoBehaviour
{
    [SerializeField] private Camera     mainCamera;
    [SerializeField] private Hypertag   followTag;
    [SerializeField] private float      scrollTime = 0.5f; 
    [SerializeField] private float      teleportThreshold = 2.0f; // Multiplier for teleporting

    private Vector2 cameraSize;
    private Renderer followTargetRenderer;
    private Bounds followTargetBounds;
    private Transform followTarget;
    private bool isMoving = false;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        CalculateCameraSize();
    }

    void Update()
    {
        FetchTarget();
        if (followTarget == null || isMoving) return;

        followTargetBounds = followTargetRenderer.bounds; // Always update bounds

        Vector3 camPosition = mainCamera.transform.position;

        if (!IsTargetFullyInView(followTargetBounds))
        {
            Vector3 newCamPosition = GetNewCameraPosition(followTargetBounds, camPosition);

            // Ensure this movement actually makes the object fully visible
            if (!IsTargetFullyInViewAfterMove(followTargetBounds, newCamPosition))
            {
                newCamPosition = AdjustFinalCameraPosition(newCamPosition, followTargetBounds);
            }

            float moveDistance = Vector3.Distance(camPosition, newCamPosition);
            float maxMoveDistance = cameraSize.x * teleportThreshold;

            if (moveDistance > maxMoveDistance)
            {
                mainCamera.transform.position = newCamPosition; // Teleport
            }
            else
            {
                StartCoroutine(SmoothMove(newCamPosition)); // Smooth movement
            }
        }
    }

    void FetchTarget()
    {
        if (followTarget == null)
        {
            followTarget = Hypertag.FindFirstObjectWithHypertag<Transform>(followTag);
            if (followTarget)
            {
                followTargetRenderer = followTarget.GetComponent<Renderer>();
                followTargetBounds = followTargetRenderer ? followTargetRenderer.bounds : new Bounds(followTarget.position, Vector3.zero);
            }
        }
    }

    void CalculateCameraSize()
    {
        float height = mainCamera.orthographicSize * 2;
        float width = height * mainCamera.aspect;
        cameraSize = new Vector2(width, height);
    }

    bool IsTargetFullyInView(Bounds targetBounds)
    {
        Vector3 camPos = mainCamera.transform.position;

        float left = camPos.x - cameraSize.x / 2;
        float right = camPos.x + cameraSize.x / 2;
        float bottom = camPos.y - cameraSize.y / 2;
        float top = camPos.y + cameraSize.y / 2;

        return targetBounds.min.x >= left && targetBounds.max.x <= right &&
               targetBounds.min.y >= bottom && targetBounds.max.y <= top;
    }

    Vector3 GetNewCameraPosition(Bounds targetBounds, Vector3 camPos)
    {
        Vector3 newCamPos = camPos;

        float targetWidth = targetBounds.size.x;
        float targetHeight = targetBounds.size.y;

        // Adjust the movement based on the object size to fully contain it
        if (targetBounds.max.x > camPos.x + cameraSize.x / 2) // Right exit
            newCamPos.x += cameraSize.x - targetWidth / 2;
        else if (targetBounds.min.x < camPos.x - cameraSize.x / 2) // Left exit
            newCamPos.x -= cameraSize.x - targetWidth / 2;

        if (targetBounds.max.y > camPos.y + cameraSize.y / 2) // Top exit
            newCamPos.y += cameraSize.y - targetHeight / 2;
        else if (targetBounds.min.y < camPos.y - cameraSize.y / 2) // Bottom exit
            newCamPos.y -= cameraSize.y - targetHeight / 2;

        return newCamPos;
    }

    // Ensure after moving, the target is actually fully inside the viewport
    bool IsTargetFullyInViewAfterMove(Bounds targetBounds, Vector3 newCamPos)
    {
        float left = newCamPos.x - cameraSize.x / 2;
        float right = newCamPos.x + cameraSize.x / 2;
        float bottom = newCamPos.y - cameraSize.y / 2;
        float top = newCamPos.y + cameraSize.y / 2;

        return targetBounds.min.x >= left && targetBounds.max.x <= right &&
               targetBounds.min.y >= bottom && targetBounds.max.y <= top;
    }

    // If the camera calculation wasn't perfect, adjust it
    Vector3 AdjustFinalCameraPosition(Vector3 newCamPos, Bounds targetBounds)
    {
        Vector3 adjustedPos = newCamPos;

        if (targetBounds.max.x > adjustedPos.x + cameraSize.x / 2) // Right exit
            adjustedPos.x += targetBounds.size.x / 2;
        else if (targetBounds.min.x < adjustedPos.x - cameraSize.x / 2) // Left exit
            adjustedPos.x -= targetBounds.size.x / 2;

        if (targetBounds.max.y > adjustedPos.y + cameraSize.y / 2) // Top exit
            adjustedPos.y += targetBounds.size.y / 2;
        else if (targetBounds.min.y < adjustedPos.y - cameraSize.y / 2) // Bottom exit
            adjustedPos.y -= targetBounds.size.y / 2;

        return adjustedPos;
    }

    IEnumerator SmoothMove(Vector3 targetPos)
    {
        isMoving = true;
        Vector3 start = mainCamera.transform.position;
        float elapsedTime = 0;
        float duration = scrollTime;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            t = 1 - Mathf.Pow(1 - t, 3); // Ease-out cubic function
            mainCamera.transform.position = Vector3.Lerp(start, targetPos, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = targetPos;
        isMoving = false;
    }
}

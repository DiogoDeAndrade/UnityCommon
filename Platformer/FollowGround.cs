using UnityEngine;

public class FollowGround : MonoBehaviour
{
    [SerializeField] private Transform targetObject;
    [SerializeField] private Transform frontProbe;
    [SerializeField] private Transform backProbe;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private float     probeDistance;

    void Update()
    {
        RaycastHit2D frontHit = new(), backHit = new();

        if (frontProbe)
        {
            frontHit = Physics2D.Raycast(frontProbe.position, Vector3.down, probeDistance, groundLayers);
        }
        if (backProbe)
        {
            backHit = Physics2D.Raycast(backProbe.position, Vector3.down, probeDistance, groundLayers);
        }

        if ((frontHit.collider) && (backHit.collider) && (targetObject))
        {
            Vector3 rightVector = frontHit.point - backHit.point;
            Vector3 upVector = new Vector2(-rightVector.y, rightVector.x);
            if (upVector.y < 0.0f) upVector = -upVector;
            if (transform != targetObject)
            {
                if (transform.right.x < 0.0f) upVector.x = -upVector.x;
            }

            targetObject.localRotation = Quaternion.LookRotation(Vector3.forward, upVector);
        }
    }

    private void OnDrawGizmosSelected()
    {
        RaycastHit2D frontHit = new(), backHit = new();

        if (frontProbe)
        {
            frontHit = Physics2D.Raycast(frontProbe.position, Vector3.down, probeDistance, groundLayers);
            if (frontHit.collider == null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(frontProbe.position, frontProbe.position + Vector3.down * probeDistance);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(frontProbe.position, frontHit.point);
            }
        }
        if (backProbe)
        {
            backHit = Physics2D.Raycast(backProbe.position, Vector3.down, probeDistance, groundLayers);
            if (backHit.collider == null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(backProbe.position, backProbe.position + Vector3.down * probeDistance);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(backProbe.position, backHit.point);
            }
        }

        if ((frontHit.collider) && (backHit.collider) && (targetObject))
        {
            Vector3 rightVector = frontHit.point - backHit.point;
            Vector3 upVector = new Vector2(-rightVector.y, rightVector.x);
            if (upVector.y < 0.0f) upVector = -upVector;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(targetObject.position, targetObject.position + upVector * probeDistance);
        }
    }
}

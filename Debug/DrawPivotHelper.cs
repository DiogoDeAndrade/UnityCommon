using UC;
using UnityEngine;

public class DrawPivotHelper : MonoBehaviour
{
    [SerializeField] private float length = 1.0f;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        DebugHelpers.DrawArrow(transform.position, transform.forward, length, 0.25f, 45.0f, transform.right);
        Gizmos.color = Color.green;
        DebugHelpers.DrawArrow(transform.position, transform.up, length, 0.25f, 45.0f, transform.right);
        Gizmos.color = Color.red;
        DebugHelpers.DrawArrow(transform.position, transform.right, length, 0.25f, 45.0f, transform.up);
    }
}

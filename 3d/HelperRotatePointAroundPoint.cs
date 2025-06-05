using UnityEngine;

public class HelperRotatePointAroundPoint : MonoBehaviour
{
    public enum UpdateMode { FixedUpdate, Update, LateUpdate, Manual };

    [SerializeField] UpdateMode updateMode = UpdateMode.Update;
    [SerializeField] Transform sourcePoint;
    [SerializeField] Transform destinationPoint;
    [SerializeField] Transform pivotPoint;
    [SerializeField] Quaternion rotation;

    private void Start()
    {
        if (destinationPoint == null) destinationPoint = transform;
    }

    void FixedUpdate()
    {
        if (updateMode == UpdateMode.FixedUpdate)
        {
            RotatePointAroundPoint();
        }
    }
    void Update()
    {
        if (updateMode == UpdateMode.Update)
        {
            RotatePointAroundPoint();
        }
    }

    void LateUpdate()
    {
        if (updateMode == UpdateMode.LateUpdate)
        {
            RotatePointAroundPoint();
        }
    }

    public void RotatePointAroundPoint()
    {
        // Fetch matrix of the source point (world space)
        var matrix = sourcePoint.localToWorldMatrix;
        // Convert matrix to pivot's local space
        matrix = pivotPoint.worldToLocalMatrix * matrix;
        // Rotate point
        matrix = Matrix4x4.Rotate(rotation) * matrix;
        // Convert back to world space
        matrix = pivotPoint.localToWorldMatrix * matrix;
        // Apply the new position to the source point
        destinationPoint.position = matrix.GetPosition();
        destinationPoint.rotation = matrix.rotation;
    }
}

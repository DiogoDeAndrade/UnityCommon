using UnityEngine;

public static class DebugHelpers
{
    public static void DrawArrow(Vector3 position, Vector3 dir, float length, float arrowHeadLength, float arrowHeadAngle)
    {
        DrawArrow(position, dir, length, arrowHeadLength, arrowHeadAngle, Vector3.up);
    }

    public static void DrawArrow(Vector3 position, Vector3 dir, float length, float arrowHeadLength, float arrowHeadAngle, Vector3 upVector)
    {
        if (length == 0) return;
        if (dir.magnitude < 1e-3) return;

        // Normalize the direction vector to get consistent scaling
        Vector3 normalizedDir = dir.normalized;

        // Calculate the end point of the arrow shaft
        Vector3 endPoint = position + normalizedDir * length;

        // Draw the main shaft of the arrow
        Gizmos.DrawLine(position, endPoint);

        Vector3 perpVector = Vector3.Cross(normalizedDir, upVector);

        Vector3 disp = -normalizedDir * arrowHeadLength;
        Vector3 arrowEnd1 = endPoint + perpVector * arrowHeadLength + disp;
        Vector3 arrowEnd2 = endPoint - perpVector * arrowHeadLength + disp;

        // Draw the arrowhead lines
        Gizmos.DrawLine(endPoint, arrowEnd1);
        Gizmos.DrawLine(endPoint, arrowEnd2);
    }
}

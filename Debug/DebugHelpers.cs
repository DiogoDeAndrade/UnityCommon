using System;
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

    static Material triangleMaterial;
    public static void DrawTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        if (triangleMaterial == null)
        {
            // Create a simple unlit material for GL
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            triangleMaterial = new Material(shader);
            triangleMaterial.hideFlags = HideFlags.HideAndDontSave;
            triangleMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            triangleMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            triangleMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            triangleMaterial.SetInt("_ZWrite", 0);
        }
        triangleMaterial.SetPass(0);

        GL.Begin(GL.TRIANGLES);
        GL.Color(Gizmos.color);

        GL.Vertex(p1);
        GL.Vertex(p2);
        GL.Vertex(p3);

        GL.End();
    }
}

using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{

    public static class DebugHelpers
    {
        public static void DrawArrow(Vector3 position, Vector3 dir, float length, float arrowHeadLength, float arrowHeadAngle)
        {
            DrawArrow(position, dir, length, arrowHeadLength, arrowHeadAngle, Vector3.up);
        }

        public static void DrawArrow(Vector3 position, Vector3 dir, float length, float arrowHeadLength, float arrowHeadAngle, Vector3 upVector)
        {
#if UNITY_EDITOR
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
#endif
        }

        static Material triangleMaterial;
#if UNITY_EDITOR
        static void SetTriangleMaterial()
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
        }
#endif

        public static void DrawTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
#if UNITY_EDITOR
            SetTriangleMaterial();

            GL.Begin(GL.TRIANGLES);
            GL.Color(Gizmos.color);

            GL.Vertex(p1);
            GL.Vertex(p2);
            GL.Vertex(p3);

            GL.End();
#endif
        }

        public static void DrawWireTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p1);
        }

        public static void DrawConvexPolygon(Vector3[] poly)
        {
#if UNITY_EDITOR
            SetTriangleMaterial();

            GL.Begin(GL.TRIANGLES);
            GL.Color(Gizmos.color);

            for (int i = 1; i < poly.Length - 1; i++)
            {
                GL.Vertex(poly[0]);
                GL.Vertex(poly[i]);
                GL.Vertex(poly[i + 1]);
            }

            GL.End();
#endif
        }

        public static void DrawConvexPolygon(Vector3[] vertices, int[] indices)
        {
#if UNITY_EDITOR
            SetTriangleMaterial();

            GL.Begin(GL.TRIANGLES);
            GL.Color(Gizmos.color);

            for (int i = 1; i < indices.Length - 1; i++)
            {
                GL.Vertex(vertices[indices[0]]);
                GL.Vertex(vertices[indices[i]]);
                GL.Vertex(vertices[indices[i + 1]]);
            }

            GL.End();
#endif
        }

        public static void DrawConvexPolygon(List<Vector3> vertices, List<int> indices)
        {
#if UNITY_EDITOR
            SetTriangleMaterial();

            GL.Begin(GL.TRIANGLES);
            GL.Color(Gizmos.color);

            for (int i = 1; i < indices.Count - 1; i++)
            {
                GL.Vertex(vertices[indices[0]]);
                GL.Vertex(vertices[indices[i]]);
                GL.Vertex(vertices[indices[i + 1]]);
            }

            GL.End();
#endif
        }

        public static void DrawWireConvexPolygon(Vector3[] poly)
        {
            for (int i = 0; i < poly.Length; i++)
            {
                Gizmos.DrawLine(poly[i], poly[(i + 1) % poly.Length]);
            }
        }

        public static void DrawWireConvexPolygon(Vector3[] vertices, int[] indices)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                Gizmos.DrawLine(vertices[indices[i]], vertices[indices[(i + 1) % indices.Length]]);
            }
        }

        public static void DrawWireConvexPolygon(List<Vector3> vertices, List<int> indices)
        {
            for (int i = 0; i < indices.Count; i++)
            {
                Gizmos.DrawLine(vertices[indices[i]], vertices[indices[(i + 1) % indices.Count]]);
            }
        }

        public static void DrawTextAt(Vector3 pos, Vector3 offset, int fontSize, Color color, string text, bool shadow = false, bool centerY = false)
        {
#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.fontSize = fontSize;
            if (centerY) style.alignment = TextAnchor.MiddleCenter;
            else style.alignment = TextAnchor.UpperCenter;

                // Convert the world position to screen space
                Vector3 screenPos = Camera.current.WorldToScreenPoint(pos);

            // Draw the label at the new world position
            if (shadow)
            {
                style.normal.textColor = Color.black;
                Vector3 shadowPos = Camera.current.ScreenToWorldPoint(screenPos + offset + new Vector3(1, 1, 0));

                Handles.Label(shadowPos, text, style);
            }

            // Draw the label at the new world position
            style.normal.textColor = color;
            Vector3 offsetPos = Camera.current.ScreenToWorldPoint(screenPos + offset);
            Handles.Label(offsetPos, text, style);
#endif
        }

        public static void DrawBox(Bounds r)
        {
            Gizmos.DrawLine(new Vector2(r.min.x, r.min.y), new Vector2(r.max.x, r.min.y));
            Gizmos.DrawLine(new Vector2(r.max.x, r.min.y), new Vector2(r.max.x, r.max.y));
            Gizmos.DrawLine(new Vector2(r.max.x, r.max.y), new Vector2(r.min.x, r.max.y));
            Gizmos.DrawLine(new Vector2(r.min.x, r.max.y), new Vector2(r.min.x, r.min.y));
        }

        public static void DrawHemisphere(Vector3 position, Vector3 right, Vector3 up, float radius, int divs = 20)
        {
            float angleInc = Mathf.PI / (float)(divs);

            Vector3 prevPos = position + radius * right;
            float angle = angleInc;
            for (int i = 0; i < divs; i++)
            {
                Vector3 p = position + radius * Mathf.Cos(angle) * right + radius * Mathf.Sin(angle) * up;
                Gizmos.DrawLine(prevPos, p);
                prevPos = p;
                angle += angleInc;
            }
            Gizmos.DrawLine(prevPos, position + radius * right);
        }
    }
}
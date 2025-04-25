using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UC
{

    public static class DebugHelpers
    {
        public static void DrawArrow(Vector3 position, Vector3 dir, float length, float arrowHeadLength, Vector3 perpVector)
        {
#if UNITY_EDITOR
            if (length == 0) return;
            if (dir.magnitude < 1e-3) return;

            // Calculate the end point of the arrow shaft
            Vector3 endPoint = position + dir * length;

            // Draw the main shaft of the arrow
            Gizmos.DrawLine(position, endPoint);

            Vector3 disp = -dir.normalized * arrowHeadLength;
            Vector3 arrowEnd1 = endPoint + perpVector * arrowHeadLength + disp;
            Vector3 arrowEnd2 = endPoint - perpVector * arrowHeadLength + disp;

            // Draw the arrowhead lines
            Gizmos.DrawLine(endPoint, arrowEnd1);
            Gizmos.DrawLine(endPoint, arrowEnd2);
#endif
        }

        static Material triangleMaterial;
        public static void DrawTriangle(Vector3 p1, Vector3 p2, Vector3 p3)
        {
#if UNITY_EDITOR
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
#endif
        }

        public static void DrawPolygon(Vector3[] poly)
        {
#if UNITY_EDITOR
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

            for (int i = 1; i < poly.Length - 1; i++)
            {
                GL.Vertex(poly[0]);
                GL.Vertex(poly[i]);
                GL.Vertex(poly[i + 1]);
            }

            GL.End();
#endif
        }


        public static void DrawTextAt(Vector3 pos, Vector3 offset, int fontSize, Color color, string text, bool shadow = false, bool centerY = false)
        {
#if UNITY_EDITOR
            GUIStyle style = new GUIStyle();
            style.fontSize = fontSize;
            if (centerY) style.alignment = TextAnchor.UpperCenter;

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
using NaughtyAttributes;
using System;
using UnityEngine;

namespace UC
{

    public class RectMeshGenerator : MeshGenerator
    {
        public enum FillMode { Normal, Hole };

        [SerializeField, Header("Rect Generator")]
        private FillMode fillMode = FillMode.Normal;
        [SerializeField, ShowIf(nameof(hasHole))]
        private Vector2 innerSize = new Vector2(50.0f, 50.0f);
        [SerializeField]
        private Vector2 size = new Vector2(100.0f, 100.0f);
        [SerializeField, Range(0, 100), HideIf(nameof(hasHole))]
        private int subdivisions = 0;
        [SerializeField]
        private float rotation;

        bool hasHole => (fillMode == FillMode.Hole);

        protected override bool colorModeSupported(ColorMode colorMode)
        {
            return true;
        }

        protected override void Build()
        {
            Mesh mesh = new Mesh();
            mesh.name = $"Generated rectangle";

            Vector3[] vertices = null;
            Vector2[] uvs = null;
            Color[] colors = null;
            int[] indices = null;

            // Half-size of the rectangle
            Vector2 halfSize = size * 0.5f;


            if (fillMode == FillMode.Normal)
            {
                float xInc = size.x / (subdivisions + 1);
                float yInc = size.y / (subdivisions + 1);

                int nVertex = (2 + subdivisions) * (2 + subdivisions);
                vertices = new Vector3[nVertex];
                uvs = new Vector2[nVertex];
                colors = new Color[nVertex];

                float y = -size.y * 0.5f;
                for (int yi = 0; yi < subdivisions + 2; yi++)
                {
                    float x = -size.x * 0.5f;
                    for (int xi = 0; xi < subdivisions + 2; xi++)
                    {
                        int index = yi * (subdivisions + 2) + xi;
                        vertices[index] = new Vector3(x, y, 0.0f);
                        uvs[index] = new Vector2((float)xi / (float)(subdivisions + 1), (float)yi / (float)(subdivisions + 1));

                        switch (colorMode)
                        {
                            case ColorMode.Single:
                                colors[index] = color.linear;
                                break;
                            case ColorMode.Outer:
                                {
                                    float d = GetRectSDF(halfSize, new Vector2(x, y));
                                    d = Mathf.Clamp01(-d / Mathf.Min(halfSize.x, halfSize.y));

                                    colors[index] = (outerColorOverCircle == null) ? (Color.white) : (outerColorOverCircle.EvaluateLinear(d));
                                }
                                break;
                            case ColorMode.OuterAndColor:
                                {
                                    float d = GetRectSDF(halfSize, new Vector2(x, y));
                                    d = Mathf.Clamp01(-d / Mathf.Min(halfSize.x, halfSize.y));

                                    var cInner = color.linear;
                                    var delta = new Vector2(x, y);
                                    delta.Normalize();
                                    float normalizedAngle = (colorOffset + ((Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg) + 180.0f) / 360.0f) % 1.0f;
                                    var cOut = (outerColorOverCircle == null) ? (Color.white) : (outerColorOverCircle.EvaluateLinear(normalizedAngle));

                                    colors[index] = Color.Lerp(cOut, cInner, d);
                                }
                                break;
                            case ColorMode.OuterAndInner:
                                {
                                    float d = GetRectSDF(halfSize, new Vector2(x, y));
                                    d = Mathf.Clamp01(-d / Mathf.Min(halfSize.x, halfSize.y));

                                    var delta = new Vector2(x, y);
                                    delta.Normalize();
                                    float normalizedAngle = (colorOffset + ((Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg) + 180.0f) / 360.0f) % 1.0f;
                                    var cInner = (innerColorOverCircle == null) ? (Color.white) : (innerColorOverCircle.EvaluateLinear(normalizedAngle));
                                    var cOut = (outerColorOverCircle == null) ? (Color.white) : (outerColorOverCircle.EvaluateLinear(normalizedAngle));

                                    colors[index] = Color.Lerp(cOut, cInner, d);
                                }
                                break;
                            case ColorMode.GradientX:
                                {
                                    float t = (float)xi / (float)(subdivisions + 1);

                                    colors[index] = (gradient == null) ? (Color.white) : (gradient.EvaluateLinear(t));
                                }
                                break;
                            case ColorMode.GradientY:
                                {
                                    float t = (float)yi / (float)(subdivisions + 1);

                                    colors[index] = (gradient == null) ? (Color.white) : (gradient.EvaluateLinear(t));
                                }
                                break;
                            default:
                                break;
                        }

                        x += xInc;
                    }
                    y += yInc;
                }

                int nIndices = ((subdivisions + 1) * (subdivisions + 1) * 2) * 3;
                indices = new int[nIndices];

                for (int i = 0; i < subdivisions + 1; i++)
                {
                    for (int j = 0; j < subdivisions + 1; j++)
                    {
                        int index = (i * (subdivisions + 1) + j) * 6;

                        indices[index] = i * (subdivisions + 2) + j;
                        indices[index + 1] = (i + 1) * (subdivisions + 2) + (j + 1);
                        indices[index + 2] = i * (subdivisions + 2) + (j + 1);

                        indices[index + 3] = i * (subdivisions + 2) + j;
                        indices[index + 4] = (i + 1) * (subdivisions + 2) + j;
                        indices[index + 5] = (i + 1) * (subdivisions + 2) + (j + 1);
                    }
                }
            }
            else
            {
                var innerHalfSize = innerSize * 0.5f;

                vertices = new Vector3[8];
                uvs = new Vector2[8];

                colors = new Color[8];

                float ui = innerHalfSize.x / size.x;
                float vi = innerHalfSize.y / size.y;

                vertices[0] = new Vector3(-halfSize.x, -halfSize.y, 0.0f); uvs[0] = new Vector2(0.0f, 0.0f);
                vertices[1] = new Vector3(halfSize.x, -halfSize.y, 0.0f); uvs[1] = new Vector2(1.0f, 0.0f);
                vertices[2] = new Vector3(halfSize.x, halfSize.y, 0.0f); uvs[2] = new Vector2(1.0f, 1.0f);
                vertices[3] = new Vector3(-halfSize.x, halfSize.y, 0.0f); uvs[3] = new Vector2(0.0f, 1.0f);
                vertices[4] = new Vector3(-innerHalfSize.x, -innerHalfSize.y, 0.0f); uvs[4] = new Vector2(0.5f - ui, 0.5f - vi);
                vertices[5] = new Vector3(innerHalfSize.x, -innerHalfSize.y, 0.0f); uvs[5] = new Vector2(0.5f + ui, 0.5f - vi);
                vertices[6] = new Vector3(innerHalfSize.x, innerHalfSize.y, 0.0f); uvs[6] = new Vector2(0.5f + ui, 0.5f + vi);
                vertices[7] = new Vector3(-innerHalfSize.x, innerHalfSize.y, 0.0f); uvs[7] = new Vector2(0.5f - ui, 0.5f + vi);

                switch (colorMode)
                {
                    case ColorMode.Single:
                        for (int i = 0; i < 8; i++) colors[i] = color;
                        break;
                    case ColorMode.Outer:
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                float d1 = GetRectSDF(halfSize, vertices[i]);
                                float d2 = GetRectSDF(innerHalfSize, vertices[i]);

                                float d = Mathf.Clamp01(-d1 / Mathf.Min(halfSize.x - innerHalfSize.x, halfSize.y - innerHalfSize.y));

                                colors[i] = (outerColorOverCircle == null) ? (Color.white) : (outerColorOverCircle.EvaluateLinear(d));
                            }
                        }
                        break;
                    case ColorMode.OuterAndColor:
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                float d1 = GetRectSDF(halfSize, vertices[i]);
                                float d2 = GetRectSDF(innerHalfSize, vertices[i]);
                                float d = Mathf.Clamp01(-d1 / Mathf.Min(halfSize.x - innerHalfSize.x, halfSize.y - innerHalfSize.y));

                                var innerColor = color.linear;
                                var delta = vertices[i].normalized;
                                float normalizedAngle = (colorOffset + ((Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg) + 180.0f) / 360.0f) % 1.0f;
                                var outColor = (outerColorOverCircle == null) ? (Color.white) : (outerColorOverCircle.EvaluateLinear(normalizedAngle));

                                colors[i] = Color.Lerp(outColor, innerColor, d);
                            }
                        }
                        break;
                    case ColorMode.OuterAndInner:
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                float d1 = GetRectSDF(halfSize, vertices[i]);
                                float d2 = GetRectSDF(innerHalfSize, vertices[i]);
                                float d = Mathf.Clamp01(-d1 / Mathf.Min(halfSize.x - innerHalfSize.x, halfSize.y - innerHalfSize.y));

                                var delta = vertices[i].normalized;
                                float normalizedAngle = (colorOffset + ((Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg) + 180.0f) / 360.0f) % 1.0f;
                                var innerColor = (innerColorOverCircle == null) ? (Color.white) : (innerColorOverCircle.EvaluateLinear(normalizedAngle));
                                var outColor = (outerColorOverCircle == null) ? (Color.white) : (outerColorOverCircle.EvaluateLinear(normalizedAngle));

                                colors[i] = Color.Lerp(outColor, innerColor, d);
                            }
                        }
                        break;
                    case ColorMode.GradientX:
                        for (int i = 0; i < 8; i++)
                        {
                            float t = (vertices[i].x + halfSize.x) / size.x;

                            colors[i] = (gradient == null) ? (Color.white) : (gradient.EvaluateLinear(t));
                        }
                        break;
                    case ColorMode.GradientY:
                        for (int i = 0; i < 8; i++)
                        {
                            float t = (vertices[i].y + halfSize.y) / size.y;

                            colors[i] = (gradient == null) ? (Color.white) : (gradient.EvaluateLinear(t));
                        }
                        break;
                    default:
                        break;
                }

                indices = new int[8 * 3] { 0, 4, 1, 1, 4, 5, 1, 5, 2, 5, 6, 2, 6, 3, 2, 6, 7, 3, 0, 3, 7, 0, 7, 4 };
            }

            if (colorSpace == ColorSpace.Gamma)
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = colors[i].gamma;
                }
            }

            if (Mathf.Abs(rotation) > 1e-3)
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    vertices[i] = vertices[i].RotateZ(rotation);
                }
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.UploadMeshData(true);

            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.mesh = mesh;
            }
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material = material;
            }
            var skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer)
            {
                skinnedMeshRenderer.sharedMesh = mesh;
                skinnedMeshRenderer.material = material;
            }
        }

        public static float GetRectSDF(Vector2 halfSize, Vector2 point)
        {
            // Compute the distance from the point to the rectangle bounds
            Vector2 d = new Vector2(
                Mathf.Abs(point.x) - halfSize.x,
                Mathf.Abs(point.y) - halfSize.y
            );

            // Compute the signed distance
            float outsideDistance = Vector2.Max(d, Vector2.zero).magnitude;
            float insideDistance = Mathf.Min(Mathf.Max(d.x, d.y), 0.0f);

            return outsideDistance + insideDistance;
        }
    }
}
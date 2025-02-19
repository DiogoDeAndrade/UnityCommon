using NaughtyAttributes;
using UnityEngine;

public class RadialMeshGenerator : MeshGenerator
{
    public enum FillMode { Normal, Hole, NormalOverCircle, HoleOverCircle };

    [SerializeField, Header("Radial Generator")]
    private FillMode       fillMode = FillMode.Normal;
    [SerializeField, ShowIf(nameof(hasHole))] 
    private float          innerRadius = 0.0f;
    [SerializeField] 
    private float          outerRadius = 10.0f;
    [SerializeField, Range(3, 100)] 
    private int            subdivisions = 3;
    [SerializeField] 
    private float          angularOffset;
    [SerializeField, ShowIf(nameof(hasHoleAndOverCircle))]
    private AnimationCurve  innerRadiusOverCircle;
    [SerializeField, ShowIf(nameof(isOverCircle))]
    private AnimationCurve  outerRadiusOverCircle;

    bool hasHole => (fillMode == FillMode.Hole) || (fillMode == FillMode.HoleOverCircle);
    bool hasHoleAndOverCircle => (fillMode == FillMode.HoleOverCircle);
    bool isOverCircle => (fillMode == FillMode.HoleOverCircle) || (fillMode == FillMode.NormalOverCircle);
    
    protected override void Build()
    {
        Mesh mesh = new Mesh();
        mesh.name = $"Generated {subdivisions}-gon";

        float ang = angularOffset * Mathf.Deg2Rad;
        float angInc = Mathf.PI * 2.0f / subdivisions;
        float t = 0.0f;
        float tInc = 1.0f / subdivisions;
        float tOffset = colorOffset;        

        Vector3[] vertices = new Vector3[subdivisions * 2];
        Color[]   colors = new Color[subdivisions * 2];        

        for (int i = 0; i < subdivisions; i++)
        {
            float s = Mathf.Sin(ang);
            float c = Mathf.Cos(ang);

            float r1 = 1.0f; 
            float r2 = 1.0f;

            if (isOverCircle)
            {
                r1 = (innerRadiusOverCircle != null) ? (innerRadiusOverCircle.Evaluate(t)) : 0.0f;
                r2 = (outerRadiusOverCircle != null) ? (outerRadiusOverCircle.Evaluate(t)) : 1.0f;
            }

            switch (fillMode)
            {
                case FillMode.Normal:
                    vertices[i * 2] = new Vector3(0.0f, 0.0f, 0.0f);
                    vertices[i * 2 + 1] = new Vector3(outerRadius * s, outerRadius * c, 0.0f);
                    break;
                case FillMode.Hole:
                    vertices[i * 2] = new Vector3(innerRadius * r1 * s, innerRadius * r1 * c, 0.0f);
                    vertices[i * 2 + 1] = new Vector3(outerRadius * r2 * s, outerRadius * r2 * c, 0.0f);
                    break;
                case FillMode.NormalOverCircle:
                    vertices[i * 2] = new Vector3(0.0f, 0.0f, 0.0f);
                    vertices[i * 2 + 1] = new Vector3(outerRadius * r2 * s, outerRadius * r2 * c, 0.0f);
                    break;
                case FillMode.HoleOverCircle:
                    vertices[i * 2] = new Vector3(innerRadius * r1 * s, innerRadius * r1 * c, 0.0f);
                    vertices[i * 2 + 1] = new Vector3(outerRadius * r2 * s, outerRadius * r2 * c, 0.0f);
                    break;
                default:
                    break;
            }

            float tt = (1.0f + t + tOffset) % 1.0f;
            switch (colorMode)
            {
                case ColorMode.Single:
                    colors[i * 2] = colors[i * 2 + 1] = color.linear;
                    break;
                case ColorMode.Outer:
                    colors[i * 2] = colors[i * 2 + 1] = (outerColorOverCircle != null) ? (outerColorOverCircle.EvaluateLinear(tt)) : (Color.white);
                    break;
                case ColorMode.OuterAndColor:
                    colors[i * 2] = color.linear;
                    colors[i * 2 + 1] = (outerColorOverCircle != null) ? (outerColorOverCircle.EvaluateLinear(tt)) : (Color.white);
                    break;
                case ColorMode.OuterAndInner:
                    colors[i * 2] = (innerColorOverCircle != null) ? (innerColorOverCircle.EvaluateLinear(tt)) : (Color.white);
                    colors[i * 2 + 1] = (outerColorOverCircle != null) ? (outerColorOverCircle.EvaluateLinear(tt)) : (Color.white);
                    break;
                case ColorMode.GradientX:
                    colors[i * 2] = gradient.EvaluateLinear(Mathf.Clamp01(0.5f + 0.5f * vertices[i * 2].x / outerRadius));
                    colors[i * 2 + 1] = gradient.EvaluateLinear(Mathf.Clamp01(0.5f + 0.5f * vertices[i * 2 + 1].x / outerRadius));
                    break;
                case ColorMode.GradientY:
                    colors[i * 2] = gradient.EvaluateLinear(Mathf.Clamp01(0.5f + 0.5f * -vertices[i * 2].y / outerRadius));
                    colors[i * 2 + 1] = gradient.EvaluateLinear(Mathf.Clamp01(0.5f + 0.5f * -vertices[i * 2 + 1].y / outerRadius));
                    break;
                default:
                    break;
            }

            ang += angInc;
            t += tInc;
        }

        int[] indices = new int[subdivisions * 2 * 3];
        for (int i = 0; i < subdivisions; i++)
        {
            indices[i * 6] = (i * 2) % vertices.Length;
            indices[i * 6 + 1] = (i * 2 + 1) % vertices.Length;
            indices[i * 6 + 2] = ((i + 1) * 2 + 1) % vertices.Length;

            indices[i * 6 + 3] = (i * 2) % vertices.Length;
            indices[i * 6 + 4] = ((i + 1) * 2 + 1) % vertices.Length;
            indices[i * 6 + 5] = ((i + 1) * 2) % vertices.Length;
        }

        if (colorSpace == ColorSpace.Gamma)
        {
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = colors[i].gamma;
            }
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = indices;
        mesh.UploadMeshData(true);

        var meshFilter= GetComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        var meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.material = material;
    }
}

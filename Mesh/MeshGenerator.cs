using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour
{
    public enum FillMode { Normal, Hole, NormalOverCircle, HoleOverCircle };
    public enum ColorMode { Single, Outer, OuterAndColor, OuterAndInner, GradientX, GradientY };

    [SerializeField]
    private FillMode       fillMode = FillMode.Normal;
    [SerializeField, ShowIf("hasHole")] 
    private float          innerRadius = 0.0f;
    [SerializeField] 
    private float          outerRadius = 10.0f;
    [SerializeField, Range(3, 100)] 
    private int            subdivions = 3;
    [SerializeField] 
    private float          offset;
    [SerializeField, ShowIf("hasHoleAndOverCircle")]
    private AnimationCurve  innerRadiusOverCircle;
    [SerializeField, ShowIf("isOverCircle")]
    private AnimationCurve  outerRadiusOverCircle;
    [SerializeField]
    private ColorMode       colorMode = ColorMode.Single;
    [SerializeField, Range(-1.0f, 1.0f), ShowIf("needColorOffset")]
    private float           colorOffset;
    [SerializeField, ShowIf("needsColor")]
    private Color           color = Color.white;
    [SerializeField, ShowIf("needInnerGradient")] 
    private Gradient        innerColorOverCircle;
    [SerializeField, ShowIf("needOuterGradient")] 
    private Gradient        outerColorOverCircle;
    [SerializeField, ShowIf("needGradient")]
    private Gradient        gradient;
    [SerializeField]
    private Material        material;

    bool needColorOffset => (colorMode == ColorMode.Outer) || (colorMode == ColorMode.OuterAndInner);
    bool needsColor => (colorMode == ColorMode.Single) || (colorMode == ColorMode.OuterAndColor);
    bool needOuterGradient => (colorMode == ColorMode.Outer) || (colorMode == ColorMode.OuterAndColor) || (colorMode == ColorMode.OuterAndInner);
    bool needInnerGradient => (colorMode == ColorMode.OuterAndInner);
    bool needGradient => (colorMode == ColorMode.GradientX) || (colorMode == ColorMode.GradientY);
    bool hasHole => (fillMode == FillMode.Hole) || (fillMode == FillMode.HoleOverCircle);
    bool hasHoleAndOverCircle => (fillMode == FillMode.HoleOverCircle);
    bool isOverCircle => (fillMode == FillMode.HoleOverCircle) || (fillMode == FillMode.NormalOverCircle);
    void Start()
    {
        Build();
    }

    [Button("Build")]
    void Build()
    {
        Mesh mesh = new Mesh();
        mesh.name = $"Generated {subdivions}-gon";

        float ang = offset * Mathf.Deg2Rad;
        float angInc = Mathf.PI * 2.0f / subdivions;
        float t = 0.0f;
        float tInc = 1.0f / subdivions;
        float tOffset = colorOffset;        

        Vector3[] vertices = new Vector3[subdivions * 2];
        Color[]   colors = new Color[subdivions * 2];        

        for (int i = 0; i < subdivions; i++)
        {
            float s = Mathf.Sin(ang);
            float c = Mathf.Cos(ang);

            float r1 = (innerRadiusOverCircle != null) ? (innerRadiusOverCircle.Evaluate(t)) : 0.0f;
            float r2 = (outerRadiusOverCircle != null) ? (outerRadiusOverCircle.Evaluate(t)) : 1.0f;

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
                    colors[i * 2] = colors[i * 2 + 1] = color;
                    break;
                case ColorMode.Outer:
                    colors[i * 2] = colors[i * 2 + 1] = (innerColorOverCircle != null) ? (innerColorOverCircle.Evaluate(tt)) : (Color.white);
                    break;
                case ColorMode.OuterAndColor:
                    colors[i * 2] = color;
                    colors[i * 2 + 1] = (outerColorOverCircle != null) ? (outerColorOverCircle.Evaluate(tt)) : (Color.white);
                    break;
                case ColorMode.OuterAndInner:
                    colors[i * 2] = (innerColorOverCircle != null) ? (innerColorOverCircle.Evaluate(tt)) : (Color.white);
                    colors[i * 2 + 1] = (outerColorOverCircle != null) ? (outerColorOverCircle.Evaluate(tt)) : (Color.white);
                    break;
                case ColorMode.GradientX:
                    colors[i * 2] = gradient.Evaluate(Mathf.Clamp01(0.5f + 0.5f * vertices[i * 2].x / outerRadius));
                    colors[i * 2 + 1] = gradient.Evaluate(Mathf.Clamp01(0.5f + 0.5f * vertices[i * 2 + 1].x / outerRadius));
                    break;
                case ColorMode.GradientY:
                    colors[i * 2] = gradient.Evaluate(Mathf.Clamp01(0.5f + 0.5f * -vertices[i * 2].y / outerRadius));
                    colors[i * 2 + 1] = gradient.Evaluate(Mathf.Clamp01(0.5f + 0.5f * -vertices[i * 2 + 1].y / outerRadius));
                    break;
                default:
                    break;
            }

            ang += angInc;
            t += tInc;
        }

        int[] indices = new int[subdivions * 2 * 3];
        for (int i = 0; i < subdivions; i++)
        {
            indices[i * 6] = (i * 2) % vertices.Length;
            indices[i * 6 + 1] = ((i + 1) * 2 + 1) % vertices.Length;
            indices[i * 6 + 2] = (i * 2 + 1) % vertices.Length;

            indices[i * 6 + 3] = (i * 2) % vertices.Length;
            indices[i * 6 + 4] = ((i + 1) * 2) % vertices.Length;
            indices[i * 6 + 5] = ((i + 1) * 2 + 1) % vertices.Length;
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

    /*private void OnValidate()
    {
        Build();
    }//*/
}

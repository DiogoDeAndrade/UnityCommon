using NaughtyAttributes;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MeshGenerator : MonoBehaviour
{
    [SerializeField] 
    private float          innerRadius = 0.0f;
    [SerializeField] 
    private float          outerRadius = 10.0f;
    [SerializeField, Range(3, 100)] 
    private int            subdivions = 3;
    [SerializeField] 
    private float          offset;
    [SerializeField] 
    private AnimationCurve innerRadiusOverCircle;
    [SerializeField]
    private AnimationCurve outerRadiusOverCircle;
    [SerializeField] 
    private Gradient       innerColorOverCircle;
    [SerializeField] 
    private Gradient       outerColorOverCircle;
    [SerializeField]
    private Material       material; 

    void Start()
    {
        Build();
    }

    [Button("Build")]
    void Build()
    {
        Mesh mesh = new Mesh();

        float ang = offset * Mathf.Deg2Rad;
        float angInc = Mathf.PI * 2.0f / subdivions;
        float t = 0.0f;
        float tInc = 1.0f / subdivions;

        Vector3[] vertices = new Vector3[subdivions * 2];
        Color[]   colors = new Color[subdivions * 2];        

        for (int i = 0; i < subdivions; i++)
        {
            float s = Mathf.Sin(ang);
            float c = Mathf.Cos(ang);

            float r1 = (innerRadiusOverCircle != null) ? (innerRadiusOverCircle.Evaluate(t)) : 0.0f;
            float r2 = (outerRadiusOverCircle != null) ? (outerRadiusOverCircle.Evaluate(t)) : 1.0f;

            vertices[i * 2] = new Vector3(innerRadius * r1 * s, innerRadius * r1 * c, 0.0f);
            vertices[i * 2 + 1] = new Vector3(outerRadius * r2 * s, outerRadius * r2 * c, 0.0f);

            colors[i * 2] = (innerColorOverCircle != null) ? (innerColorOverCircle.Evaluate(t)) : (Color.white);
            colors[i * 2 + 1] = (outerColorOverCircle != null) ? (outerColorOverCircle.Evaluate(t)) : (Color.white);

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
}

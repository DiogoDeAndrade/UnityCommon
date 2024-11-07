using NaughtyAttributes;
using UnityEngine;

public class MeshOctreeComponent : MonoBehaviour
{
    [SerializeField] private bool displayOctree;
    MeshOctree meshOctree;

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, ref Triangle hitInfo, ref float t)
    {
        return meshOctree.Raycast(origin, dir, maxDist, ref hitInfo, ref t);
    }
    public bool Linecast(Vector3 p1, Vector3 p2, ref Triangle hitInfo, ref float t)
    {
        Vector3 delta = p2 - p1;
        float maxDist = delta.magnitude;
        if (maxDist < 1e-3) return true;
        Vector3 dir = delta / maxDist;
        return meshOctree.Raycast(p1, dir, maxDist, ref hitInfo, ref t);
    }

    [Button("Build")]
    public void Build()
    {
        var meshRenderers = GetComponentsInChildren<MeshRenderer>();
        if (meshRenderers.Length == 0) return;

        var bounds = meshRenderers[0].bounds;
        for (int i = 1; i < meshRenderers.Length; i++)
        {
            bounds.Encapsulate(meshRenderers[i].bounds);
        }

        var meshFilters = GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return;

        meshOctree = new MeshOctree(bounds.min, bounds.max, 4);
        
        foreach (var meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            var matrix = meshFilter.transform.localToWorldMatrix;
            var vertices = mesh.vertices;
            vertices = matrix.TransformPositions(vertices);
            for (int submeshId = 0; submeshId < mesh.subMeshCount; submeshId++)
            {
                if (mesh.GetTopology(submeshId) != MeshTopology.Triangles)
                {
                    throw new System.Exception("Submeshes that aren't composed of triangles aren't supported!");                    
                }
                var indices = mesh.GetTriangles(submeshId);

                for (int j = 0; j < indices.Length; j += 3)
                {
                    meshOctree.AddTriangle(new Triangle(vertices[indices[j]], vertices[indices[j + 1]], vertices[indices[j + 2]]));
                }
            }
        }
    }

    public void OnDrawGizmosSelected()
    {
        if ((meshOctree != null) && (displayOctree))
        {
            meshOctree.DrawGizmos(5);
        }
    }
}

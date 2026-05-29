using UnityEngine;

namespace UC
{

    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(VoxelObject))]
    public class VoxelRenderer : MonoBehaviour
    {
        public Material material;

        private bool ownsMaterial;
        private Mesh generatedMesh;

        VoxelObject voxelObject;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;

        void Start()
        {
            UpdateMesh();
        }

        private void OnDestroy()
        {
            if ((ownsMaterial) && (material))
            {
                material.Delete();
                material = null;
                ownsMaterial = false;
            }

            DestroyGeneratedMesh();
        }

        private void DestroyGeneratedMesh()
        {
            if (!generatedMesh)
                return;

            if ((meshFilter) && (meshFilter.sharedMesh == generatedMesh))
                meshFilter.sharedMesh = null;

            generatedMesh.Delete();
            generatedMesh = null;
        }

        public void UpdateMesh()
        {
            EnsureComponents();

            DestroyGeneratedMesh();

            generatedMesh = voxelObject.GetMesh();
            meshFilter.sharedMesh = generatedMesh;

            if (material == null)
            {
                material = meshRenderer.sharedMaterial;

                if (material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (!shader)
                    {
                        Debug.LogWarning("Could not find URP Lit shader.");
                        return;
                    }

                    material = new Material(shader);
                    material.name = $"Generated Voxel Material ({name})";
                    material.SetFloat("_Cull", 1.0f);

                    ownsMaterial = true;
                    meshRenderer.sharedMaterial = material;
                }
            }
        }

        void EnsureComponents()
        {
            if (voxelObject == null)
            {
                voxelObject = GetComponent<VoxelObject>();
                meshFilter = GetComponent<MeshFilter>();
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        public void SetVoxelData(VoxelData<byte> voxelData)
        {
            EnsureComponents();

            voxelObject.voxelData = voxelData;

            UpdateMesh();
        }
    }
}
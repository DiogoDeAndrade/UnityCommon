using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(VoxelObject))]
public class VoxelRenderer : MonoBehaviour
{
    public Material material;

    VoxelObject     voxelObject;
    MeshFilter      meshFilter;
    MeshRenderer    meshRenderer;

    void Start()
    {
        UpdateMesh();
    }

    public void UpdateMesh()
    {
        if (voxelObject == null)
        {
            voxelObject = GetComponent<VoxelObject>();
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        meshFilter.mesh = voxelObject.GetMesh();

        if (material == null)
        {
            material = meshRenderer.sharedMaterial;

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader);
                material.SetFloat("_Cull", 1.0f);

                meshRenderer.material = material;
            }
        }
    }

    public void SetVoxelData(VoxelData voxelData)
    {
        if (voxelObject == null)
        {
            voxelObject = GetComponent<VoxelObject>();
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        voxelObject.voxelData = voxelData;
        
        UpdateMesh();
    }
}

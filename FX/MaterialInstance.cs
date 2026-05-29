using NaughtyAttributes;
using System;
using UC;
using UnityEngine;
using UnityEngine.UIElements;

public class MaterialInstance : MonoBehaviour
{
    [Flags]
    public enum Overrides { Shader = 1 };

    [SerializeField] private int        materialIndex = 0;
    [SerializeField] private Overrides  overrides;
    [SerializeField, ShowIf(nameof(isOverrideShader))] private Shader shader;

    protected Material material;
    protected Material sourceMaterial;

    bool isOverrideShader => (overrides & Overrides.Shader) != 0;

    void Awake()
    {
        Initialize();
    }
    protected virtual void UpdateMaterial()
    {

    }

    private void OnDestroy()
    {
        DestroyMaterialInstance(GetComponent<Renderer>());
    }

    private void DestroyMaterialInstance(Renderer renderer)
    {
        if ((sourceMaterial) && (material))
        {
            material.Delete();

            var materials = renderer.sharedMaterials;
            materials[materialIndex] = sourceMaterial;
            renderer.sharedMaterials = materials;
        }
    }

    [Button("Force Init")]
    void Initialize()
    {
        Renderer renderer = GetComponent<Renderer>();
        DestroyMaterialInstance(renderer);

        var materials = renderer.sharedMaterials;
        sourceMaterial = materials[materialIndex];

        material = new Material(sourceMaterial);
        material.name = $"Generated Material Instance";
        if (isOverrideShader) material.shader = shader;
        UpdateMaterial();

        materials[materialIndex] = material;

        renderer.sharedMaterials = materials;
    }
}

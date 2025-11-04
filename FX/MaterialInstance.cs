using UnityEngine;
using NaughtyAttributes;
using System;

public class MaterialInstance : MonoBehaviour
{
    [Flags]
    public enum Overrides { Shader = 1 };

    [SerializeField] private int        materialIndex = 0;
    [SerializeField] private Overrides  overrides;
    [SerializeField, ShowIf(nameof(isOverrideShader))] private Shader shader;

    protected Material material;

    bool isOverrideShader => (overrides & Overrides.Shader) != 0;

    void Awake()
    {
        Initialize();
    }
    protected virtual void UpdateMaterial()
    {

    }

    [Button("Force Init")]
    void Initialize()
    {
        Renderer renderer = GetComponent<Renderer>();

        var materials = renderer.sharedMaterials;
        material = materials[materialIndex];

        material = new Material(material);
        material.name = $"Generated Material Instance";
        if (isOverrideShader) material.shader = shader;
        UpdateMaterial();

        materials[materialIndex] = material;

        renderer.materials = materials;
    }
}

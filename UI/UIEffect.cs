using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using System;

public class UIImageEffect : MonoBehaviour
{
    [Flags]
    public enum Effects { None = 0, ColorRemap = 1, Inverse = 2 };

    [SerializeField] 
    private Effects      effects;
    [SerializeField, ShowIf(nameof(colorRemapEnable))] 
    private ColorPalette palette;
    [SerializeField, ShowIf(nameof(inverseEnable))] 
    private float        inverseFactor;

    public bool colorRemapEnable => (effects & Effects.ColorRemap) != 0;
    public bool inverseEnable => (effects & Effects.Inverse) != 0;

    private Image           uiImage;
    private RawImage        rawImage;
    private Material        material;

    private void Start()
    {
        SetupMaterials();
    }

    public void SetRemap(ColorPalette colorPalette)
    {
        palette = colorPalette;
    }

    private void Update()
    {
        ConfigureMaterial();
    }

    [Button("Update Material")]
    private void SetupMaterials()
    {
        uiImage = GetComponent<Image>();
        rawImage = GetComponent<RawImage>();
        material = (uiImage) ? (uiImage.material) : (rawImage.material);
        material = new Material(material);
        if (uiImage) uiImage.material = material;
        else if (rawImage) rawImage.material = material;

        palette?.RefreshCache();

        ConfigureMaterial();
    }

    private void ConfigureMaterial()
    {
        if ((palette) && (colorRemapEnable))
        {
            var texture = palette.GetTexture(ColorPalette.TextureLayoutMode.Horizontal, 4);
            material.SetTexture("_Colormap", texture);
            material.SetFloat("_EnableRemap", 1.0f);
        }
        else
        {
            material.SetFloat("_EnableRemap", 0.0f);
        }

        if (inverseEnable)
        {
            material.SetFloat("_InverseFactor", inverseFactor);
        }
        else
        {
            material.SetFloat("_InverseFactor", 0.0f);
        }
    }
}

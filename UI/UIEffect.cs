using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;
using System;

[ExecuteAlways]
public class UIImageEffect : MonoBehaviour
{
    [Flags]
    public enum Effects { None = 0, ColorRemap = 1, Inverse = 2, ColorFlash = 4, Outline = 8 };

    [SerializeField] 
    private Effects      effects;
    [SerializeField, ShowIf(nameof(colorRemapEnable))] 
    private ColorPalette palette;
    [SerializeField, ShowIf(nameof(inverseEnable)), Range(0.0f, 1.0f)] 
    private float        inverseFactor;
    [SerializeField, ShowIf(nameof(flashEnable))]
    private Color flashColor = new Color(1.0f, 1.0f, 1.0f, 0.0f);
    [SerializeField, ShowIf(nameof(outlineEnable))]
    private float outlineWidth = 1.0f;
    [SerializeField, ShowIf(nameof(outlineEnable))]
    private Color outlineColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

    public bool colorRemapEnable => (effects & Effects.ColorRemap) != 0;
    public bool inverseEnable => (effects & Effects.Inverse) != 0;
    public bool flashEnable => (effects & Effects.ColorFlash) != 0;
    public bool outlineEnable => (effects & Effects.Outline) != 0;

    private Image                       uiImage;
    private RawImage                    rawImage;
    private Material                    material;
    private SecondarySpriteTexture[]    otherTextures = new SecondarySpriteTexture[16];

    private void Start()
    {
        SetupMaterials();
    }

    public void SetRemap(ColorPalette colorPalette)
    {
        palette = colorPalette;
        if (material)
        {
            material.name = palette.name + "_Material";
        }
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
        if (palette)
        {
            material.name = palette.name + "_Material";
        }
        else
        {
            material.name = "Material" + UnityEngine.Random.Range(0, 1000000);
        }
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

            // Stupid hack required by the UI renderer, because it doesn't process secondary textures
            if (uiImage)
            {
                var sprite = uiImage.sprite;
                if (sprite)
                {
                    int count = sprite.GetSecondaryTextures(otherTextures);
                    for (int i = 0; i < count; i++)
                    {
                        material.SetTexture(otherTextures[i].name, otherTextures[i].texture);
                    }
                }
                else
                {
                    material.SetTexture("_EffectTexture", null);
                }
            }
            else
            {
                material.SetTexture("_PaletteTexture", null);
            }
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
        if (flashEnable)
        {
            material.SetColor("_FlashColor", flashColor);
        }
        else
        {
            material.SetColor("_FlashColor", flashColor.ChangeAlpha(0));
        }

        if (outlineEnable)
        {
            Vector2 texelSize = Vector2.zero;
            if ((uiImage) && (uiImage.sprite) && (uiImage.sprite.texture))
            {
                var texture = uiImage.sprite.texture;
                texelSize = new Vector2(1.0f / texture.width, 1.0f / texture.height);
            }

            material.SetColor("_OutlineColor", outlineColor);
            material.SetFloat("_OutlineWidth", outlineWidth);
            material.SetVector("_OutlineTexelSize", texelSize);
            material.SetFloat("_OutlineEnable", 1.0f);
        }
        else
        {
            material.SetFloat("_OutlineEnable", 0.0f);
        }

    }
}

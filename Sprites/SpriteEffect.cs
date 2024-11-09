using UnityEngine;
using NaughtyAttributes;
using System;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffect : MonoBehaviour
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

    private MaterialPropertyBlock   mpb;
    private SpriteRenderer          spriteRenderer;
    private bool                    dirty = true;

    private void OnEnable()
    {
        if (mpb == null) mpb = new();

        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.GetPropertyBlock(mpb);

        ConfigureMaterial();
    }

    public void SetRemap(ColorPalette colorPalette)
    {
        palette = colorPalette;
        dirty = true;
    }

    private void Update()
    {
        if (dirty)
        {
            ConfigureMaterial();
        }
    }

    [Button("Update Material")]
    private void ConfigureMaterial()
    {
        if ((palette) && (colorRemapEnable))
        {
            var texture = palette.GetTexture(ColorPalette.TextureLayoutMode.Horizontal, 4);
            mpb.SetTexture("_Colormap", texture);
            mpb.SetFloat("_EnableRemap", 1.0f);
        }
        else
        {
            mpb.SetFloat("_EnableRemap", 0.0f);
        }

        if (inverseEnable)
        {
            mpb.SetFloat("_InverseFactor", inverseFactor);
        }
        else
        {
            mpb.SetFloat("_InverseFactor", 0.0f);
        }

        spriteRenderer.SetPropertyBlock(mpb);

        dirty = false;
    }
}

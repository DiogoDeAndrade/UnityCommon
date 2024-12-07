using UnityEngine;
using NaughtyAttributes;
using System;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteEffect : MonoBehaviour
{
    [Flags]
    public enum Effects { None = 0, ColorRemap = 1, Inverse = 2, ColorFlash = 4 };

    [SerializeField] 
    private Effects      effects;
    [SerializeField, ShowIf(nameof(colorRemapEnable))] 
    private ColorPalette palette;
    [SerializeField, ShowIf(nameof(inverseEnable))] 
    private float        inverseFactor;
    [SerializeField, ShowIf(nameof(flashEnable))]
    private Color        flashColor = new Color(1.0f, 1.0f, 1.0f, 0.0f);

    public bool colorRemapEnable => (effects & Effects.ColorRemap) != 0;
    public bool inverseEnable => (effects & Effects.Inverse) != 0;
    public bool flashEnable => (effects & Effects.ColorFlash) != 0;

    private MaterialPropertyBlock   mpb;
    private SpriteRenderer          spriteRenderer;

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
    }

    public ColorPalette GetRemap()
    {
        return palette;
    }

    public void SetInverseFactor(float factor)
    {
        inverseFactor = factor;

        if (factor > 0.0f) effects |= Effects.Inverse;
        else effects &= ~Effects.Inverse;
    }

    public float GetInverseFactor() => inverseFactor;
    public void SetFlashColor(Color color)
    {
        flashColor = color;

        if (color.a > 0.0f) effects |= Effects.ColorFlash;
        else effects &= ~Effects.ColorFlash;
    }

    public Color GetFlashColor() => flashColor;
    public float flashAlpha
    {
        get { return flashColor.a; }
        set { SetFlashColor(flashColor.ChangeAlpha(value)); }
    }

    private void Update()
    {
        ConfigureMaterial();
    }

    [Button("Update Material")]
    private void ConfigureMaterial()
    {
        spriteRenderer.GetPropertyBlock(mpb);

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

        if (flashEnable)
        {
            mpb.SetColor("_FlashColor", flashColor);
        }
        else
        {
            mpb.SetColor("_FlashColor", flashColor.ChangeAlpha(0));
        }

        spriteRenderer.SetPropertyBlock(mpb);
    }
}

public static class SpriteEffectExtensions
{
    public static Tweener.BaseInterpolator FlashInvert(this SpriteEffect spriteEffect, float duration)
    {
        spriteEffect.Tween().Stop("FlashInvert", Tweener.StopBehaviour.SkipToEnd);

        var current = spriteEffect.GetInverseFactor();

        return spriteEffect.Tween().Interpolate(0.0f, 1.0f, duration, (value) =>
        {
            if (value < 0.5f) spriteEffect.SetInverseFactor(value * 2.0f);
            else spriteEffect.SetInverseFactor(1.0f - (value - 0.5f) * 2.0f);
        }, "FlashInvert").Done(() => spriteEffect.SetInverseFactor(current));
    }

    public static Tweener.BaseInterpolator FlashColor(this SpriteEffect spriteEffect, float duration, Color color)
    {
        spriteEffect.Tween().Stop("FlashColor", Tweener.StopBehaviour.SkipToEnd);
        spriteEffect.SetFlashColor(color);

        return spriteEffect.Tween().Interpolate(0.0f, 1.0f, duration, (value) =>
        {
            spriteEffect.SetFlashColor(Color.Lerp(color, color.ChangeAlpha(0), value));
        }, "FlashColor").Done(() => spriteEffect.SetFlashColor(color.ChangeAlpha(0.0f)));
    }
}

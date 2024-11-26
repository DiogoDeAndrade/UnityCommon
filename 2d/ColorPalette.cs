using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ColorPalette", menuName = "Unity Common/Palette")]
public class ColorPalette : ScriptableObject
{
    public enum TextureLayoutMode { Horizontal };

    public enum SortMode
    {
        Hue,           // Sort by hue first
        Brightness,     // Sort by brightness first
        Temperature,   // Sort by color temperature (warm to cool)
        Saturation     // Sort by saturation first
    }


    public List<Color> colors;

    struct TextureCacheKey
    {
        public TextureLayoutMode    mode;
        public int                  sizePerItem;
    }

    private Dictionary<TextureCacheKey, Texture2D> textureCache;

    public int Count => colors.Count;

    public void Add(Color color)
    {
        if (colors == null) colors = new List<Color>();

        colors.Add(color);
    }

    public void SetColor(int index, Color color)
    {
        colors[index] = color;
    }

    public bool GetColor(Color pixel, float tolerance, bool useAlpha, ref Color color)
    {
        if (colors == null) return false;

        if (useAlpha)
        {
            foreach (var c in colors)
            {
                if (c.DistanceRGBA(pixel) < tolerance)
                {
                    color = c;
                    return true;
                }
            }
        }
        else
        {
            foreach (var c in colors)
            {
                if (c.DistanceRGB(pixel) < tolerance)
                {
                    color = c;
                    return true;
                }
            }
        }

        return false;
    }

    public void SortColors(SortMode mode = SortMode.Hue)
    {
        if (colors == null || colors.Count == 0) return;

        switch (mode)
        {
            case SortMode.Hue:
                colors.Sort((a, b) =>
                {
                    Color.RGBToHSV(a, out float h1, out float s1, out float v1);
                    Color.RGBToHSV(b, out float h2, out float s2, out float v2);

                    h1 = Mathf.Floor(h1 * 10) / 10.0f;
                    h2 = Mathf.Floor(h2 * 10) / 10.0f;
                    s1 = Mathf.Floor(s1 * 10) / 10.0f;
                    s2 = Mathf.Floor(s2 * 10) / 10.0f;
                    v1 = Mathf.Floor(v1 * 10) / 10.0f;
                    v2 = Mathf.Floor(v2 * 10) / 10.0f;

                    // Primary sort by hue
                    int hueCompare = h1.CompareTo(h2);
                    if (hueCompare != 0) return hueCompare;

                    // Secondary sort by saturation
                    int satCompare = s1.CompareTo(s2);
                    if (satCompare != 0) return satCompare;

                    // Tertiary sort by value (brightness)
                    return v1.CompareTo(v2);
                });
                break;

            case SortMode.Brightness:
                colors.Sort((a, b) =>
                {
                    Color.RGBToHSV(a, out float h1, out float s1, out float v1);
                    Color.RGBToHSV(b, out float h2, out float s2, out float v2);

                    h1 = Mathf.Floor(h1 * 10) / 10.0f;
                    h2 = Mathf.Floor(h2 * 10) / 10.0f;
                    s1 = Mathf.Floor(s1 * 10) / 10.0f;
                    s2 = Mathf.Floor(s2 * 10) / 10.0f;
                    v1 = Mathf.Floor(v1 * 10) / 10.0f;
                    v2 = Mathf.Floor(v2 * 10) / 10.0f;

                    // Primary sort by value (brightness)
                    int brightnessCompare = v1.CompareTo(v2);
                    if (brightnessCompare != 0) return brightnessCompare;

                    // Secondary sort by saturation
                    int satCompare = s1.CompareTo(s2);
                    if (satCompare != 0) return satCompare;

                    // Tertiary sort by hue
                    return h1.CompareTo(h2);
                });
                break;

            case SortMode.Temperature:
                colors.Sort((a, b) =>
                {
                    // Calculate color temperature (simplified)
                    // Warmer colors have more red, cooler colors have more blue
                    float tempA = a.r / (a.b + 0.01f);
                    float tempB = b.r / (b.b + 0.01f);
                    return tempB.CompareTo(tempA); // Sort from warm to cool
                });
                break;

            case SortMode.Saturation:
                colors.Sort((a, b) =>
                {
                    Color.RGBToHSV(a, out float h1, out float s1, out float v1);
                    Color.RGBToHSV(b, out float h2, out float s2, out float v2);

                    h1 = Mathf.Floor(h1 * 10) / 10.0f;
                    h2 = Mathf.Floor(h2 * 10) / 10.0f;
                    s1 = Mathf.Floor(s1 * 10) / 10.0f;
                    s2 = Mathf.Floor(s2 * 10) / 10.0f;
                    v1 = Mathf.Floor(v1 * 10) / 10.0f;
                    v2 = Mathf.Floor(v2 * 10) / 10.0f;

                    // Primary sort by saturation
                    int satCompare = s1.CompareTo(s2);
                    if (satCompare != 0) return satCompare;

                    // Secondary sort by hue
                    int hueCompare = h1.CompareTo(h2);
                    if (hueCompare != 0) return hueCompare;

                    // Tertiary sort by value
                    return v1.CompareTo(v2);
                });
                break;
        }
    }

    public int GetIndexClosestColorRGB(Color pixel)
    {
        int ret = 0;
        float minDist = pixel.DistanceRGB(colors[ret]);

        for (int i = 1; i < colors.Count; i++)
        {
            float d = colors[i].DistanceRGB(pixel);
            if (d < minDist)
            {
                minDist = d;
                ret = i;
            }
        }

        return ret;
    }

    public Texture2D GetTexture(TextureLayoutMode mode = TextureLayoutMode.Horizontal, int sizePerItem = 2)
    {
        if (textureCache != null)
        {
            if (textureCache.TryGetValue(new TextureCacheKey { mode = mode, sizePerItem = sizePerItem }, out Texture2D ret))
            {
                return ret;
            }
        }
        else
        {
            textureCache = new();
        }

        if (mode == TextureLayoutMode.Horizontal)
        {
            Texture2D texture = new Texture2D(sizePerItem * colors.Count, sizePerItem, TextureFormat.ARGB32, false);

            UpdateTexture(texture, mode, sizePerItem);
        
            textureCache.Add(new TextureCacheKey { mode = mode, sizePerItem = sizePerItem }, texture);

            return texture;
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public void UpdateTexture(Texture2D texture, TextureLayoutMode mode, int sizePerItem)
    {
        if (mode == TextureLayoutMode.Horizontal)
        {
            int pitch = sizePerItem * colors.Count;
            Color[] bitmap = new Color[pitch * sizePerItem];
            for (int i = 0; i < colors.Count; i++)
            {
                Color c = colors[i];
                for (int y = 0; y < sizePerItem; y++)
                {
                    int index = (i * sizePerItem) + y * pitch;
                    for (int x = 0; x < sizePerItem; x++)
                    {
                        bitmap[index] = c;
                        index++;
                    }
                }
            }

            texture.SetPixels(bitmap);
            texture.Apply();
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public void RefreshCache()
    {
        if (textureCache != null)
        {
            foreach ((var key, var value) in textureCache)
            {
                UpdateTexture(value, key.mode, key.sizePerItem);
            }
        }
    }

    public ColorPalette Clone()
    {
        var ret = ScriptableObject.CreateInstance<ColorPalette>();
        ret.colors = new List<Color>(colors);

        return ret;
    }


    [Button("Sort by Hue")]
    void SortByHue() { SortColors(SortMode.Hue); }

    [Button("Sort by Brightness")]
    void SortByBrightness() { SortColors(SortMode.Brightness); }

    [Button("Sort by Saturation")]
    void SortBySaturation() { SortColors(SortMode.Saturation); }

    [Button("Sort by Temperature")]
    void SortByTemperature() { SortColors(SortMode.Temperature); }
}

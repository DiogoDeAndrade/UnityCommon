using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bitmap
{
    public Color[] data;
    public int     width;
    public int     height;

    public Bitmap(int width, int height)
    {
        this.width = width;
        this.height = height;
        data = new Color[width * height];
    }

    public void Fill(Color color)
    {
        for (int i = 0; i < data.Length; i++) data[i] = color;
    }

    public void Rect(int x, int y, int sx, int sy, Color color)
    {
        for (int dy = Mathf.Max(y, 0); dy < Mathf.Min(y + sy, height); dy++)
        {
            for (int dx = Mathf.Max(x, 0); dx < Mathf.Min(x + sx, width); dx++)
            {
                data[dy * width + dx] = color;
            }
        }
    }
    public void RectAlpha(int x, int y, int sx, int sy, Color color)
    {
        for (int dy = Mathf.Max(y, 0); dy < Mathf.Min(y + sy, height); dy++)
        {
            for (int dx = Mathf.Max(x, 0); dx < Mathf.Min(x + sx, width); dx++)
            {
                data[dy * width + dx] = color * color.a + data[dy * width + dx] * (1.0f - color.a);
            }
        }
    }

    public Texture2D ToTexture(string name = "", FilterMode filter = FilterMode.Bilinear, TextureWrapMode wrapU = TextureWrapMode.Repeat, TextureWrapMode wrapV = TextureWrapMode.Repeat)
    {
        var ret = new Texture2D(width, height, TextureFormat.ARGB32, false);

        if (name != "") ret.name = name;
        ret.filterMode = filter;
        ret.wrapModeU = wrapU;
        ret.wrapModeV = wrapV;
        ret.SetPixels(data);
        ret.Apply(true, true);

        return ret;
    }
}

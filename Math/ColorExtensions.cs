using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static public class ColorExtensions
{
    public static Color ChangeAlpha(this Color c, float a)
    {
        return new Color(c.r, c.g, c.b, a);
    }
};


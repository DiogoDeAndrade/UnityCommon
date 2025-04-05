using UnityEngine;

namespace UC
{

    public static class GradientExtensions
    {
        public static void FromColor(this Gradient g, Color c)
        {
            GradientColorKey[] colorKeys = new GradientColorKey[2]
            {
            new GradientColorKey(c, 0.0f),
            new GradientColorKey(c, 1.0f),
            };
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2]
            {
            new GradientAlphaKey(c.a, 0.0f),
            new GradientAlphaKey(c.a, 1.0f),
            };

            g.SetKeys(colorKeys, alphaKeys);
        }
    };

}
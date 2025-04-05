using UnityEngine;

namespace UC
{

    public static class ColorExtensions
    {
        public static Color ChangeAlpha(this Color c, float a)
        {
            return new Color(c.r, c.g, c.b, a);
        }

        public static Color Clamp(this Color c)
        {
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
        }

        public static Color MoveTowards(this Color current, Color target, float maxDelta)
        {
            // Gradually move the Red, Green, Blue, and Alpha channels independently
            float r = Mathf.MoveTowards(current.r, target.r, maxDelta);
            float g = Mathf.MoveTowards(current.g, target.g, maxDelta);
            float b = Mathf.MoveTowards(current.b, target.b, maxDelta);
            float a = Mathf.MoveTowards(current.a, target.a, maxDelta);

            // Return the new color
            return new Color(r, g, b, a);
        }

        public static float DistanceRGB(this Color c1, Color c2)
        {
            Color cInc = c1 - c2;
            return Mathf.Sqrt(cInc.r * cInc.r + cInc.g * cInc.g + cInc.b * cInc.b);
        }
        public static float DistanceRGBA(this Color c1, Color c2)
        {
            Color cInc = c1 - c2;
            return Mathf.Sqrt(cInc.r * cInc.r + cInc.g * cInc.g + cInc.b * cInc.b + cInc.a * cInc.a);
        }

        public static Color EvaluateLinear(this Gradient gradient, float t)
        {
            if (gradient.colorSpace != ColorSpace.Linear)
            {
                return gradient.Evaluate(t).linear;
            }

            return gradient.Evaluate(t);
        }
    };

}
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
        public static float DistanceCIELAB_SRGB(this Color c1, Color c2)
        {
            var cl1 = SRGBToCIELAB(c1);
            var cl2 = SRGBToCIELAB(c2);

            float dx = cl1.x - cl2.x;
            float dy = cl1.y - cl2.y;
            float dz = cl1.z - cl2.z;

            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        public static float DistanceCIELAB_RGB(this Color c1, Color c2)
        {
            var cl1 = RGBToCIELAB(c1);
            var cl2 = RGBToCIELAB(c2);

            float dx = cl1.x - cl2.x;
            float dy = cl1.y - cl2.y;
            float dz = cl1.z - cl2.z;

            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }
        public static float DistanceHyAB_SRGB(this Color c1, Color c2)
        {
            var cl1 = SRGBToCIELAB(c1);
            var cl2 = SRGBToCIELAB(c2);

            float dx = cl1.x - cl2.x;
            float dy = cl1.y - cl2.y;
            float dz = cl1.z - cl2.z;

            return Mathf.Abs(dx) + Mathf.Sqrt(dy * dy + dz * dz);
        }
        public static float DistanceHyAB_RGB(this Color c1, Color c2)
        {
            var cl1 = RGBToCIELAB(c1);
            var cl2 = RGBToCIELAB(c2);

            float dx = cl1.x - cl2.x;
            float dy = cl1.y - cl2.y;
            float dz = cl1.z - cl2.z;

            return Mathf.Abs(dx) + Mathf.Sqrt(dy * dy + dz * dz);
        }

        // CIELAB conversion based on https://en.wikipedia.org/wiki/CIELAB_color_space#Forward_transformation and https://30fps.net/pages/hyab-kmeans/
        static float SRGBToLinear(float n)
        {
            return (n <= 0.04045f) ? (n / 12.92f) : (Mathf.Pow((n + 0.055f) / 1.055f, 2.4f));
        }

        static float LinearToSRGB(float n)
        {
            return (n <= 0.0031308f) ? (12.92f * n) : (1.055f * Mathf.Pow(n, 1f / 2.4f) - 0.055f);
        }

        static float PivotLab(float n)
        {
            const float delta = 6f / 29f;
            return (n > delta * delta * delta) ? (Mathf.Pow(n, 1f / 3f)) : ((n / (3f * delta * delta)) + (4f / 29f));
        }

        static float PivotLabInv(float n)
        {
            const float delta = 6f / 29f;
            return (n > delta) ? (n * n * n) : (3f * delta * delta * (n - 4f / 29f));
        }

        public static Color SRGBToLinear(Color c)
        {
            float r = SRGBToLinear(c.r);
            float g = SRGBToLinear(c.g);
            float b = SRGBToLinear(c.b);

            return new Color(r, g, b, c.a);
        }

        public static Color ToLinear(this Color c)
        {
            float r = SRGBToLinear(c.r);
            float g = SRGBToLinear(c.g);
            float b = SRGBToLinear(c.b);

            return new Color(r, g, b, c.a);
        }

        public static Color LinearTosRGB(Color c)
        {
            float r = LinearToSRGB(c.r);
            float g = LinearToSRGB(c.g);
            float b = LinearToSRGB(c.b);

            return new Color(r, g, b, c.a);
        }

        public static Vector3 SRGBToCIELAB(Color rgb)
        {
            return RGBToCIELAB(rgb.ToLinear());
        }

        public static Vector3 RGBToCIELAB(Color rgb)
        {
            float r = rgb.r;
            float g = rgb.g;
            float b = rgb.b;

            // linear RGB -> XYZ (D65)
            float x = r * 0.4124564f + g * 0.3575761f + b * 0.1804375f;
            float y = r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;
            float z = r * 0.0193339f + g * 0.1191920f + b * 0.9503041f;

            // Normalize by white point
            x /= 0.95047f;
            y /= 1.00000f;
            z /= 1.08883f;

            // XYZ → Lab
            float fx = PivotLab(x);
            float fy = PivotLab(y);
            float fz = PivotLab(z);

            float L = 116f * fy - 16f;
            float a = 500f * (fx - fy);
            float bLab = 200f * (fy - fz);

            return new Vector3(L, a, bLab);
        }

        public static Color CIELABToSRGB(Vector3 lab, float alpha = 1.0f)
        {
            float L = lab.x;
            float a = lab.y;
            float b = lab.z;

            // 1. Lab → XYZ
            float fy = (L + 16f) / 116f;
            float fx = fy + a / 500f;
            float fz = fy - b / 200f;

            float x = PivotLabInv(fx) * 0.95047f;
            float y = PivotLabInv(fy) * 1.00000f;
            float z = PivotLabInv(fz) * 1.08883f;

            // 2. XYZ → linear RGB
            float r = 3.2404542f * x - 1.5371385f * y - 0.4985314f * z;
            float g = -0.9692660f * x + 1.8760108f * y + 0.0415560f * z;
            float bRgb = 0.0556434f * x - 0.2040259f * y + 1.0572252f * z;

            // 3. linear RGB → sRGB
            r = LinearToSRGB(r);
            g = LinearToSRGB(g);
            bRgb = LinearToSRGB(bRgb);

            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(bRgb), alpha);
        }

        public static Color EvaluateLinear(this Gradient gradient, float t)
        {
            if (gradient.colorSpace != ColorSpace.Linear)
            {
                return gradient.Evaluate(t).linear;
            }

            return gradient.Evaluate(t);
        }

        public static string ToHex(this Color c)
        {
            return ColorUtility.ToHtmlStringRGBA(c);
        }

        public static Color RandomRGB()
        {
            return new Color(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f), 1.0f);
        }
    };
}

using System.Linq;

public static class RandomExtension
{
    public static float Range(this System.Random rnd, float valMin, float valMax)
    {
        return (float)(rnd.NextDouble() * (valMax - valMin) + valMin);
    }

    public static int Range(this System.Random rnd, int valMin, int valMax)
    {
        if (valMin == valMax) return valMin;

        return rnd.Next() % (valMax - valMin) + valMin;
    }

    public static int Random(this float[] a, System.Random rnd)
    {
        float r = (float)rnd.NextDouble();
        float sum = a.Sum();
        
        for (int j = 0; j < a.Length; j++) a[j] /= sum;

        int     i = 0;
        float   x = 0;

        while (i < a.Length)
        {
            x += a[i];
            if (r <= x) return i;
            i++;
        }

        return 0;
    }
}

using UnityEngine;

namespace UC
{

    public static class Noise
    {
        static PerlinNoise3D[] perlinNoise3D = new[] {
            new PerlinNoise3D(0),
            new PerlinNoise3D(101),
            new PerlinNoise3D(202)
        };

        public static float Perlin3d(Vector3 p) => Perlin3d(p.x, p.y, p.z);
        public static float Perlin3d(float x, float y, float z) => perlinNoise3D[0].Evaluate(x, y, z);

        public static Vector3 PerlinDirection3d(Vector3 p) => new Vector3(perlinNoise3D[0].EvaluateExt(p) * 2.0f - 1.0f, perlinNoise3D[1].EvaluateExt(p) * 2.0f - 1.0f, perlinNoise3D[2].EvaluateExt(p) * 2.0f - 1.0f).normalized;

        public static Vector3 PerlinDirection3d(float x, float y, float z) => PerlinDirection3d(new Vector3(x, y, z));
    }
}

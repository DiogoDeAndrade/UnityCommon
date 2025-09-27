using UnityEngine;

namespace UC
{

    public static class VectorExtensions
    {
        public static Vector3 x0z(this Vector3 inV)
        {
            return new Vector3(inV.x, 0.0f, inV.z);
        }
        public static Vector3 xy0(this Vector3 inV)
        {
            return new Vector3(inV.x, inV.y, 0.0f);
        }

        public static Vector2 xz(this Vector3 inV)
        {
            return new Vector2(inV.x, inV.z);
        }

        public static Vector2 xy(this Vector3 inV)
        {
            return new Vector2(inV.x, inV.y);
        }

        public static Vector3 xy0(this Vector2 inV)
        {
            return new Vector3(inV.x, inV.y, 0);
        }

        public static Vector3 xyz(this Vector2 inV, float z)
        {
            return new Vector3(inV.x, inV.y, z);
        }

        public static Vector2 yz(this Vector3 inV)
        {
            return new Vector2(inV.y, inV.z);
        }

        public static Vector2 zx(this Vector3 inV)
        {
            return new Vector2(inV.z, inV.x);
        }

        public static Vector2 yx(this Vector3 inV)
        {
            return new Vector2(inV.y, inV.x);
        }

        public static Vector2 zy(this Vector3 inV)
        {
            return new Vector2(inV.z, inV.y);
        }

        public static float Random(this Vector2 limits)
        {
            return UnityEngine.Random.Range(limits.x, limits.y);
        }

        public static Vector2 RandomXY(this Vector2 v)
        {
            return new Vector2(UnityEngine.Random.Range(0.0f, v.x), UnityEngine.Random.Range(0.0f, v.y));
        }


        public static float Random(this Vector2 limits, System.Random random)
        {
            return random.Range(limits.x, limits.y);
        }

        public static Vector2 RandomXY(this Vector2 v, System.Random randomGenerator)
        {
            return new Vector2(randomGenerator.Range(0.0f, v.x), randomGenerator.Range(0.0f, v.y));
        }


        public static int Random(this Vector2Int limits)
        {
            return UnityEngine.Random.Range(limits.x, limits.y + 1);
        }

        public static Vector2Int RandomXY(this Vector2Int v)
        {
            return new Vector2Int(UnityEngine.Random.Range(0, v.x), UnityEngine.Random.Range(0, v.y));
        }

        public static Vector2Int RandomXY(this Vector2Int v, System.Random randomGenerator)
        {
            return new Vector2Int(randomGenerator.Range(0, v.x), randomGenerator.Range(0, v.y));
        }

        public static int Random(this Vector2Int limits, System.Random random)
        {
            return random.Range(limits.x, limits.y + 1);
        }

        public static Vector3 xyz(this Vector4 v)
        {
            return new Vector3(v.x, v.y, v.z);
        }

        public static Vector4 xyz0(this Vector3 v)
        {
            return new Vector4(v.x, v.y, v.z, 0);
        }

        public static Vector4 xyz1(this Vector3 v)
        {
            return new Vector4(v.x, v.y, v.z, 1);
        }

        public static void SafeNormalize(this Vector2 v)
        {
            if (v.sqrMagnitude > 1e-3)
            {
                v.Normalize();
            }
            else
            {
                v.Set(0, 0);
            }
        }

        public static Vector2 SafeNormalized(this Vector2 v)
        {
            if (v.sqrMagnitude > 1e-3)
            {
                return v.normalized;
            }
            else
            {
                return Vector2.zero;
            }
        }

        public static void SafeNormalize(this Vector3 v)
        {
            if (v.sqrMagnitude > 1e-3)
            {
                v.Normalize();
            }
            else
            {
                v.Set(0, 0, 0);
            }
        }
        public static Vector3 SafeNormalized(this Vector3 v)
        {
            if (v.sqrMagnitude > 1e-3)
            {
                return v.normalized;
            }
            else
            {
                return Vector3.zero;
            }
        }

        public static void SafeNormalize(this Vector4 v)
        {
            if (v.sqrMagnitude > 1e-3)
            {
                v.Normalize();
            }
            else
            {
                v.Set(0, 0, 0, 0);
            }
        }
        public static Vector4 SafeNormalized(this Vector4 v)
        {
            if (v.sqrMagnitude > 1e-3)
            {
                return v.normalized;
            }
            else
            {
                return Vector4.zero;
            }
        }

        public static Vector3 RotateZ(this Vector3 v, float angle)
        {
            float a = Mathf.Deg2Rad * angle;
            float c = Mathf.Cos(a);
            float s = Mathf.Sin(a);

            var x = c * v.x - s * v.y;
            var y = s * v.x + c * v.y;

            return new Vector3(x, y, v.z);
        }

        public static Vector2 Perpendicular(this Vector2 v)
        {
            return new Vector2(-v.y, v.x);
        }
        public static Vector3 PerpendicularXY(this Vector3 v)
        {
            return new Vector3(-v.y, v.x, v.z);
        }

        public static Vector3 Perpendicular(this Vector3 v)
        {
            if (v == Vector3.zero)
                return Vector3.zero;

            v.Normalize();

            // Candidate axes
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

            // Pick the axis least aligned with v (smallest dot)
            Vector3 bestAxis = axes[0];
            float minDot = Mathf.Abs(Vector3.Dot(v, bestAxis));

            for (int i = 1; i < axes.Length; i++)
            {
                float d = Mathf.Abs(Vector3.Dot(v, axes[i]));
                if (d < minDot)
                {
                    minDot = d;
                    bestAxis = axes[i];
                }
            }

            // Cross to get a perpendicular
            return Vector3.Cross(v, bestAxis).normalized;
        }

        public static Vector2 ComponentScale(this Vector2 v, Vector2 scale)
        {
            return new Vector2(v.x * scale.x, v.y * scale.y);
        }

        public static Vector3 ComponentScale(this Vector3 v, Vector3 scale)
        {
            return new Vector3(v.x * scale.x, v.y * scale.y, v.z * scale.z);
        }

        public static Vector4 ComponentScale(this Vector4 v, Vector4 scale)
        {
            return new Vector4(v.x * scale.x, v.y * scale.y, v.z * scale.z, v.w * scale.w);
        }

        public static Vector2Int xy(this Vector3Int v)
        {
            return new Vector2Int(v.x, v.y);
        }

        public static Vector3Int xy0(this Vector2Int v)
        {
            return new Vector3Int(v.x, v.y, 0);
        }

        public static Vector2Int toInt(this Vector2 v)
        {
            return new Vector2Int((int)v.x, (int)v.y);
        }
        public static Vector3Int toInt(this Vector3 v)
        {
            return new Vector3Int((int)v.x, (int)v.y, (int)v.z);
        }

        public static Vector2 ChangeX(this Vector2 v, float value)
        {
            return new Vector2(value, v.y);
        }

        public static Vector2 ChangeY(this Vector2 v, float value)
        {
            return new Vector2(v.x, value);
        }
        public static Vector3 ChangeX(this Vector3 v, float value)
        {
            return new Vector3(value, v.y, v.z);
        }

        public static Vector3 ChangeY(this Vector3 v, float value)
        {
            return new Vector3(v.x, value, v.z);
        }

        public static Vector3 ChangeZ(this Vector3 v, float value)
        {
            return new Vector3(v.x, v.y, value);
        }
        public static Vector4 ChangeX(this Vector4 v, float value)
        {
            return new Vector4(value, v.y, v.z, v.w);
        }

        public static Vector4 ChangeY(this Vector4 v, float value)
        {
            return new Vector4(v.x, value, v.z, v.w);
        }

        public static Vector4 ChangeZ(this Vector4 v, float value)
        {
            return new Vector4(v.x, v.y, value, v.w);
        }
        public static Vector4 ChangeW(this Vector4 v, float value)
        {
            return new Vector4(v.x, v.y, v.z, value);
        }
    };

}
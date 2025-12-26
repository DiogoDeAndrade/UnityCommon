using System;
using UnityEngine;

namespace UC
{

    [Serializable]
    public class DistanceRange
    {
        public enum Type { Euclidean, Manhattan, Chebyshev };

        public Type type = Type.Euclidean;
        public float min = 0;
        public float max = 10;

        public DistanceRange()
        {
        }
        public DistanceRange(int range, Type type = Type.Euclidean)
        {
            this.type = type;
            max = range;
        }

        public float GetDistance(Vector2 p1, Vector2 p2)
        {
            switch (type)
            {
                case Type.Euclidean:
                    return Vector2.Distance(p1, p2);
                case Type.Manhattan:
                    return Mathf.Abs(p1.x - p2.x) + Mathf.Abs(p1.y - p2.y);
                case Type.Chebyshev:
                    return Mathf.Max(Mathf.Abs(p1.x - p2.x), Mathf.Abs(p1.y - p2.y));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        public float GetDistance(Vector3 p1, Vector3 p2)
        {
            switch (type)
            {
                case Type.Euclidean:
                    return Vector3.Distance(p1, p2);
                case Type.Manhattan:
                    return Mathf.Abs(p1.x - p2.x) + Mathf.Abs(p1.y - p2.y) + Mathf.Abs(p1.z - p2.z);
                case Type.Chebyshev:
                    return Mathf.Max(Mathf.Abs(p1.x - p2.x), Mathf.Abs(p1.y - p2.y), Mathf.Abs(p1.z - p2.z));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public bool IsInRange(Vector2 p1, Vector2 p2)
        {
            return IsInRange(GetDistance(p1, p2));
        }

        public bool IsInRange(Vector3 p1, Vector3 p2)
        {
            return IsInRange(GetDistance(p1, p2));
        }

        public bool IsInRange(float distance)
        {
            return (distance >= min) && (distance <= max);
        }
    }
}
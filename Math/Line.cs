using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Line
{
    public static bool Intersect(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, out Vector3 intersection)
    {
        Vector3 lineVec3 = b1 - a1;
        Vector3 lineVec1 = a2 - a1;
        Vector3 lineVec2 = b2 - b1;
        Vector3 crossVec1and2 = Vector3.Cross(lineVec1, lineVec2);
        Vector3 crossVec3and2 = Vector3.Cross(lineVec3, lineVec2);

        float planarFactor = Vector3.Dot(lineVec3, crossVec1and2);

        //is coplanar, and not parallel
        if ((Mathf.Abs(planarFactor) < 0.0001f) && (crossVec1and2.sqrMagnitude > 0.0001f))
        {
            float s1 = Vector3.Dot(crossVec3and2, crossVec1and2) / crossVec1and2.sqrMagnitude;

            if ((s1 >= 0) && (s1 <= 1))
            {
                float s2 = Vector3.Dot(Vector3.Cross(-lineVec3, lineVec1), -crossVec1and2) / crossVec1and2.sqrMagnitude;

                intersection = b1 + (lineVec2 * Mathf.Max(0.0f, Mathf.Min(s2, 1.0f)));

                return (s2 >= 0) && (s2 <= 1);
            }
            else
            {
                intersection = a1 + (lineVec1 * Mathf.Max(0.0f, Mathf.Min(s1, 1.0f)));
                return false;
            }
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }
}

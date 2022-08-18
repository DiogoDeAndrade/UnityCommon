using UnityEngine;

public class Triangle
{
    const float kEpsilon = 0.0001f;

    Vector3[] v;

    public Triangle()
    {
        v = new Vector3[3] { Vector3.zero, Vector3.zero, Vector3.zero };
    }
    public Triangle(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        v = new Vector3[3] { p1, p2, p3 };
    }

    public float area => 0.5f * Mathf.Sqrt(Mathf.Pow(((v[1].x * v[0].y) - (v[2].x * v[0].y) - (v[0].x * v[1].y) + (v[2].x * v[1].y) + (v[0].x * v[2].y) - (v[1].x * v[2].y)), 2.0f) +
                                           Mathf.Pow(((v[1].x * v[0].z) - (v[2].x * v[0].z) - (v[0].x * v[1].z) + (v[2].x * v[1].z) + (v[0].x * v[2].z) - (v[1].x * v[2].z)), 2.0f) +
                                           Mathf.Pow(((v[1].y * v[0].z) - (v[2].y * v[0].z) - (v[0].y * v[1].z) + (v[2].y * v[1].z) + (v[0].y * v[2].z) - (v[1].y * v[2].z)), 2.0f));

    public static Triangle operator*(Triangle src, Matrix4x4 matrix) => new Triangle(matrix * src.v[0].xyz1(), matrix * src.v[1].xyz1(), matrix * src.v[2].xyz1());

    public Vector3 GetVertex(int index)
    {
        return v[index];        
    }

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist, out float t)
    {
        // compute plane's normal
        Vector3 v0v1 = v[1] - v[0];
        Vector3 v0v2 = v[2] - v[0];
        // no need to normalize
        Vector3 N = Vector3.Cross(v0v1,v0v2); // N 
        float area2 = N.magnitude;

        // Step 1: finding P
        t = 0.0f;

        // check if ray and plane are parallel ?
        float NdotRayDirection = Vector3.Dot(N,dir);
        if (Mathf.Abs(NdotRayDirection) < kEpsilon) // almost 0 
            return false; // they are parallel so they don't intersect ! 

        // compute d parameter using equation 2
        float d = -Vector3.Dot(N, v[0]);

        // compute t (equation 3)
        t = -(Vector3.Dot(N, origin) + d) / NdotRayDirection;

        // check if the triangle is in behind the ray
        if (t < 0) return false; // the triangle is behind 

        // compute the intersection point using equation 1
        Vector3 P = origin + t * dir;

        // Step 2: inside-outside test
        Vector3 C; // vector perpendicular to triangle's plane 

        // edge 0
        Vector3 edge0 = v[1] - v[0];
        Vector3 vp0 = P - v[0];
        C = Vector3.Cross(edge0, vp0);
        if (Vector3.Dot(N, C) < 0) return false; // P is on the right side 

        // edge 1
        Vector3 edge1 = v[2] - v[1];
        Vector3 vp1 = P - v[1];
        C = Vector3.Cross(edge1, vp1);
        if (Vector3.Dot(N, C) < 0) return false; // P is on the right side 

        // edge 2
        Vector3 edge2 = v[0] - v[2];
        Vector3 vp2 = P - v[2];
        C = Vector3.Cross(edge2, vp2);
        if (Vector3.Dot(N, C) < 0) return false; // P is on the right side; 

        if (t > maxDist) return false;

        return true; // this ray hits the triangle 
    }

    public void DrawGizmo()
    {
        Gizmos.DrawLine(v[0], v[1]);
        Gizmos.DrawLine(v[1], v[2]);
        Gizmos.DrawLine(v[2], v[0]);
    }
}

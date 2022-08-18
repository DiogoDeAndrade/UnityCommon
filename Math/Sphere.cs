using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sphere 
{
    public Vector3  position;
    public float    radius;

    public bool Raycast(Ray ray, float maxDistance, out float t)
    {
        return Sphere.Raycast(ray, position, radius, maxDistance, out t);
    }

    public static bool Raycast(Ray ray, Vector3 position, float radius, float maxDistance, out float t)
    {
        Vector3     m = ray.origin - position;
        float       b = Vector3.Dot(m, ray.direction);
        float       c = Vector3.Dot(m, m) - radius * radius;

        t = float.MaxValue;

        // Exit if r’s origin outside s (c > 0) and r pointing away from s (b > 0) 
        if ((c > 0.0f) && (b > 0.0f)) return false;
        
        float discr = b * b - c;

        // A negative discriminant corresponds to ray missing sphere 
        if (discr < 0.0f) return false;

        // Ray now found to intersect sphere, compute smallest t value of intersection
        t = -b - Mathf.Sqrt(discr);

        // If t is negative, ray started inside sphere so clamp t to zero 
        if (t < 0.0f) t = 0.0f;
        
        return true;
    }
}

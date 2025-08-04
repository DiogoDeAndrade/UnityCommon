using System;
using UnityEngine;

[Serializable]
public class Bounds2d
{
    public Vector2 center;
    public Vector2 size;

    public Vector2 extents
    {
        get => size * 0.5f;
        set => size = value * 2.0f;
    }
    public Vector2 min
    {
        get => center - extents;
        set => center = value + extents;
    }
    public Vector2 max
    {
        get => center + extents;
        set => center = value - extents;
    }

    public Bounds2d(Vector2 center, Vector2 size)
    {
        this.center = center;
        this.size = size;
    }

    public Vector2 ClosestPoint(Vector2 p)
    {
        // Clamp p.x between min.x and max.x, and p.y between min.y and max.y
        float clampedX = Mathf.Clamp(p.x, min.x, max.x);
        float clampedY = Mathf.Clamp(p.y, min.y, max.y);

        return new Vector2(clampedX, clampedY);
    }

    public bool Contains(Vector2 p)
    {
        return ((min.x <= p.x) && (max.x >= p.x) &&
                (min.y <= p.y) && (max.y >= p.y));
    }

    public bool ContainsMinInclusive(Vector2 p)
    {
        return ((min.x <= p.x) && (max.x > p.x) &&
                (min.y <= p.y) && (max.y > p.y));
    }

    public void Encapsulate(Bounds2d bounds)
    {
        // Compute the new min and max corners
        Vector2 newMin = Vector2.Min(this.min, bounds.min);
        Vector2 newMax = Vector2.Max(this.max, bounds.max);

        // Update size and center to cover both
        size = newMax - newMin;
        center = newMin + size * 0.5f;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Polyline : IEnumerable<(Vector3 position, Vector3 normal)>
{
    [SerializeField]
    List<Vector3>   vertices;
    List<Vector3>   normals;

    public List<Vector3> GetVertices() => vertices;

    public Polyline() { }
    public Polyline(Vector3 p) { vertices = new() { p }; }
    public Polyline(Vector3 p1, Vector3 p2, Vector3 p3) { vertices = new() { p1, p2, p3 }; }

    public void Add(Vector3 vertex)
    {
        if (vertices == null) vertices = new List<Vector3>();

        vertices.Add(vertex);
    }
    public void Add(Vector3 vertex, Vector3 normal)
    {
        if (vertices == null) vertices = new List<Vector3>();
        if (normals == null) normals = new List<Vector3>();

        vertices.Add(vertex);
        normals.Add(normal);
    }


    public void SetNormal(int index, Vector3 normal)
    {
        if (normals == null)
        {
            normals = new List<Vector3>();
            normals.Resize(index + 1);
        }

        normals[index] = normal;
    }

    public void Insert(int index, Vector3 vertex)
    {
        vertices.Insert(index, vertex);
    }
    public void Insert(int index, Vector3 vertex, Vector3 normal)
    {
        vertices.Insert(index, vertex);
        normals.Insert(index, normal);
    }

    public int Count
    {
        get => (vertices != null) ? (vertices.Count) : (0);
    }

    public int normalCount
    {
        get => (normals != null) ? (normals.Count) : (0);
        set
        {
            if (value == 0) normals = null;
            else
            {
                if (normals == null) normals = new List<Vector3>();
                normals.Resize(value);
            }
        }
    }

    public Vector3 this[int idx]
    {
        get => vertices[idx];
        set => vertices[idx] = value;
    }

    public Vector3 GetNormal(int idx)
    {
        return normals[idx];
    }
        
    public void Simplify(float maxDistance)
    {
        int     currentPoint = 0;
        float   currentError = 0;

        while ((currentPoint < vertices.Count) && (vertices.Count > 3))
        {
            int     i1 = (currentPoint + 1) % (vertices.Count);
            int     i2 = (currentPoint + 2) % (vertices.Count);
            Vector3 p0 = vertices[currentPoint];
            Vector3 p1 = vertices[i1];
            Vector3 p2 = vertices[i2];

            float dist = Line.Distance(p0, p2, p1);
            float error = (dist * (p2 - p0).magnitude) * 0.5f;
            if ((error + currentError ) <= maxDistance)
            {
                vertices.RemoveAt(i1);
                normals?.RemoveAt(i1);
                currentError += error;
            }
            else
            {
                currentPoint++;
                currentError = 0;
            }
        }
    }

    public bool isCW()
    {
        Vector3 v = Vector3.zero;
        var     count = vertices.Count;

        for (int i = 0; i < count; i++)
        {
            Vector3 p0 = vertices[i];
            Vector3 p1 = vertices[(i + 1) % count];
            Vector3 p2 = vertices[(i + 2) % count];

            v += Vector3.Cross(p1 - p0, p2 - p0);
        }
        v.Normalize();

        float ma = 0.0f;

        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
        {
            if (Mathf.Abs(v.x) > Mathf.Abs(v.z)) ma = v.x;
            else ma = v.z;
        }
        else
        {
            if (Mathf.Abs(v.y) > Mathf.Abs(v.z)) ma = v.y;
            else ma = v.z;
        }

        return (ma < 0);
    }

    public void ReverseOrder()
    {
        vertices.Reverse();
    }

    public float ComputeArea()
    {
        if (vertices == null || vertices.Count < 3)
        {
            return 0f; // No area for less than 3 vertices
        }

        // Step 1: Determine the best projection plane by calculating the normal vector
        Vector3 normal = CalculateNormal();

        // Step 2: Project the polygon onto the most appropriate 2D plane
        List<Vector2> projectedVertices = ProjectTo2D(normal);

        // Step 3: Use the Shoelace formula to calculate the area of the 2D polygon
        return Calculate2DPolygonArea(projectedVertices);
    }

    Vector3 CalculateNormal()
    {
        Vector3 normal = Vector3.zero;
        if ((normals != null) && (normals.Count == vertices.Count))
        {
            foreach (var n in normals)
            {
                normal += n;
            }
        }
        else
        {
            // Using Newell's method to calculate the normal
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 current = vertices[i];
                Vector3 next = vertices[(i + 1) % vertices.Count];

                normal.x += (current.y - next.y) * (current.z + next.z);
                normal.y += (current.z - next.z) * (current.x + next.x);
                normal.z += (current.x - next.x) * (current.y + next.y);
            }
        }

        normal.Normalize();

        return normal;
    }

    private List<Vector2> ProjectTo2D(Vector3 normal)
    {
        List<Vector2> projectedVertices = new List<Vector2>();

        // Determine the dominant axis (the largest component in the normal vector)
        if (Mathf.Abs(normal.x) > Mathf.Abs(normal.y) && Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
        {
            // Project onto YZ plane (ignore x component)
            foreach (var vertex in vertices)
            {
                projectedVertices.Add(new Vector2(vertex.y, vertex.z));
            }
        }
        else if (Mathf.Abs(normal.y) > Mathf.Abs(normal.x) && Mathf.Abs(normal.y) > Mathf.Abs(normal.z))
        {
            // Project onto XZ plane (ignore y component)
            foreach (var vertex in vertices)
            {
                projectedVertices.Add(new Vector2(vertex.x, vertex.z));
            }
        }
        else
        {
            // Project onto XY plane (ignore z component)
            foreach (var vertex in vertices)
            {
                projectedVertices.Add(new Vector2(vertex.x, vertex.y));
            }
        }

        return projectedVertices;
    }

    // Calculate the area of a 2D polygon using the Shoelace formula
    private float Calculate2DPolygonArea(List<Vector2> vertices2D)
    {
        float area = 0f;
        int count = vertices2D.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 current = vertices2D[i];
            Vector2 next = vertices2D[(i + 1) % count]; // Ensure the loop wraps around

            area += current.x * next.y;
            area -= current.y * next.x;
        }

        return Mathf.Abs(area) * 0.5f;
    }

    public Vector3 GetCenter()
    {
        Vector3 center = Vector3.zero;
        if ((vertices == null) || (vertices.Count == 0)) return center;

        foreach (var v in vertices)
        {
            center += v;
        }

        return center / vertices.Count;
    }

    // Implement the IEnumerable interface to allow iteration
    public IEnumerator<(Vector3 position, Vector3 normal)> GetEnumerator()
    {
        if ((normals != null) && (vertices.Count == normals.Count))
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 position = vertices[i];
                Vector3 normal = normals[i];
                yield return (position, normal);
            }
        }
        else
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 position = vertices[i];
                yield return (position, Vector3.zero);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

#if UNITY_EDITOR
    public void DrawGizmos()
    {
        for (int i = 0; i < vertices.Count - 1; i++)
        {
            Gizmos.DrawLine(vertices[i], vertices[i + 1]);
        }
    }
#endif
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Polyline
{
    [SerializeField]
    List<Vector3>    vertices;

    public List<Vector3> GetVertices() => vertices;

    public void Add(Vector3 vertex)
    {
        if (vertices == null) vertices = new List<Vector3>();

        vertices.Add(vertex);
    }

    public int Count
    {
        get => (vertices != null) ? (vertices.Count) : (0);
    }

    public Vector3 this[int idx]
    {
        get => vertices[idx];
        set => vertices[idx] = value;
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
}

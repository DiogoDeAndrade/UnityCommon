
using System;
using System.Collections.Generic;
using UnityEngine;

public class Octree<T> where T : class
{
    protected Vector3 min;
    protected Vector3 max;
    protected int     nLevels;

    protected class Node
    {
        public Node     parent;
        public bool     isLeaf;
        public Bounds   bounds;
        public List<T>  objects;
        public Node[]   children;
    }

    protected Node rootNode;

    public Octree(Vector3 min, Vector3 max, int nLevels)
    {
        this.min = Vector3.zero;        
        this.max = Vector3.zero;
        if (min.x < max.x) { this.min.x = min.x; this.max.x = max.x; } else { this.min.x = max.x; this.max.x = min.x; }
        if (min.y < max.y) { this.min.y = min.y; this.max.y = max.y; } else { this.min.y = max.y; this.max.y = min.y; }
        if (min.z < max.z) { this.min.z = min.z; this.max.z = max.z; } else { this.min.z = max.z; this.max.z = min.z; }
        this.nLevels = nLevels;

        rootNode = Init(new Bounds((this.min + this.max) * 0.5f, (this.max - this.min)), nLevels);
    }

    public void Add(Vector3 position, T value)
    {        
        var leafNode = GetLeafNode(rootNode, position);
        if (leafNode != null)
        {
            leafNode.objects.Add(value);
        }
    }
    public void Add(float x, float y, float z, T value) => Add(new Vector3(x, y, z), value);    

    public void Remove(T value)
    {
        Remove(rootNode, value);
    }

    public void GetCandidateObjectsInSphere(Vector3 p, float radius, List<T> ret)
    {
        GetCandidateObjectsInSphere(rootNode, p, radius, ret);
    }

    void GetCandidateObjectsInSphere(Node node, Vector3 p, float radius, List<T> ret)
    {
        bool includeThis = node.bounds.Contains(p);

        if (!includeThis)
        {
            float dist = (node.bounds.ClosestPoint(p) - p).sqrMagnitude;
            if (dist <= radius * radius)
            {
                includeThis = true;
            }
        }

        if (includeThis)
        {
            if (node.isLeaf)
            {
                foreach (var obj in node.objects)
                {
                    ret.Add(obj);
                }
            }
            else
            {
                for (int i = 0; i < node.children.Length; i++)
                {
                    GetCandidateObjectsInSphere(node.children[i], p, radius, ret);
                }
            }
        }
    }

    Node GetLeafNode(Node node, Vector3 p)
    {
        if (node.bounds.ContainsMinInclusive(p))
        {
            if (node.isLeaf) return node;
            else
            {
                for (int i = 0; i < node.children.Length; i++)
                {
                    var ret = GetLeafNode(node.children[i], p);
                    if (ret != null) return ret;
                }
            }
        }

        return null;
    }

    void Remove(Node node, T value)
    {
        if (node.isLeaf)
        {
            node.objects.RemoveAll((o) => o == value);
        }
        else
        {
            for (int i = 0; i < node.children.Length; i++)
            {
                Remove(node.children[i], value);                    
            }
        }
    }

    Node Init(Bounds b, int nLevels, Node parent = null)
    {
        Node n = new Node
        {
            isLeaf = (nLevels == 0),
            bounds = b,
            parent = parent
        };

        if (n.isLeaf)
        {
            n.objects = new List<T>();
        }
        else
        {
            n.children = new Node[8];

            Vector3 s = b.size * 0.5f;

            n.children[0] = Init(new Bounds(b.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 0.5f, s), nLevels - 1, n);
            n.children[1] = Init(new Bounds(b.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 0.5f, s), nLevels - 1, n);
            n.children[2] = Init(new Bounds(b.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 0.5f, s), nLevels - 1, n);
            n.children[3] = Init(new Bounds(b.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 0.5f, s), nLevels - 1, n);
            n.children[4] = Init(new Bounds(b.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 1.5f, s), nLevels - 1, n);
            n.children[5] = Init(new Bounds(b.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 1.5f, s), nLevels - 1, n);
            n.children[6] = Init(new Bounds(b.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 1.5f, s), nLevels - 1, n);
            n.children[7] = Init(new Bounds(b.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 1.5f, s), nLevels - 1, n);
        }

        return n;
    }
}

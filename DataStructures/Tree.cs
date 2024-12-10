using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public class Tree<N> where N : IEquatable<N>
{
    public delegate bool TestCollapseFunc(Tree<N> tree, int grandParentId, int parentId, int nodeId);

    [Serializable]
    public class Node
    {
        public Node(N data, int parentId = -1) { this.data = data; parent = parentId; }

        public bool isLeaf => (children == null || children.Count == 0);
        public bool isRoot => parent == -1;

        public N            data;
        public int          parent;
        public List<int>    children;

        public void AddChild(int newNodeId)
        {
            if (children == null) children = new List<int>();
            children.Add(newNodeId);
        }

        public void RemoveChild(int childId)
        {
            if (children == null) return;
            children.Remove(childId);
        }
    };

    [SerializeField]
    private List<Node>      nodes;
    [SerializeField]
    public  int             rootNodeId = -1;

    public int nodeCount => (nodes != null) ? (nodes.Count) : (0);

    public Tree(N node)
    {
        var rootNode = new Node(node);
        nodes = new() { rootNode };
        rootNodeId = 0;
    }

    public int AddNode(N node, int treeParentNodeId) 
    {
        var newNode = new Node(node, treeParentNodeId);
        nodes.Add(newNode);
        var newNodeId = nodes.Count - 1;
        nodes[treeParentNodeId].AddChild(newNodeId);

        return newNodeId;
    }

    public N GetNode(int nodeId)
    {
        return nodes[nodeId].data;
    }

    public IEnumerable<int> GetChildren(int nodeId)
    {
        if (nodes[nodeId].children == null) return Enumerable.Empty<int>();
        return nodes[nodeId].children.AsEnumerable();
    }

    public int GetChildrenCount(int nodeId)
    {
        if (nodes[nodeId].children == null) return 0;

        return nodes[nodeId].children.Count;
    }

    public void Simplify(TestCollapseFunc collapseTest)
    {
        Simplify(rootNodeId, collapseTest);
    }

    void Simplify(int nodeId, TestCollapseFunc collapseTest)
    {
        bool anyChange = true;

        if (nodes[nodeId].isLeaf) return;

        while (anyChange)
        {
            anyChange = false;

            foreach (var childId in nodes[nodeId].children)
            {
                if (nodes[childId].isLeaf) continue;

                bool canMove = true;

                foreach (var grandchildId in nodes[childId].children)
                {
                    if (!collapseTest(this, nodeId, childId, grandchildId))
                    {
                        canMove = false;
                        break;
                    }
                }

                if (canMove)
                {
                    anyChange = true;

                    nodes[nodeId].RemoveChild(childId);
                    foreach (var grandchildId in nodes[childId].children)
                    {
                        nodes[nodeId].AddChild(grandchildId);
                        nodes[grandchildId].parent = nodeId;
                    }
                    break;
                }
            }
        }

        // Already simplified what we can, move to next children
        foreach (var childId in nodes[nodeId].children)
        {
            Simplify(childId, collapseTest);
        }
    }

}

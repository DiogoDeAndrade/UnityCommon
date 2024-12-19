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

        public N data;
        public int parent;
        public List<int> children;

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
    private List<Node> nodes;
    [SerializeField]
    public int rootNodeId = -1;

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

    public void Balance()
    {
        // Balance until you can't balance anymore - at most do 10 balances
        int maxBalance = 0;
        while (maxBalance < 10)
        {
            if (!BalanceNode(rootNodeId)) break;

            maxBalance++;
        }
    }

    bool BalanceNode(int nodeId)
    {
        var node = nodes[nodeId];
        if (node.isLeaf) return false;

        // Balance children first
        bool ret = true;
        while (ret)
        {
            ret = false;
            foreach (var c in node.children)
            {
                ret |= BalanceNode(c);
                if (ret) break;
            }
        }

        int minDepth = int.MaxValue;
        int maxDepth = -int.MaxValue;
        int deepestNode = -1;

        foreach (var c in node.children)
        {
            int depth = GetMaxDepth(c);
            if (depth < minDepth) minDepth = depth;
            if (depth > maxDepth) { maxDepth = depth; deepestNode = c; }
        }

        if ((maxDepth - minDepth) > 1)
        {
            // Need to balance this node
            int thisParent = nodes[nodeId].parent;

            nodes[nodeId].RemoveChild(deepestNode);
            nodes[deepestNode].parent = -1;

            if (thisParent == -1)
            {
                nodes[deepestNode].parent = -1;
                rootNodeId = deepestNode;
            }
            else
            {
                nodes[thisParent].RemoveChild(nodeId);
                nodes[thisParent].AddChild(deepestNode);
                nodes[deepestNode].parent = thisParent;
            }

            nodes[deepestNode].AddChild(nodeId);
            nodes[nodeId].parent = deepestNode;

            ret = true;
        }

        return ret;
    }

    int GetMaxDepth(int nodeId)
    {
        var node = nodes[nodeId];
        
        if (node.isLeaf) return 1;

        int ret = 1;
        foreach (var c in node.children)
        {
            ret = Mathf.Max(ret, GetMaxDepth(c) + 1);
        }

        return ret;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VoxelTree
{
    protected class Node
    {
        public Node         parent;
        public bool         isFull;
        public Bounds       bounds;
        public Vector3Int   voxelCount;
        public Vector3Int   voxelOffset;
        public byte[]       voxels;
        public Node[]       children;

        public bool isVoxelFull()
        {
            if (voxels != null)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if (voxels[i] == 0) return false;
                }

                return true;
            }
            return false;
        }

        public bool isVoxelEmpty()
        {
            if (voxels != null)
            {
                for (int i = 0; i < voxels.Length; i++)
                {
                    if (voxels[i] != 0) return false;
                }

                return true;
            }
            return true;
        }

        public Vector3Int GetVoxelPos(int index)
        {
            return new Vector3Int(voxelOffset.x + index % voxelCount.x,
                                  voxelOffset.y + (index / voxelCount.x) % voxelCount.y,
                                  voxelOffset.z + (index / (voxelCount.x * voxelCount.y)));
        }

        public void GetVoxelData(int index, out Vector3Int pos, out byte value)
        {
            pos = GetVoxelPos(index);
            value = voxels[index];
        }
    }
    
    [SerializeField] protected Node     rootNode;
    [SerializeField] protected int      maxVoxelsPerLeaf;
    [SerializeField] protected bool     isOccupancyMap;
    [SerializeField] protected Vector3  voxelSize;

    public VoxelTree(int maxVoxelsPerLeaf, bool isOccupancyMap)
    {
        this.maxVoxelsPerLeaf = Mathf.Max(1, maxVoxelsPerLeaf);
        this.isOccupancyMap = isOccupancyMap;
    }

    public VoxelTree(int maxVoxelsPerLeaf, bool isOccupancyMap, VoxelData data) : this(maxVoxelsPerLeaf, isOccupancyMap)
    {
        Build(data);
    }

    public void Build(VoxelData data)
    {
        voxelSize = data.voxelSize;
        rootNode = Build(data, null, data.bounds, data.gridSize, new Vector3Int(0, 0, 0));

        // Mark full nodes as full
        MarkFull(rootNode);
        RemoveEmpty(rootNode);
    }

    bool MarkFull(Node node)
    {
        if (node.children != null)
        {
            node.isFull = true;
            foreach (var child in node.children)
            {
                node.isFull &= MarkFull(child);
            }
        }

        return node.isFull;
    }

    bool RemoveEmpty(Node node)
    {
        if (node.children != null)
        {
            int childCount = node.children.Length;
            int countEmpty = 0;
            for (int i = 0; i < childCount; i++)
            {
                var child = node.children[i];
                bool childEmpty = RemoveEmpty(child);
                if (childEmpty)
                {
                    node.children[i] = null;
                    countEmpty++;
                }
            }
            if (countEmpty == 0)
            {

            }
            else if ((childCount - countEmpty) == 0)
            {
                node.children = null;
            }
            else
            {
                var newChildren = new Node[childCount - countEmpty];
                int index = 0;
                foreach (var child in node.children)
                {
                    if (child != null)
                    {
                        newChildren[index] = child;
                        index++;
                    }
                }
                node.children = newChildren;
            }

            return (node.children == null);
        }
        else
        {
            if (node.isFull) return false;
            if (node.voxels == null)
            {
                return true;
            }
            else
            {
                return node.isVoxelEmpty();
            }
        }
    }

    Node Build(VoxelData data, Node parent, Bounds bounds, Vector3Int voxelCount, Vector3Int voxelOffset)
    {            
        var node = new Node()
        {
            parent = parent,
            isFull = false,
            bounds = bounds,
            voxelCount = voxelCount,
            voxelOffset = voxelOffset
        };

        int nVoxels = voxelCount.x * voxelCount.y * voxelCount.z;
        if (nVoxels > maxVoxelsPerLeaf)
        {
            // Subdivide
            node.voxels = null;
            node.children = null;

            List<Node> childrenNodes = new List<Node>();

            var     halfVoxelCount1 = voxelCount / 2;
            if (halfVoxelCount1.x == 0) 
                halfVoxelCount1.x = 1;
            if (halfVoxelCount1.y == 0) 
                halfVoxelCount1.y = 1;
            if (halfVoxelCount1.z == 0) 
                halfVoxelCount1.z = 1;

            var halfVoxelCount2 = voxelCount - halfVoxelCount1;
            
            bool split_x, split_y, split_z;
            split_x = (halfVoxelCount2.x > 0);
            split_y = (halfVoxelCount2.y > 0);
            split_z = (halfVoxelCount2.z > 0);

            Vector3 s = new Vector3(halfVoxelCount1.x * voxelSize.x, halfVoxelCount1.y * voxelSize.y, halfVoxelCount1.z * voxelSize.z);

            childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 0.5f, s), new Vector3Int(halfVoxelCount1.x, halfVoxelCount1.y, halfVoxelCount1.z), new Vector3Int(voxelOffset.x + 0,                 voxelOffset.y + 0                , voxelOffset.z + 0)));
            if (split_x) childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 0.5f, s), new Vector3Int(halfVoxelCount2.x, halfVoxelCount1.y, halfVoxelCount1.z), new Vector3Int(voxelOffset.x + halfVoxelCount1.x, voxelOffset.y + 0                , voxelOffset.z + 0)));
            if (split_y) childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 0.5f, s), new Vector3Int(halfVoxelCount1.x, halfVoxelCount2.y, halfVoxelCount1.z), new Vector3Int(voxelOffset.x + 0,                 voxelOffset.y + halfVoxelCount1.y, voxelOffset.z + 0)));
            if (split_x && split_y) childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 0.5f, s), new Vector3Int(halfVoxelCount2.x, halfVoxelCount2.y, halfVoxelCount1.z), new Vector3Int(voxelOffset.x + halfVoxelCount1.x, voxelOffset.y + halfVoxelCount1.y, voxelOffset.z + 0)));
            if (split_z)
            {
                childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 1.5f, s), new Vector3Int(halfVoxelCount1.x, halfVoxelCount1.y, halfVoxelCount2.z), new Vector3Int(voxelOffset.x + 0, voxelOffset.y + 0, voxelOffset.z + halfVoxelCount1.z)));
                if (split_x) childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 0.5f + Vector3.forward * s.z * 1.5f, s), new Vector3Int(halfVoxelCount2.x, halfVoxelCount1.y, halfVoxelCount2.z), new Vector3Int(voxelOffset.x + halfVoxelCount1.x, voxelOffset.y + 0, voxelOffset.z + halfVoxelCount1.z)));
                if (split_y) childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 0.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 1.5f, s), new Vector3Int(halfVoxelCount1.x, halfVoxelCount2.y, halfVoxelCount2.z), new Vector3Int(voxelOffset.x + 0, voxelOffset.y + halfVoxelCount1.y, voxelOffset.z + halfVoxelCount1.z)));
                if (split_x && split_y) childrenNodes.Add(Build(data, node, new Bounds(bounds.min + Vector3.right * s.x * 1.5f + Vector3.up * s.y * 1.5f + Vector3.forward * s.z * 1.5f, s), new Vector3Int(halfVoxelCount2.x, halfVoxelCount2.y, halfVoxelCount2.z), new Vector3Int(voxelOffset.x + halfVoxelCount1.x, voxelOffset.y + halfVoxelCount1.y, voxelOffset.z + halfVoxelCount1.z)));
            }

            node.children = childrenNodes.ToArray();
        }
        else
        {
            // Create voxel data
            node.children = null;
            node.voxels = new byte[nVoxels];

            int srcVoxelIndex;
            int dstVoxelIndex = 0;
            
            for (int z = voxelOffset.z; z < voxelOffset.z + voxelCount.z; z++)
            {
                for (int y = voxelOffset.y; y < voxelOffset.y + voxelCount.y; y++)
                {
                    srcVoxelIndex = z * (data.gridSize.x * data.gridSize.y) + y * data.gridSize.x + voxelOffset.x;

                    for (int x = voxelOffset.x; x < voxelOffset.x + voxelCount.x; x++)
                    {
                        node.voxels[dstVoxelIndex] = data.data[srcVoxelIndex];
                        srcVoxelIndex++;
                        dstVoxelIndex++;
                    }
                }
            }

            node.isFull = node.isVoxelFull();

            if ((isOccupancyMap) && (node.isFull))
            {
                // Don't need the voxel data itself, can discard it, just retain that the 
                // cell was completely full
                node.voxels = null;
            }
        }

        return node;
    }

    delegate bool ExecuteNodeFunction(Node node);

    void ExecuteOnNode(ExecuteNodeFunction callback)
    {
        if (rootNode == null) return;

        ExecuteOnNode(rootNode, callback);
    }

    void ExecuteOnNode(Node node, ExecuteNodeFunction callback)
    {
        if (!callback(node)) return;

        if (node.children != null)
        {
            for (int i = 0; i < node.children.Length; i++)
            {
                ExecuteOnNode(node.children[i], callback);
            }
        }
    }

    public void DrawGizmoBounds()
    {
        ExecuteOnNode((node) =>
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
            return true;
        });
    }

    public void DrawOccupancy()
    {
        ExecuteOnNode((node) =>
        {
            if (node.children == null)
            {
                if (node.isFull)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);

                    return false;
                }
                else
                {
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);

                    Vector3Int voxelPos;
                    for (int i = 0; i < node.voxels.Length; i++)
                    {
                        if (node.voxels[i] != 0)
                        {
                            voxelPos = node.GetVoxelPos(i);
                            Gizmos.color = Color.red;
                            Gizmos.DrawWireCube(rootNode.bounds.min + new Vector3(voxelSize.x * (voxelPos.x + 0.5f), voxelSize.y * (voxelPos.y + 0.5f), voxelSize.z * (voxelPos.z + 0.5f)), voxelSize);
                        }
                    }
                }
            }
            else
            {
                if (node.isFull)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
                    return false;
                }
                //Gizmos.color = Color.red;
                //Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
            }

            return true;
        });
    }
    
    public bool Intersect(Transform transform, VoxelTree otherVoxelTree, Transform otherTransform, ref OBB obb1, ref OBB obb2)
    {
        // Create a new transformation that converts local space of A to world space and then from world space to the local space of B
        var matrix = otherTransform.worldToLocalMatrix * transform.localToWorldMatrix;

        // Start running through the nodes
        return Intersect(rootNode, otherVoxelTree, matrix, ref obb1, ref obb2);
    }

    bool Intersect(Node node, VoxelTree otherVoxelTree, Matrix4x4 transformMatrix, ref OBB obb1, ref OBB obb2)
    {
        // Check first if this node is intersecting
        OBB nodeOBB = new OBB(node.bounds);
        nodeOBB.Transform(transformMatrix);

        if (!otherVoxelTree.Intersect(nodeOBB, ref obb2))
        {
            return false;
        }

        if (node.children == null)
        {
            // There are no children. This node is either full or has voxel data, otherwise it's an empty node and no intersection happens here
            if (node.isFull)
            {
                obb1 = new OBB(node.bounds);
                return true;
            }
            else if (node.voxels != null)
            {
                Vector3Int  voxelPos;
                OBB         voxelOBB;
                OBB         localVoxelOBB;

                for (int i = 0; i < node.voxels.Length; i++)
                {
                    // Check if this is an empty voxel
                    if (node.voxels[i] == 0) continue;

                    voxelPos = node.GetVoxelPos(i);
                    localVoxelOBB = new OBB(rootNode.bounds.min + new Vector3(voxelSize.x * (voxelPos.x + 0.5f), voxelSize.y * (voxelPos.y + 0.5f), voxelSize.z * (voxelPos.z + 0.5f)), voxelSize);
                    voxelOBB = new OBB(localVoxelOBB);
                    voxelOBB.Transform(transformMatrix);

                    if (otherVoxelTree.Intersect(voxelOBB, ref obb2))
                    {
                        obb1 = localVoxelOBB;
                        return true;
                    }
                }
            }
            return false;
        }
        else
        {
            // There are children, so we need to check all the children for an intersection
            foreach (var child in node.children)
            {
                if (Intersect(child, otherVoxelTree, transformMatrix, ref obb1, ref obb2))
                {
                    return true;
                }
            }
            return false;
        }
    }

    bool Intersect(OBB obb, ref OBB intersectionOBB)
    {
        return Intersect(rootNode, obb, ref intersectionOBB);
    }

    bool Intersect(Node node, OBB obb, ref OBB intersectionOBB)
    {
        if (node.children == null)
        {
            // There are no children. This node is either full or has voxel data, otherwise it's an empty node and no intersection happens here
            if (node.isFull)
            {
                OBB nodeOBB = new OBB(node.bounds);

                if (obb.Intersect(nodeOBB))
                {
                    intersectionOBB = nodeOBB;
                    return true;
                }
            }
            else if (node.voxels != null)
            {
                Vector3Int  voxelPos;
                OBB         voxelOBB;

                for (int i = 0; i < node.voxels.Length; i++)
                {
                    // Check if this is an empty voxel
                    if (node.voxels[i] == 0) continue;

                    voxelPos = node.GetVoxelPos(i);
                    voxelOBB = new OBB(rootNode.bounds.min + new Vector3(voxelSize.x * (voxelPos.x + 0.5f), voxelSize.y * (voxelPos.y + 0.5f), voxelSize.z * (voxelPos.z + 0.5f)), voxelSize);

                    if (obb.Intersect(voxelOBB))
                    {
                        intersectionOBB = voxelOBB;
                        return true;
                    }
                }
            }
            return false;
        }
        else
        {
            // There are children, so we need to check all the children for an intersection
            foreach (var child in node.children)
            {
                if (Intersect(child, obb, ref intersectionOBB))
                {
                    return true;
                }
            }
            return false;
        }
    }

    public OBB GetOBB()
    {
        return new OBB(rootNode.bounds);
    }

    public bool HasRootNode()
    {
        return (rootNode != null);
    }

    // Debug stuff
    /*
    Transform sourceTransform;
    Transform otherTransform;

    public bool IntersectWithGizmos(Transform transform, VoxelTree otherVoxelTree, Transform otherTransform, ref OBB obb1, ref OBB obb2)
    {
        // Create a new transformation that converts local space of A to world space and then from world space to the local space of B
        var matrix = otherTransform.worldToLocalMatrix * transform.localToWorldMatrix;

        this.sourceTransform = transform;
        this.otherTransform = otherTransform;

        otherVoxelTree.sourceTransform = transform;
        otherVoxelTree.otherTransform = otherTransform;

        // Start running through the nodes
        return IntersectWithGizmos(rootNode, otherVoxelTree, matrix, ref obb1, ref obb2);
    }

    bool IntersectWithGizmos(Node node, VoxelTree otherVoxelTree, Matrix4x4 transformMatrix, ref OBB obb1, ref OBB obb2)
    {
        // Check first if this node is intersecting
        OBB nodeOBB = new OBB(node.bounds);
        nodeOBB.Transform(transformMatrix);

        if (!otherVoxelTree.IntersectWithGizmos(nodeOBB, ref obb2))
        {
            // Failed this intersection, so draw it in red - This should draw in red all the tree nodes that were
            // tested and failed
            // Use the nodeOBB (should be in local space of other voxel tree), and use the otherTransform to map it from local to world
            var obb = new OBB(nodeOBB);
            obb.Transform(otherTransform.localToWorldMatrix);
            Gizmos.color = Color.red;
            obb.DrawGizmo();

            return false;
        }

        if (node.children == null)
        {
            // There are no children. This node is either full or has voxel data, otherwise it's an empty node and no intersection happens here
            if (node.isFull)
            {
                // Display node that hit the tree (full node) - Use the nodeOBB (should be in local space of other voxel tree), and use the 
                // otherTransform to map it from local to world
                var obb = new OBB(nodeOBB);
                obb.Transform(otherTransform.localToWorldMatrix);
                Gizmos.color = Color.red;
                obb.DrawGizmo();

                obb1 = new OBB(node.bounds);
                return true;
            }
            else if (node.voxels != null)
            {
                Vector3Int  voxelPos;
                OBB         voxelOBB;

                for (int i = 0; i < node.voxels.Length; i++)
                {
                    // Check if this is an empty voxel
                    if (node.voxels[i] == 0) continue;

                    // Check if this voxel is empty space or not
                    voxelPos = node.GetVoxelPos(i);
                    voxelOBB = new OBB(rootNode.bounds.min + new Vector3(voxelSize.x * (voxelPos.x + 0.5f), voxelSize.y * (voxelPos.y + 0.5f), voxelSize.z * (voxelPos.z + 0.5f)), voxelSize);
                    voxelOBB.Transform(transformMatrix);

                    if (otherVoxelTree.Intersect(voxelOBB, ref obb2))
                    {
                        // Voxel on source collided with something on other, draw it in green
                        var obb = new OBB(voxelOBB);
                        obb.Transform(otherTransform.localToWorldMatrix);
                        Gizmos.color = Color.green;
                        obb.DrawGizmo();

                        obb1 = new OBB(rootNode.bounds.min + new Vector3(voxelSize.x * (voxelPos.x + 0.5f), voxelSize.y * (voxelPos.y + 0.5f), voxelSize.z * (voxelPos.z + 0.5f)), voxelSize);
                        return true;
                    }
                }
            }
            return false;
        }
        else
        {
            // There are children, so we need to check all the children for an intersection
            foreach (var child in node.children)
            {
                if (IntersectWithGizmos(child, otherVoxelTree, transformMatrix, ref obb1, ref obb2))
                {
                    return true;
                }
            }
            return false;
        }
    }

    bool IntersectWithGizmos(OBB obb, ref OBB intersectionOBB)
    {
        return IntersectWithGizmos(rootNode, obb, ref intersectionOBB);
    }

    bool IntersectWithGizmos(Node node, OBB obb, ref OBB intersectionOBB)
    {
        if (node.children == null)
        {
            // There are no children. This node is either full or has voxel data, otherwise it's an empty node and no intersection happens here
            if (node.isFull)
            {
                OBB nodeOBB = new OBB(node.bounds);

                if (obb.Intersect(nodeOBB))
                {
                    // Full node - Display this voxel in dark green
                    var debugObb = new OBB(nodeOBB);
                    debugObb.Transform(otherTransform.localToWorldMatrix);
                    Gizmos.color = Color.yellow;
                    debugObb.DrawGizmo();

                    intersectionOBB = nodeOBB;
                    return true;
                }
            }
            else if (node.voxels != null)
            {
                Vector3Int voxelPos;
                OBB voxelOBB;

                for (int i = 0; i < node.voxels.Length; i++)
                {
                    // Check if this is an empty voxel
                    if (node.voxels[i] == 0) continue;

                    voxelPos = node.GetVoxelPos(i);
                    voxelOBB = new OBB(rootNode.bounds.min + new Vector3(voxelSize.x * (voxelPos.x + 0.5f), voxelSize.y * (voxelPos.y + 0.5f), voxelSize.z * (voxelPos.z + 0.5f)), voxelSize);

                    if (obb.Intersect(voxelOBB))
                    {
                        // Intersection at the voxel level - Display this voxel in green
                        var debugObb = new OBB(voxelOBB);
                        debugObb.Transform(otherTransform.localToWorldMatrix);
                        Gizmos.color = Color.cyan;
                        debugObb.DrawGizmo();

                        intersectionOBB = voxelOBB;
                        return true;
                    }
                }
            }
            return false;
        }
        else
        {
            // There are children, so we need to check all the children for an intersection
            foreach (var child in node.children)
            {
                if (IntersectWithGizmos(child, obb, ref intersectionOBB))
                {
                    return true;
                }
            }
            return false;
        }
    }*/
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class Quadtree<T> where T : class
    {
        protected Vector2   min;
        protected Vector2   max;
        protected int       nLevels;

        protected class Node
        {
            public Node     parent;
            public bool     isLeaf;
            public Bounds2d bounds;
            public List<T>  objects;
            public Node[]   children;
        }

        protected Node rootNode;

        public Quadtree(Vector2 min, Vector2 max, int nLevels)
        {
            this.min = Vector2.zero;
            this.max = Vector2.zero;
            if (min.x < max.x) { this.min.x = min.x; this.max.x = max.x; } else { this.min.x = max.x; this.max.x = min.x; }
            if (min.y < max.y) { this.min.y = min.y; this.max.y = max.y; } else { this.min.y = max.y; this.max.y = min.y; }
            this.nLevels = nLevels;

            rootNode = Init(new Bounds2d((this.min + this.max) * 0.5f, (this.max - this.min)), nLevels);
        }

        public void Add(Vector2 position, T value)
        {
            var leafNode = GetLeafNode(rootNode, position);
            if (leafNode != null)
            {
                leafNode.objects.Add(value);
            }
        }
        public void Add(float x, float y, T value) => Add(new Vector2(x, y), value);

        public void Add(T item, Func<T, Bounds2d, bool> intersects)
        {
            AddToNode(rootNode, item, intersects);
        }

        private void AddToNode(Node node, T item, Func<T, Bounds2d, bool> intersects)
        {
            // Skip nodes that don't intersect the item
            if (!intersects(item, node.bounds))
                return;

            if (node.isLeaf)
            {
                node.objects.Add(item);
            }
            else
            {
                foreach (var child in node.children)
                {
                    AddToNode(child, item, intersects);
                }
            }
        }

        public delegate float DistanceFuncWithPoint(Vector2 queryPoint, T obj, out Vector2 closestPoint);

        // If overlappingObjects, it assumes that if I find an objects that has negative distance (so the point is inside), I can't return immediately because some other object might have this point further inside.
        public T FindClosest(Vector2 point, DistanceFuncWithPoint distanceFunc, bool overlappingObjects, out float outDistance, out Vector2 outClosestPoint)
        {
            T bestObj = null;
            float bestDist = float.MaxValue;
            Vector2 bestPt = Vector2.zero;

            // Min‐heap (as a simple list) of (node, squaredMinDistToNode)
            var queue = new List<(Node node, float dist)>();
            queue.Add((rootNode, DistanceSqrToBounds(point, rootNode.bounds)));

            while (queue.Count > 0)
            {
                // Pop the node with smallest bound‐distance
                int minIndex = 0;
                for (int i = 1; i < queue.Count; i++)
                    if (queue[i].dist < queue[minIndex].dist)
                        minIndex = i;
                var (node, nodeDist) = queue[minIndex];
                queue.RemoveAt(minIndex);

                // Prune any subtree that can't beat our current best
                if (nodeDist >= bestDist)
                    break;

                if (node.isLeaf)
                {
                    foreach (var obj in node.objects)
                    {
                        // distanceFunc returns squared distance, and outputs the closest point on obj
                        Vector2 candidatePt;
                        float d = distanceFunc(point, obj, out candidatePt);
                        if (d < bestDist)
                        {
                            bestDist = d;
                            bestObj = obj;
                            bestPt = candidatePt;

                            if ((!overlappingObjects) && (d < 0))
                            {
                                outDistance = bestDist;
                                outClosestPoint = candidatePt;
                                return obj;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var child in node.children)
                    {
                        float dChild = DistanceSqrToBounds(point, child.bounds);
                        if (dChild < bestDist)
                            queue.Add((child, dChild));
                    }
                }
            }

            outClosestPoint = bestPt;
            outDistance = bestDist;
            return bestObj;
        }

        private static float DistanceSqrToBounds(Vector2 point, Bounds2d b)
        {
            Vector2 closest = b.ClosestPoint(point);
            return (closest - point).sqrMagnitude;
        }

        public void Remove(T value)
        {
            Remove(rootNode, value);
        }

        public void GetCandidateObjectsInSphere(Vector2 p, float radius, List<T> ret)
        {
            var seen = new HashSet<T>();
            GetCandidateObjectsInSphere(rootNode, p, radius, ret, seen);
        }

        private void GetCandidateObjectsInSphere(Node node, Vector2 p, float radius, List<T> ret, HashSet<T> seen)
        {
            bool includeNode = node.bounds.Contains(p);
            if (!includeNode)
            {
                float distSq = (node.bounds.ClosestPoint(p) - p).sqrMagnitude;
                if (distSq <= radius * radius)
                    includeNode = true;
            }

            if (!includeNode)
                return;

            if (node.isLeaf)
            {
                foreach (var obj in node.objects)
                {
                    if (seen.Add(obj))
                        ret.Add(obj);
                }
            }
            else
            {
                foreach (var child in node.children)
                    GetCandidateObjectsInSphere(child, p, radius, ret, seen);
            }
        }

        Node GetLeafNode(Node node, Vector2 p)
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

        Node Init(Bounds2d b, int nLevels, Node parent = null)
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
                n.children = new Node[4];

                Vector2 s = b.size * 0.5f;

                n.children[0] = Init(new Bounds2d(b.min + Vector2.right * s.x * 0.5f + Vector2.up * s.y * 0.5f, s), nLevels - 1, n);
                n.children[1] = Init(new Bounds2d(b.min + Vector2.right * s.x * 1.5f + Vector2.up * s.y * 0.5f, s), nLevels - 1, n);
                n.children[2] = Init(new Bounds2d(b.min + Vector2.right * s.x * 0.5f + Vector2.up * s.y * 1.5f, s), nLevels - 1, n);
                n.children[3] = Init(new Bounds2d(b.min + Vector2.right * s.x * 1.5f + Vector2.up * s.y * 1.5f, s), nLevels - 1, n);
            }

            return n;
        }
    }
}
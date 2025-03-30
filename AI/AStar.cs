using System.Collections.Generic;
using UnityEngine;

public static class AStar
{
    private class Node
    {
        public Vector2Int Position;
        public Node Parent;
        public int G; // Cost from start
        public int H; // Heuristic cost to end
        public int F => G + H;

        public Node(Vector2Int position, Node parent, int g, int h)
        {
            Position = position;
            Parent = parent;
            G = g;
            H = h;
        }
    }

    public static List<Vector2Int> GetPath(int[,] grid, Vector2Int startPos, Vector2Int endPos)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        HashSet<Vector2Int> closedSet = new HashSet<Vector2Int>();
        Dictionary<Vector2Int, Node> openSet = new Dictionary<Vector2Int, Node>();
        PriorityQueue<Node, int> priorityQueue = new();

        int gridWidth = grid.GetLength(0);
        int gridHeight = grid.GetLength(1);

        Node startNode = new Node(startPos, null, 0, Heuristic(startPos, endPos));
        openSet[startPos] = startNode;
        priorityQueue.Enqueue(startNode, startNode.F);

        while (priorityQueue.Count > 0)
        {
            Node current = priorityQueue.Dequeue();

            if (current.Position == endPos)
            {
                return ReconstructPath(current);
            }

            openSet.Remove(current.Position);
            closedSet.Add(current.Position);

            foreach (Vector2Int neighbor in GetNeighbors(current.Position, grid, gridWidth, gridHeight))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                int tentativeG = current.G + 1;

                if (!openSet.ContainsKey(neighbor) || tentativeG < openSet[neighbor].G)
                {
                    Node neighborNode = new Node(neighbor, current, tentativeG, Heuristic(neighbor, endPos));
                    openSet[neighbor] = neighborNode;
                    priorityQueue.Enqueue(neighborNode, neighborNode.F);
                }
            }
        }

        return path; // No path found
    }

    private static List<Vector2Int> ReconstructPath(Node node)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        while (node != null)
        {
            path.Add(node.Position);
            node = node.Parent;
        }
        path.Reverse();
        return path;
    }

    private static int Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y); // Manhattan distance
    }

    private static List<Vector2Int> GetNeighbors(Vector2Int position, int[,] grid, int gridWidth, int gridHeight)
    {
        List<Vector2Int> neighbors = new List<Vector2Int>();
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighbor = position + dir;
            if (neighbor.x >= 0 && neighbor.x < gridWidth && neighbor.y >= 0 && neighbor.y < gridHeight && grid[neighbor.x, neighbor.y] == 0)
            {
                neighbors.Add(neighbor);
            }
        }
        return neighbors;
    }
}

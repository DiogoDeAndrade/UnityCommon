using System.Collections.Generic;
using UC;
using UnityEngine;

public static class ColliderExtensions
{
    public static void SetSprite(this PolygonCollider2D polygonCollider, Sprite sprite)
    {
        // Clear existing paths
        polygonCollider.pathCount = 0;

        // Set new paths from the sprite's physics shape
        int pathCount = sprite.GetPhysicsShapeCount();
        polygonCollider.pathCount = pathCount;

        List<Vector2> path = new();

        for (int i = 0; i < pathCount; i++)
        {
            path.Clear();
            sprite.GetPhysicsShape(i, path);
            polygonCollider.SetPath(i, path);
        }
    }

    public static Vector2 Random(this BoxCollider2D boxCollider)
    {
        return boxCollider.bounds.Random();
    }
}

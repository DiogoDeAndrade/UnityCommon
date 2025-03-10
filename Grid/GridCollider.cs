using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(GridObject))]
public class GridCollider : MonoBehaviour
{
    GridSystem      gridSystem;
    SpriteRenderer  spriteRenderer;
    TilemapRenderer tilemapRenderer;

    void OnEnable()
    {
        gridSystem = GetComponentInParent<GridSystem>();
        gridSystem.Register(this);
        spriteRenderer = GetComponent<SpriteRenderer>();
        tilemapRenderer = GetComponent<TilemapRenderer>();
    }

    void OnDisable()
    {
        gridSystem = GetComponentInParent<GridSystem>();
        gridSystem.Unregister(this);
    }
}

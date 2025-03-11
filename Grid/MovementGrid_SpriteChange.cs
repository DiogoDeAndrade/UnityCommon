using System;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(GridObject))]
public class MovementGrid_SpriteChange : MonoBehaviour
{
    [SerializeField] private Sprite spriteLeft;
    [SerializeField] private Sprite spriteRight;
    [SerializeField] private Sprite spriteUp;
    [SerializeField] private Sprite spriteDown;

    SpriteRenderer  spriteRenderer;
    GridObject      gridObject;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        gridObject = GetComponent<GridObject>();

        gridObject.onTurnTo += ChangeSprite;
    }

    private void ChangeSprite(Vector2Int sourcePos, Vector2Int destPos)
    {
        int currentDir = -1;
        Vector2 currentDelta = destPos - sourcePos;

        if (Mathf.Abs(currentDelta.x) < Mathf.Abs(currentDelta.y))
        {
            // More movement in Y than X
            if (currentDelta.y > 0.0f)
            {
                currentDir = 2;
            }
            else if (currentDelta.y < 0.0f)
            {
                currentDir = 0;
            }
        }
        else
        {
            if (currentDelta.x > 0.0f)
            {
                currentDir = 3;
            }
            else if (currentDelta.x < 0.0f)
            {
                currentDir = 1;
            }
        }

        switch (currentDir)
        {
            case 0: spriteRenderer.sprite = spriteDown; break;
            case 1: spriteRenderer.sprite = spriteLeft; break;
            case 2: spriteRenderer.sprite = spriteUp; break;
            case 3: spriteRenderer.sprite = spriteRight; break;
        }
    }
}

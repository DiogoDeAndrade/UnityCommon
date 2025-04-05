using UnityEngine;

namespace UC
{

    [RequireComponent(typeof(GridObject))]
    public class MovementGrid_SpriteChange : MonoBehaviour
    {
        [SerializeField] private Sprite spriteLeft;
        [SerializeField] private Sprite spriteRight;
        [SerializeField] private Sprite spriteUp;
        [SerializeField] private Sprite spriteDown;

        SpriteRenderer spriteRenderer;
        GridObject gridObject;

        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            gridObject = GetComponent<GridObject>();

            gridObject.onTurnTo += ChangeSprite;
        }

        private void ChangeSprite(Vector2Int sourcePos, Vector2Int destPos)
        {
            int currentDir = gridObject.GetFacingDirection();

            switch (currentDir)
            {
                case 0: spriteRenderer.sprite = spriteDown; break;
                case 1: spriteRenderer.sprite = spriteLeft; break;
                case 2: spriteRenderer.sprite = spriteUp; break;
                case 3: spriteRenderer.sprite = spriteRight; break;
            }
        }
    }
}
using UnityEngine;

namespace UC
{

    [RequireComponent(typeof(GridObject))]
    public class MovementGrid_SpriteChange : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [SerializeField] private Sprite spriteLeft;
        [SerializeField] private bool   flipLeft;
        [SerializeField] private Sprite spriteRight;
        [SerializeField] private bool   flipRight;
        [SerializeField] private Sprite spriteUp;
        [SerializeField] private bool   flipUp;
        [SerializeField] private Sprite spriteDown;
        [SerializeField] private bool   flipDown;

        GridObject gridObject;

        void Start()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            gridObject = GetComponent<GridObject>();

            gridObject.onTurnTo += ChangeSprite;
        }

        private void ChangeSprite(Vector2Int sourcePos, Vector2Int destPos)
        {
            int currentDir = gridObject.GetFacingDirection();

            switch (currentDir)
            {
                case 0: if (spriteDown) spriteRenderer.sprite = spriteDown; spriteRenderer.flipY = flipDown; break;
                case 1: if (spriteLeft) spriteRenderer.sprite = spriteLeft; spriteRenderer.flipX = flipLeft; break;
                case 2: if (spriteUp) spriteRenderer.sprite = spriteUp; spriteRenderer.flipY = flipUp; break;
                case 3: if (spriteRight) spriteRenderer.sprite = spriteRight; spriteRenderer.flipX = flipRight; break;
            }
        }

        public void SetSpriteRenderer(SpriteRenderer spriteRenderer)
        {
            this.spriteRenderer = spriteRenderer;
        }
    }
}
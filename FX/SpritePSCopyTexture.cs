using NaughtyAttributes;
using UnityEngine;

namespace UC
{

    public class SpritePSCopyTexture : MonoBehaviour
    {
        [SerializeField] private bool dynamic;

        void Start()
        {
            CopyTexture();

            if (!dynamic)
            {
                Destroy(this);
            }
        }

        // Update is called once per frame
        void Update()
        {
            CopyTexture();
        }

        [Button("Run Once")]
        void CopyTexture()
        {
            var ps = GetComponent<ParticleSystem>();

            var shape = ps.shape;
            if ((shape.shapeType == ParticleSystemShapeType.Sprite) ||
                (shape.shapeType == ParticleSystemShapeType.SpriteRenderer))
            {
                if (shape.spriteRenderer != null)
                {
                    shape.texture = shape.spriteRenderer.sprite.texture;
                }
                else if (shape.sprite != null)
                {
                    shape.texture = shape.sprite.texture;
                }
            }
        }
    }
}
using NaughtyAttributes;
using System.Collections;
using UnityEngine;

namespace UC
{

    public class SpritePSCopyTexture : MonoBehaviour
    {
        [SerializeField] private bool dynamic;
        [SerializeField,HideIf(nameof(dynamic))] private bool waitOneFrame;

        void Start()
        {
            if (dynamic) return;

            if (waitOneFrame)
            {
                StartCoroutine(CopyTextureCR());
            }
            else
            {
                CopyTexture();
                Destroy(this);
            }
        }

        IEnumerator CopyTextureCR()
        {
            yield return null;
            CopyTexture();
            Destroy(this);
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
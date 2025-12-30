using System;
using UnityEngine;

namespace UC.RPG
{
    [Serializable]
    [PolymorphicName("RPG/Visuals Module")]
    public class RPGVisualsModule: SOModule
    {
        public GameObject                   gfxElementPrefab = null;
        public Vector2                      gfxOffset = Vector2.zero;
        public Sprite                       _displaySprite;
        public Color                        highlightColor = Color.white;
        public bool                         enableHitFlash = true;
        public RuntimeAnimatorController    controller;
        public bool                         enableShadow = true;
        public Vector2                      shadowScale = Vector2.one;
        [Header("UI")]
        public EntityPanel.DataChannel      uiChannel = EntityPanel.AllDataChannels;

        public void InitGraphics(GameObject gameObject)
        {
            // Check if there is already a graphics object
            var gfx = RPGConfig.gfxTag.FindIn(gameObject);
            if ((gfx) && (gfxElementPrefab != null))
            {
                // Delete existing one, remove it first from the hierarchy so it doesn't influence GetComponents before the end of the frame
                gfx.transform.SetParent(null);
                GameObject.Destroy(gfx);
            }

            // Spawn object
            if (gfxElementPrefab)
            {
                gfx = GameObject.Instantiate(gfxElementPrefab, gameObject.transform);
            }

            gfx.transform.localPosition = gfxOffset;

            if (controller)
            {
                var animator = gfx.GetComponent<Animator>();
                if (animator)
                {
                    animator.runtimeAnimatorController = controller;
                }
            }

            // Find shadow object
            var shadowGfx = RPGConfig.gfxShadowTag.FindAllIn<SpriteRenderer>(gameObject);
            if (shadowGfx != null)
            {
                foreach (var sg in shadowGfx)
                {
                    sg.enabled = enableShadow;
                    sg.transform.localScale = shadowScale.xy1();
                }
            }
        }
    }
}

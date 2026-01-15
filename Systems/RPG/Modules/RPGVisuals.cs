using NaughtyAttributes;
using System;
using UnityEngine;

namespace UC.RPG
{
    [Serializable]
    [PolymorphicName("RPG/Visuals Module")]
    public class RPGVisualsModule: SOModule
    {
        public RPGGfx                       gfxElementPrefab = null;
        public Vector2                      gfxOffset = Vector2.zero;
        public Sprite                       _displaySprite;
        public Color                        highlightColor = Color.white;
        public bool                         enableHitFlash = true;
        public bool                         enableShadow = true;
        public Vector2                      shadowScale = Vector2.one;
        [Header("Animation")]
        public RuntimeAnimatorController    controller;
        public bool                         desynchAnimation = true;
        [MinMaxSlider(0.1f, 2.0f)]
        public Vector2                      animationSpeed = Vector2.one;
        [Header("UI")]
        public DataChannel                  uiChannel = DataChannel.All;

        public void InitGraphics(GameObject gameObject)
        {
            // Check if there is already a graphics object
            var gfx = gameObject.GetComponentInChildren<RPGGfx>();
            if ((gfx) && (gfxElementPrefab != null))
            {
                // Delete existing one, remove it first from the hierarchy so it doesn't influence GetComponents before the end of the frame
                gfx.transform.SetParent(null);
                GameObject.Destroy(gfx.gameObject);
            }

            // Spawn object
            if (gfxElementPrefab)
            {
                gfx = GameObject.Instantiate(gfxElementPrefab, gameObject.transform);
            }

            gfx.transform.localPosition = gfxOffset;

            if (controller)
            {
                gfx.SetAnimator(controller);
            }

            gfx.SetShadow(enableShadow, shadowScale.xy1());            
        }
    }
}

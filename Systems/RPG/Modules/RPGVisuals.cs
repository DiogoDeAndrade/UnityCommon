using System;
using UC.Interaction;
using UC.RPG;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Visuals Module")]
    public class RPGVisualsModule: SOModule
    {
        public Sprite                       _displaySprite;
        public Color                        highlightColor = Color.white;
        public RuntimeAnimatorController    controller;
        public EntityPanel.DataChannel      uiChannel = EntityPanel.AllDataChannels;
    }
}

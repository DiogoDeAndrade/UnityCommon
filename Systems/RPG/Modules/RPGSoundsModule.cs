using System;
using UC.Interaction;
using UC.RPG;
using UnityEngine;

namespace UC.RPG
{

    [Serializable]
    [PolymorphicName("RPG/Sounds Module")]
    public class RPGSoundsModule : SOModule
    {
        public SoundDef    hitSound;
        public SoundDef    deathSound;
    }
}

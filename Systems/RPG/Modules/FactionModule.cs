using System;

namespace UC.RPG
{
    [Serializable]
    [PolymorphicName("RPG/Faction Module")]
    public class FactionModule : SOModule
    {
        public Faction   faction = null;
    }
}

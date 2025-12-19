using UnityEngine;

namespace UC.RPG
{
    public enum ChangeType { Burst, OverTime };

    public partial class ChangeData
    {
        public ChangeData() { }
        public ChangeData(float value) { deltaValue = value; }

        public ChangeType       changeType = ChangeType.Burst;
        public float            deltaValue = 0.0f;
        public Vector3          changeSrcPosition = Vector3.zero;
        public Vector3          changeSrcDirection = Vector3.zero;
        public GameObject       source = null;
        public RPGChangeData    rpgChangeData = null;

        public ChangeData Clone()
        {
            var copy = (ChangeData)MemberwiseClone();
            if (rpgChangeData != null)
            {
                copy.rpgChangeData = rpgChangeData.Clone();
            }
            return copy;
        }
    }

    // Provides additional data for RPG system
    public partial class RPGChangeData
    {
        public bool         isCrit = false;
        public float        critMultiplier = 1.0f;
        public bool         isMiss = false;
        public float        blocked = 0.0f;
        public float        resisted = 0.0f;
        public RPGEntity    weapon;
        public RPGEntity    srcEntiy;
        public RPGEntity    targetEntiy;
        public DamageType   damageType;
        public ResourceType resourceType;

        public RPGChangeData Clone()
        {
            return (RPGChangeData)MemberwiseClone();
        }
    }
}

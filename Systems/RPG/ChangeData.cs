using UnityEngine;

namespace UC.RPG
{
    public enum ChangeType { Burst, OverTime };

    public partial class ChangeData
    {
        public ChangeType changeType = ChangeType.Burst;
        public float deltaValue = 0.0f;
        public Vector3 changeSrcPosition = Vector3.zero;
        public Vector3 changeSrcDirection = Vector3.zero;
        public GameObject source = null;
    }
}

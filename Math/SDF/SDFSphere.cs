using System;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class SDFSphere : SDF
    {
        public Vector3  offset;
        public float    radius;

        public override Bounds GetBounds()
        {
            var ret = new Bounds(offset, Vector3.one * radius * 2.0f);
#if UNITY_6000_0_OR_NEWER
            ret = ret.ToWorld(ownerGameObject.transform);
#endif
            return ret;
        }

        public override float Sample(Vector3 worldPoint)
        {
            return Vector3.Distance(offset, ToLocalPoint(worldPoint)) - radius;
        }

#if UNITY_6000_0_OR_NEWER
        public override void DrawGizmos()
        {
            Gizmos.matrix = ownerGameObject.transform.localToWorldMatrix;
            Gizmos.DrawSphere(offset, radius);
        }
#endif
    }
}

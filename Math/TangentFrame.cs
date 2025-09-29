using System;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class TangentFrame
    {
        public Vector3 center;
        public Vector3 dir;
        public Vector3 up;
        public Vector3 right;

        public Vector2 WorldToFrame(Vector3 worldPoint)
        {
            Vector3 rel = worldPoint - center;
            Vector2 q2 = new Vector2(Vector3.Dot(rel, right), Vector3.Dot(rel, up));

            return q2;
        }

        public Vector3 FrameToWorld(Vector2 pt)
        {
            return center + pt.x * right + pt.y * up;
        }

    }
}

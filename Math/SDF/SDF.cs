using NaughtyAttributes;
using System;
#if UNITY_6000_0_OR_NEWER
using UnityEngine;
#endif

namespace UC
{
    [Serializable]
    public abstract class SDF
#if UNITY_6000_0_OR_NEWER
        : ScriptableObject
#endif
    {
        public virtual bool needArgs => false;
        public virtual SDF[] args
        {
            get { return null; }
            set { args = null; }
        }
        public abstract Bounds GetBounds();
        public abstract float Sample(Vector3 worldPoint);

#if UNITY_6000_0_OR_NEWER
        public Vector3 ToLocalPoint(Vector3 worldPoint) => ownerGameObject.transform.InverseTransformPoint(worldPoint);
#else
        public Vector3 ToLocalPoint(Vector3 worldPoint) => worldPoint;
#endif

#if UNITY_6000_0_OR_NEWER 
        public GameObject ownerGameObject;

#if UNITY_EDITOR
        public abstract void DrawGizmos();
#endif

#endif
    }
}
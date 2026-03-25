using System;
using UnityEngine;

namespace UC.DoubleMath
{
    public static class DoubleExtensions 
    {
        public static DVector3 ToDVector3(this Vector3 v) => new DVector3((double)v.x, (double)v.y, (double)v.z);
    }
}
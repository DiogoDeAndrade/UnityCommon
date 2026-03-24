using System;
using UnityEngine;

namespace UC.DoubleMath
{
    public static class DoubleExtensions 
    {
        public static DoubleVector3 ToDoubleVector3(this Vector3 v) => new DoubleVector3((double)v.x, (double)v.y, (double)v.z);
    }
}
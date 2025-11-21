using System;
using UnityEngine;

namespace UC
{
    [Serializable]
    public class CursorDef
    {
        public Sprite   cursor;
        public Color    color = Color.white;
        public Vector2  size = Vector2.zero;
    }
}

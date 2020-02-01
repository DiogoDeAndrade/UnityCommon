using System.Collections;
using System.Collections.Generic;
using UnityEngine;

static public class LayerMaskExtensions
{
    public static bool HasLayer(this LayerMask layerMask, int layer)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
}

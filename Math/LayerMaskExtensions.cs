using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LayerMaskExtensions
{
    public static bool HasLayer(this LayerMask layerMask, int layer)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
}

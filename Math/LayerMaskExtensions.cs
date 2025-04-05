using UnityEngine;

namespace UC
{

    public static class LayerMaskExtensions
    {
        public static bool HasLayer(this LayerMask layerMask, int layer)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }
    }
}
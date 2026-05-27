using UnityEngine;

namespace UC
{
    public interface ITooltip
    {
        RectTransform GetTooltip(RectTransform parentTransform);
        int GetOrder();
    }
}

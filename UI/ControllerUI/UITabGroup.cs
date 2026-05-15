using UnityEngine;

namespace UC
{
    public class UITabGroup : UIGroup
    {
        protected override void NotifyEnableUI(bool value)
        {
            base.NotifyEnableUI(value);

            if (!value)
            {
                var allGroups = GetComponentsInChildren<UIGroup>(true);
                foreach (var group in allGroups)
                {
                    if (group != this)
                    {
                        group.EnableUI(false);
                    }
                }
            }
        }
    }
}

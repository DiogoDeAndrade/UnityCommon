using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class GridAction_Teleport : GridActionContainer
    {
        [SerializeField]
        private Hypertag objectToTeleportTag;
        [SerializeField]
        private Hypertag targetLocationTag;

        public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
        {
            if (objectToTeleportTag)
            {
                var targetObj = Hypertag.FindFirstObjectWithHypertag<GridObject>(objectToTeleportTag);
                if (targetObj == null) return;
            }
            if (targetLocationTag)
            {
                var targetPos = Hypertag.FindFirstObjectWithHypertag<Transform>(targetLocationTag);
                if (targetPos == null) return;

                retActions.Add(new NamedAction
                {
                    name = verb,
                    action = RunAction,
                    container = this
                });
            }
        }

        protected bool RunAction(GridObject subject, Vector2Int position)
        {
            GridObject targetObj = null;
            if (objectToTeleportTag)
            {
                targetObj = Hypertag.FindFirstObjectWithHypertag<GridObject>(objectToTeleportTag);
                if (targetObj == null) return false;
            }
            else
            {
                targetObj = subject;
            }

            if (targetLocationTag)
            {
                var targetPos = Hypertag.FindFirstObjectWithHypertag<Transform>(targetLocationTag);
                if (targetPos == null) return false;

                targetObj.TeleportTo(targetPos.transform.position);
            }

            return false;
        }
    }
}
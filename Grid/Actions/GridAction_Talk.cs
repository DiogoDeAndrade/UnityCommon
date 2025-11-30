using System.Collections.Generic;
using UnityEngine;

namespace UC
{

    public class GridAction_Talk : GridActionContainer
    {
        [SerializeField, DialogueKey] private string dialogueKey;

        public override void ActualGatherActions(GridObject subject, Vector2Int position, List<NamedAction> retActions)
        {
            if (DialogueManager.HasDialogue(dialogueKey))
            {
                retActions.Add(new NamedAction
                {
                    name = verb,
                    action = RunAction,
                    container = this
                });
            }
        }

        protected bool RunAction(NamedAction namedAction, GridObject subject, Vector2Int position)
        {
            DialogueManager.StartConversation(dialogueKey);

            return true;
        }

        public override bool ShouldRunTurn()
        {
            return false;
        }
    }
}
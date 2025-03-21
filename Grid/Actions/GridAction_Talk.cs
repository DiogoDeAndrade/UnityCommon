using System.Collections.Generic;
using UnityEngine;

public class GridAction_Talk : GridAction
{
    [SerializeField, DialogueKey] private string dialogueKey;

    protected override void ActualGatherActions(GridObject subject, Vector2Int position, List<GridAction> actions)
    {
        if (DialogueManager.HasDialogue(dialogueKey))
        {
            actions.Add(this);
        }
    }

    protected override bool ActualRunAction(GridObject subject, Vector2Int position)
    {
        DialogueManager.StartConversation(dialogueKey);

        return true;
    }

    public override bool ShouldRunTurn() 
    { 
        return false; 
    }
}

using UnityEngine;

namespace UC
{
    // Turns a string field into a dropdown of dialogue keys. With no argument every key in every
    // DialogueData in the project is offered; given the name of a sibling DialogueData field, only
    // that dialogue's keys are - so a key can't point into a dialogue the owner doesn't use.
    //
    //   [SerializeField] private DialogueData dialogue;
    //   [SerializeField, DialogueKey(nameof(dialogue))] private string key;
    public class DialogueKeyAttribute : PropertyAttribute
    {
        public string dialogueField { get; }

        public DialogueKeyAttribute()
        {
        }

        public DialogueKeyAttribute(string dialogueField)
        {
            this.dialogueField = dialogueField;
        }
    }

}
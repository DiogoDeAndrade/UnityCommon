using System;
using UnityEngine;

namespace UC
{

    public abstract class DialogueOption : MonoBehaviour
    {
        // available=false is a ShowInvalid option whose condition failed: render it visibly
        // unavailable - it can't be selected, only looked at
        public abstract void Show(string text, bool available);
        public abstract void Hide();
        public abstract void Select();
        public abstract void Deselect();
    }
}
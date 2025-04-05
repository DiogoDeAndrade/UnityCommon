using System;
using UnityEngine;

namespace UC
{

    public abstract class DialogueOption : MonoBehaviour
    {
        public abstract void Show(string text);
        public abstract void Hide();
        public abstract void Select();
        public abstract void Deselect();
    }
}
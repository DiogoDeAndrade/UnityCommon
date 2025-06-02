using Unity.VisualScripting;
using UnityEngine;

static public class AnimatorExtensions
{
    static public bool HasParameter(this Animator animator, string paramName)
    {
        for (int i = 0; i < animator.parameterCount; i++)
        {
            if (animator.GetParameter(i).name == paramName)
            {
                return true;
            }
        }

        return false;
    }
}

using UC;
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

    static public bool HasParameter(this Animator animator, int paramHash)
    {
        for (int i = 0; i < animator.parameterCount; i++)
        {
            if (animator.GetParameter(i).nameHash == paramHash)
            {
                return true;
            }
        }

        return false;
    }

    public static bool GetParameterByHash(this Animator animator, int hash, out AnimatorControllerParameterType parameterType)
    {
        foreach (var p in animator.parameters)
        {
            if (p.nameHash == hash)
            {
                parameterType = p.type;
                return true;
            }
        }
        parameterType = AnimatorControllerParameterType.Trigger;

        return false;
    }

    public static Tweener.BaseInterpolator ChangeLayerWeight(this Animator animator, string name, float targetWeight, float duration, string animName = null)
    {
        var n = (string.IsNullOrEmpty(animName)) ? ($"ChangeLayerWeight{name}") : animName;
        animator.Tween().Stop(n, Tweener.StopBehaviour.SkipToEnd);

        var layerIndex = animator.GetLayerIndex(name);
        var current = animator.GetLayerWeight(layerIndex);

        if (current == targetWeight) return null;

        return animator.Tween().Interpolate(current, targetWeight, duration, (value) => animator.SetLayerWeight(layerIndex, value), n);
    }
}

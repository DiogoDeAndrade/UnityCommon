using NaughtyAttributes;
using System;
using UC;
using UnityEngine;

public class AnimationRunEvent : StateMachineBehaviour
{
    [Flags]
    public enum EventType { OnEnter = 1, OnUpdate = 2, OnExit = 4, OnAfterMove = 8, OnAfterIK = 16 };
    public enum TargetType { Self, GlobalHypertag, ParentHypertag, ChildHypertag };

    [Flags]
    public enum Filter { Type = 1}


    [SerializeField] 
    private EventType       type;
    [SerializeField] 
    private TargetType      targetType;
    [SerializeField, FunctionCallOptions(false, true, typeof(Animator), typeof(AnimatorStateInfo), typeof(int))]
    private FunctionCall    functionCall;
    [SerializeField, ShowIf(nameof(needTag))] 
    private Hypertag        hypertag;
    [SerializeField]
    private Filter          filters; 
    [SerializeField, ShowIf(nameof(needFilter))]
    private string          filterByType;

    bool needTag => (targetType == TargetType.GlobalHypertag) || (targetType == TargetType.ParentHypertag) || (targetType == TargetType.ChildHypertag);
    bool needFilter => (filters & Filter.Type) != 0;

    // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if ((type & EventType.OnEnter) == 0) return;

        CallFunction(animator, stateInfo, layerIndex);
    }

    // OnStateUpdate is called on each Update frame between OnStateEnter and OnStateExit callbacks
    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if ((type & EventType.OnUpdate) == 0)  return;

        CallFunction(animator, stateInfo, layerIndex);
    }

    // OnStateExit is called when a transition ends and the state machine finishes evaluating this state
    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if ((type & EventType.OnExit) == 0)  return;

        CallFunction(animator, stateInfo, layerIndex);
    }

    // OnStateMove is called right after Animator.OnAnimatorMove()
    override public void OnStateMove(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if ((type & EventType.OnAfterMove) == 0)  return;

        CallFunction(animator, stateInfo, layerIndex);
    }

    // OnStateIK is called right after Animator.OnAnimatorIK()
    override public void OnStateIK(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if ((type & EventType.OnAfterIK) == 0)  return;

        CallFunction(animator, stateInfo, layerIndex);
    }

    void CallFunction(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        GameObject gameObject = GetObject(animator);
        if (gameObject == null)
        {
            Debug.LogWarning($"[AnimationRunEvent] No receiver for animation event {type} (object {targetType}/{hypertag} not found on animator {animator.name})", animator);
            return;
        }

        functionCall.Invoke(gameObject, animator, stateInfo, layerIndex);
    }

    GameObject GetObject(Animator animator)
    {
        switch (targetType)
        {
            case TargetType.Self:
                return animator.gameObject;
            case TargetType.GlobalHypertag:
                return hypertag.FindFirstGameObject();
            case TargetType.ParentHypertag:
                if (animator.HasHypertag(hypertag)) return animator.gameObject;
                return animator.GetComponentInParentWithHypertag<Transform>(hypertag)?.gameObject;
            case TargetType.ChildHypertag:
                if (animator.HasHypertag(hypertag)) return animator.gameObject;
                return animator.GetComponentInChildrenWithHypertag<Transform>(hypertag)?.gameObject;
            default:
                break;
        }

        return null;
    }
}

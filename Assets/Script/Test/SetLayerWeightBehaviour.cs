using UnityEngine;

public class SetLayerWeightBehaviour : StateMachineBehaviour
{
    public float enterWeight = 1f;
    public float exitWeight = 0f;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetLayerWeight(layerIndex, enterWeight);
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animator.SetLayerWeight(layerIndex, exitWeight);
    }
}

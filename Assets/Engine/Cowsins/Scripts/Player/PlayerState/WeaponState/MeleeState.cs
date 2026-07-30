using UnityEngine;
using System.Collections; 

namespace cowsins
{
    public class MeleeState : WeaponBaseState
    {
        private WeaponController controller;
        private WeaponAnimator weaponAnimator;

        private float timer;
        private bool isSwitchQueued = false;

        private Animator meleeObject;

        private float animationDuration, holsterAnimationDuration;

        public MeleeState(WeaponStates currentContext, WeaponStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory)
        {
            controller = _ctx.WeaponController;
            weaponAnimator = _ctx.WeaponAnimator;
            meleeObject = controller.settings.meleeObject.GetComponent<Animator>(); 
        }

        public sealed override void EnterState()
        {
            timer = 0;
            isSwitchQueued = false;
            controller.quickActionBehaviour.SecondaryMeleeAttack();
            CowsinsUtilities.PlayAnim("hit", _ctx.WeaponAnimator.HolsterMotionObject);
        }

        public sealed override void UpdateState()
        {
            controller.aimBehaviour?.Exit();
            CheckSwitchState();
        }

        public sealed override void FixedUpdateState()
        {
        }

        public sealed override void ExitState()
        {
            CowsinsUtilities.PlayAnim("finished", _ctx.WeaponAnimator.HolsterMotionObject);
        }

        public sealed override void CheckSwitchState()
        {
            timer += Time.deltaTime;
            AnimatorStateInfo stateInfo = meleeObject.GetCurrentAnimatorStateInfo(0);
            animationDuration = stateInfo.length;
 
            if (!isSwitchQueued && timer >= animationDuration + holsterAnimationDuration + controller.settings.meleeDelay)
            {
                controller.quickActionBehaviour.FinishMelee();

                AnimatorStateInfo holsterStateInfo = weaponAnimator.HolsterMotionObject.GetCurrentAnimatorStateInfo(0);
                holsterAnimationDuration = holsterStateInfo.length;

                _ctx.StartCoroutine(DelayedStateSwitch(holsterAnimationDuration));
                isSwitchQueued = true;
            }
        }

        private IEnumerator DelayedStateSwitch(float delay)
        {
            yield return new WaitForSeconds(delay);
            SwitchState(_factory.Default());
        }
    }
}

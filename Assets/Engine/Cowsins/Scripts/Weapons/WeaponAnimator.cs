using UnityEngine;

namespace cowsins
{
    public class WeaponAnimator : MonoBehaviour
    {
        [SerializeField] private CameraAnimations cameraAnimations;

        [SerializeField] private Animator holsterMotionObject;

        public Animator HolsterMotionObject => holsterMotionObject;

        private PlayerDependencies playerDependencies;
        private WeaponStates weaponStates;
        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IPlayerMovementEventsProvider playerEvents; // IPlayerMovementEventsProvider is implemented in PlayerMovement.cs
        private IWeaponReferenceProvider weaponController; // IWeaponReferenceProvider is implemented in WeaponController.cs
        private IWeaponBehaviourProvider weaponBehaviour; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        private IWeaponEventsProvider weaponEvents;// IWeaponEventsProvider is implemented in WeaponController.cs
        private IInteractManagerProvider interactManager; // IWeaponReferenceProvider is implemented in InteractManager.cs

        private void Start()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            weaponStates = GetComponent<WeaponStates>();
            player = playerDependencies.PlayerMovementState;
            playerEvents = playerDependencies.PlayerMovementEvents;
            weaponController = playerDependencies.WeaponReference;
            weaponBehaviour = playerDependencies.WeaponBehaviour;
            weaponEvents = playerDependencies.WeaponEvents;
            interactManager = playerDependencies.InteractManager;

            playerEvents.Events.OnClimbStart.AddListener(TryHideWeapon);
            playerEvents.Events.OnClimbStop.AddListener(TryShowWeapon);

            weaponEvents.Events.OnUnholster.AddListener(OnUnholster);
            weaponEvents.Events.OnSecondaryAttack.AddListener(SetParentConstraintSource);
            weaponEvents.Events.OnStartReload.AddListener(StartReload); 
        }

        private void OnDestroy()
        {
            playerEvents.Events.OnClimbStart.RemoveListener(TryHideWeapon);
            playerEvents.Events.OnClimbStop.RemoveListener(TryShowWeapon);

            weaponEvents.Events.OnUnholster.RemoveListener(OnUnholster);
            weaponEvents.Events.OnSecondaryAttack.RemoveListener(SetParentConstraintSource);
            weaponEvents.Events.OnStartReload.RemoveListener(StartReload);
        }

        private void Update()
        {
            if (weaponController.Id == null) return;

            Animator currentAnimator = weaponController.Id.Animator;

            if (player.IsWallRunning && !weaponBehaviour.IsReloading)
            {
                CowsinsUtilities.PlayAnim("walking", currentAnimator);
                return;

            }
            if (weaponBehaviour.IsReloading || player.IsCrouching || !player.Grounded || player.IsIdle || weaponBehaviour.IsAiming
                || currentAnimator.GetCurrentAnimatorStateInfo(0).IsName("Unholster")
                || currentAnimator.GetCurrentAnimatorStateInfo(0).IsName("reloading")
                || currentAnimator.GetCurrentAnimatorStateInfo(0).IsName("shooting"))
            {
                CowsinsUtilities.StopAnim("walking", currentAnimator);
                CowsinsUtilities.StopAnim("running", currentAnimator);
                return;
            }

            if (player.CurrentSpeed > player.CrouchSpeed && player.CurrentSpeed < player.RunSpeed && player.Grounded && !interactManager.Inspecting) CowsinsUtilities.PlayAnim("walking", currentAnimator);
            else CowsinsUtilities.StopAnim("walking", currentAnimator);

            if (player.CurrentSpeed >= player.RunSpeed && player.Grounded) CowsinsUtilities.PlayAnim("running", currentAnimator);
            else CowsinsUtilities.StopAnim("running", currentAnimator);

            if(!interactManager.Inspecting)
            {
                CowsinsUtilities.StopAnim("inspect", currentAnimator);
                CowsinsUtilities.StopAnim("finishedInspect", currentAnimator);
            }    
        }

        public void StopWalkAndRunMotion()
        {
            if (weaponController == null) return; 
            Animator weapon = weaponController.Id.Animator;
            CowsinsUtilities.StopAnim("inspect", weapon);
            CowsinsUtilities.StopAnim("walking", weapon);
            CowsinsUtilities.StopAnim("running", weapon);
        }

        public void TryHideWeapon(bool? hide)
        {
            if (hide.Value) HideWeapon();
        }
        public void TryShowWeapon(bool? show)
        {
            if (show.Value) ShowWeapon();
        }
        public void HideWeapon() => weaponStates.ForceChangeState(weaponStates._States.Hidden());

        public void ShowWeapon() => weaponStates.ForceChangeState(weaponStates._States.Default());

        public void SetParentConstraintSource(Transform transform) => cameraAnimations?.SetTarget(transform);

        private void OnUnholster(bool prop, bool playAnim)
        {
            var animator = weaponController.Id.GetComponentInChildren<Animator>(true);
            animator.Rebind();
            animator.Update(0f);
            animator.enabled = true;
            if (playAnim)
                CowsinsUtilities.PlayAnim("unholster", animator);

            StopWalkAndRunMotion();
            SetParentConstraintSource(weaponController.Id.HeadBone);
        }

        private void StartReload()
        {
            if(weaponController == null) return;
            CowsinsUtilities.PlayAnim("reloading", weaponController.Id.Animator);
        }

        #region INSPECT
        public void InitializeInspection()
        {
            WeaponIdentification wiD = weaponController.Id;
            CowsinsUtilities.PlayAnim("inspect", wiD.Animator);
            CowsinsUtilities.StopAnim("finishedInspect", wiD.Animator);
        }

        public void DisableInspection()
        {
            WeaponIdentification wID = weaponController.Id;
            CowsinsUtilities.PlayAnim("finishedInspect", wID.Animator);
            CowsinsUtilities.StopAnim("inspect", wID.Animator);
        }
        #endregion
    }
}
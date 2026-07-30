using cowsins;
using UnityEngine;

namespace cowsins
{
    public class RecoilSystem
    {
        private InputManager inputManager;
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerControlProvider playerControl;

        private WeaponContext weaponContext;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;

        private float evaluationProgress;
        public float recoilPitchOffset { get; private set; }
        public float recoilYawOffset { get; private set; }

        public RecoilSystem(WeaponContext context)
        {
            this.weaponContext = context;
            PlayerDependencies playerDependencies = weaponContext.Dependencies;

            this.inputManager = playerDependencies.InputManager;
            this.weaponBehaviour = playerDependencies.WeaponBehaviour;
            this.weaponReference = playerDependencies.WeaponReference;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.playerControl = playerDependencies.PlayerControl;

            weaponEvents.Events.OnShootHitscanProjectile.AddListener(ProgressRecoil);
        }

        public void Tick()
        {
            // Relax back to 0 if weapon is null or the current weapon does not apply recoil
            if (weapon == null || !weapon.applyRecoil || id.bulletsLeftInMagazine <= 0)
            {
                recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, 0, 3 * Time.deltaTime);
                recoilYawOffset = Mathf.Lerp(recoilYawOffset, 0, 3 * Time.deltaTime);
                return;
            }

            // If not shooting, relax back to 0
            if (!inputManager.Shooting || weapon.shootMethod == ShootingMethod.Press || weaponBehaviour.IsReloading || !playerControl.IsControllable)
            {
                recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, 0, weapon.recoilRelaxSpeed * Time.deltaTime);
                recoilYawOffset = Mathf.Lerp(recoilYawOffset, 0, weapon.recoilRelaxSpeed * Time.deltaTime);
                evaluationProgress = 0;
                return;
            }

            // If shooting, calculate the pitch and yaw. These will be gathered by PlayerMovement inside Look()
            if (inputManager.Shooting)
            {
                float xamount = (weapon.applyDifferentRecoilOnAiming && weaponBehaviour.IsAiming) ? weapon.xRecoilAmountOnAiming : weapon.xRecoilAmount;
                float yamount = (weapon.applyDifferentRecoilOnAiming && weaponBehaviour.IsAiming) ? weapon.yRecoilAmountOnAiming : weapon.yRecoilAmount;

                float targetPitchRecoil = -weapon.recoilY.Evaluate(evaluationProgress) * yamount * 1f;
                float targetYawRecoil = -weapon.recoilX.Evaluate(evaluationProgress) * xamount * 1f;

                recoilPitchOffset = Mathf.Lerp(recoilPitchOffset, targetPitchRecoil, weapon.recoilRelaxSpeed * Time.deltaTime);
                recoilYawOffset = Mathf.Lerp(recoilYawOffset, targetYawRecoil, weapon.recoilRelaxSpeed * Time.deltaTime);
            }
        }
        public void ProgressRecoil()
        {
            if (weapon.applyRecoil)
            {
                evaluationProgress += 1f / weapon.magazineSize;
            }
        }
    }

}
using System;
using System.Collections;
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// When a weapon is set to Projectile Shoot Style, its shootBehaviour in WeaponIdentification is assigned to this ProjectileShootStyle.
    /// This is done when the weapon is unholstered.
    /// </summary>
    public class ProjectileShootStyle : IShootStyle
    {
        private PlayerDependencies playerDependencies;
        private IWeaponReferenceProvider weaponReference;
        private IPlayerMultipliers multipliers;
        private IWeaponEventsProvider weaponEvents; 
        private Transform[] firePoint;
        private Weapon_SO weapon;
        private Camera mainCamera;

        private bool canShoot = true;
        public Coroutine shootingCoroutine, allowShootCoroutine;

        // Calls events in Weapon Controller when shooting or hitting an enemy ( ProjectileShootStyle does not handle hits so onHit is not used here,
        // but it is still required by IShootStyle.
        public event Action onShoot;
#pragma warning disable CS0067
        public event Action<int, float, RaycastHit, bool> onHit;
#pragma warning restore CS0067

        private WaitForSeconds shootDelayWfs;
        private WaitForSeconds timeBetweenShotsWfs;
        private WaitForSeconds fireRateWfs;

        public ProjectileShootStyle(PlayerDependencies playerDependencies)
        {
            this.playerDependencies = playerDependencies;
            this.weaponReference = playerDependencies.WeaponReference;
            this.weapon = weaponReference.Weapon;
            this.multipliers = playerDependencies.PlayerMultipliers;
            this.weaponEvents = playerDependencies.WeaponEvents;
            this.mainCamera = weaponReference.MainCamera;
            this.firePoint = weaponReference.Id.FirePoint;

            shootDelayWfs = new WaitForSeconds(weapon.shootDelay);
            timeBetweenShotsWfs = new WaitForSeconds(weapon.timeBetweenShots);
            fireRateWfs = new WaitForSeconds(weapon.fireRate);
        }

        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier)
        {
            if (canShoot)
                HandleProjectileShot(spread);
        }
        private void HandleProjectileShot(float spread)
        {
            if (weapon == null) return;

            canShoot = false; // since you have already shot, you will have to wait in order to being able to shoot again

            shootingCoroutine = playerDependencies.StartCoroutine(HandleShooting(spread));
            if (allowShootCoroutine != null)
            {
                playerDependencies.StopCoroutine(allowShootCoroutine);
                allowShootCoroutine = null;
            }
            allowShootCoroutine = playerDependencies.StartCoroutine(AllowShootAfterDelay());

            weaponEvents.Events.OnShootSpawnEffects?.Invoke();
        }

        private IEnumerator HandleShooting(float spread)
        {
            if ((int)weapon.shootStyle == 1)
            {
                yield return shootDelayWfs;
            }

            if (weapon.timeBetweenShots == 0)
            {
                // Rest the bullets that have just been shot
                weaponEvents.Events.OnReduceAmmo?.Invoke();
            }

            // Avoid calling the while loop if we only want to shoot one bullet
            if (weapon.bulletsPerFire == 1)
            {
                if (weapon == null) yield break;
                ProjectileShoot(spread);
            }
            else
            {
                int i = 0;
                while (i < weapon.bulletsPerFire)
                {
                    if (weapon == null) yield break;

                    ProjectileShoot(spread);
                    yield return timeBetweenShotsWfs;
                    i++;
                }
            }

            yield break;
        }

        private void ProjectileShoot(float spread)
        {
            onShoot?.Invoke();

            RaycastHit hit;
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0f));
            Vector3 destination = (Physics.Raycast(ray, out hit) && !hit.transform.CompareTag("Player")) ? destination = hit.point + CowsinsUtilities.GetSpreadDirection(spread, mainCamera) : destination = ray.GetPoint(50f) + CowsinsUtilities.GetSpreadDirection(spread, mainCamera);

            foreach (var p in firePoint)
            {
                GameObject bulletGO = PoolManager.Instance.GetFromPool(weapon.projectile.gameObject, p.position, p.transform.rotation);
                Bullet bullet = bulletGO.GetComponent<Bullet>();
                bullet.prefab = weapon.projectile.gameObject;

                if (weapon.explosionOnHit) bullet.explosionVFX = weapon.explosionVFX;

                bullet.hurtsPlayer = weapon.hurtsPlayer;
                bullet.explosionOnHit = weapon.explosionOnHit;
                bullet.explosionRadius = weapon.explosionRadius;
                bullet.explosionForce = weapon.explosionForce;

                bullet.criticalMultiplier = weapon.criticalDamageMultiplier;
                bullet.destination = destination;
                bullet.player = playerDependencies.transform;
                bullet.speed = weapon.speed;
                bullet.GetComponent<Rigidbody>().isKinematic = (!weapon.projectileUsesGravity) ? true : false;
                bullet.damage = weaponReference.Id.damage * multipliers.DamageMultiplier;
                bullet.duration = weapon.bulletDuration;

                bullet.Initialize();
            }
        }

        private IEnumerator AllowShootAfterDelay()
        {
            yield return fireRateWfs;
            canShoot = true;
        }

        public void SetOnShootEvent(Action @event) => onShoot = @event;
    }
}

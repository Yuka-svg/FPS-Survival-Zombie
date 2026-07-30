using UnityEngine;

namespace cowsins
{
    public class HitDetectionSystem
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;
        private IPlayerMovementStateProvider playerMovement;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private Camera mainCamera => weaponReference.MainCamera;
        private Transform weaponHolder => context.WeaponHolder;

        private WeaponControllerSettings settings;

        private AimSkillSystem aimSystem => AimSkillSystem.Instance;

        public HitDetectionSystem(WeaponContext context, WeaponControllerSettings settings)
        {
            this.settings = settings;

            // Register Pool
            foreach (var entry in this.settings.impactEffects.impacts)
            {
                if (entry.impact != null)
                    PoolManager.Instance.RegisterPool(entry.impact, PoolManager.Instance.WeaponEffectsSize);
            }
            if (this.settings.impactEffects.defaultImpact != null)
                PoolManager.Instance.RegisterPool(this.settings.impactEffects.defaultImpact, PoolManager.Instance.WeaponEffectsSize);

            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.playerMovement = context.Dependencies.PlayerMovementState;

            context.Dependencies.WeaponEvents.Events.OnHit.AddListener(Hit);
        }

        /// <summary>
        /// Handles hit detection, applies damage, and triggers effects.
        /// </summary>
        public void Hit(int layer, float damage, RaycastHit h, bool damageTarget)
        {
            if (weapon == null || h.collider == null) return;

            settings.userEvents.OnHit?.Invoke();
            weaponEvents.Events.OnInstantiateBulletHoleImpact?.Invoke(layer, h);

            if (!damageTarget) return;

            var hitTransform = h.collider.transform;
            float finalDamage = damage * GetDistanceDamageReduction(hitTransform);

            // Resolve target damageable once at the beginning of the hit resolution
            var damageable = CowsinsUtilities.GatherDamageableParent(hitTransform);
            if (damageable == null)
                damageable = h.collider.GetComponent<IDamageable>();

            // Aim skill tree: OneShotCrook — 25% chance to instantly kill ICrookEnemy (e.g. zombies)
            if (aimSystem != null && aimSystem.OneShotCrook && Random.value <= 0.25f)
            {
                if (damageable is MonoBehaviour enemyMb)
                {
                    var crook = enemyMb.GetComponent<ICrookEnemy>();
                    if (crook != null)
                    {
                        damageable.Damage(crook.GetMaxHealth() * 10f, true);
                        AIDirector.Instance?.RegisterHit();
                        return;
                    }
                }
            }

            // Aim skill tree: BonusDamageVsSpecial — 2x damage vs ISpecialEnemy (e.g. Boomer, Tank)
            if (aimSystem != null && aimSystem.BonusDamageVsSpecial)
            {
                if (damageable is MonoBehaviour enemyMb2)
                {
                    var special = enemyMb2.GetComponent<ISpecialEnemy>();
                    if (special != null)
                    {
                        finalDamage *= 2f;
                    }
                }
            }

            // Aim skill tree: random crit roll (levels 2-4)
            bool isAimCrit = false;
            if (aimSystem != null && aimSystem.RollCritical())
            {
                finalDamage = aimSystem.ApplyCriticalDamage(finalDamage);
                isAimCrit = true;
            }

            if (damageable != null)
            {
                // Check if hit was a headshot directly or via penetrating raycast to critical children
                bool isHeadshot = hitTransform.CompareTag("Critical") || CheckIsHeadshot(h, damageable);

                if (isHeadshot)
                {
                    settings.userEvents.OnCriticalHit?.Invoke();
                    damageable.Damage(finalDamage * weapon.criticalDamageMultiplier, true);
                }
                else
                {
                    damageable.Damage(finalDamage, isAimCrit);
                }
                AIDirector.Instance?.RegisterHit();
            }
        }

        private bool CheckIsHeadshot(RaycastHit h, IDamageable target)
        {
            if (target == null) return false;
            
            var targetMb = target as MonoBehaviour;
            if (targetMb == null) return false;

            var cam = mainCamera != null ? mainCamera : Camera.main;
            if (cam == null) return false;

            Vector3 rayOrigin = cam.transform.position;
            Vector3 rayDir = (h.point - rayOrigin).normalized;

            // Raycast starting slightly before the hit point to detect overlapping child colliders
            Vector3 startPoint = h.point - rayDir * 0.2f;
            RaycastHit[] hits = Physics.RaycastAll(startPoint, rayDir, 4.0f);

            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("Critical"))
                {
                    if (hit.collider.transform.IsChildOf(targetMb.transform))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private float GetDistanceDamageReduction(Transform target)
        {
            if (!weapon.applyDamageReductionBasedOnDistance) return 1;
            if (Vector3.Distance(target.position, context.Transform.position) > weapon.minimumDistanceToApplyDamageReduction)
                return (weapon.minimumDistanceToApplyDamageReduction / Vector3.Distance(target.position, context.Transform.position)) * weapon.damageReductionMultiplier;
            else return 1;
        }
    }

}
/// <summary>
/// This script belongs to cowsins as a part of the cowsins FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Inheriting from destructible, lets you explode barrels
    /// </summary>
    public class ExplosiveBarrel : Destructible
    {
        private static readonly Collider[] _explosionHitBuffer = new Collider[64];

        [SerializeField] private float explosionRadius;

        [SerializeField] private float explosionForce;

        [SerializeField] private bool hurtPlayer = true;

        [Tooltip("Damage dealt on explosion to any Damageable object within the radius." +
            "NOTE:Damage will be scaled depending on how far the object is from the center of the explosion "), SerializeField]
        private float damage;

        [Header("Effects")]
        [Tooltip("Instantiate this when the barrel explodes"), SerializeField]
        private GameObject destroyedObject, explosionVFX;

        /// <summary>
        /// Override the method from Destructible.cs
        /// Here we are damaging IDamageables within a certain radius & also instantiating some effect on destructed.
        /// </summary>
        public override void Die()
        {
            SoundManager.Instance.PlaySoundAtPosition(destroyedSFX,transform.position, 0, .1f, true);
            int hitCount = Physics.OverlapSphereNonAlloc(transform.position, explosionRadius, _explosionHitBuffer);

            Instantiate(destroyedObject, transform.position, Quaternion.identity);
            Instantiate(explosionVFX, transform.position, Quaternion.identity);

            for (int i = 0; i < hitCount; i++)
            {
                Collider c = _explosionHitBuffer[i];
                if (c == null) continue;
                if (c.CompareTag("Player") && !hurtPlayer) continue;
                if (c.TryGetComponent<Rigidbody>(out var rb))
                    rb.AddExplosionForce(explosionForce / (Vector3.Distance(c.transform.position, transform.position) + .1f), transform.position, explosionRadius, 5, ForceMode.Impulse);

                float dmg = damage / Vector3.Distance(c.transform.position, transform.position);
                if (c.CompareTag("BodyShot"))
                {
                    CowsinsUtilities.GatherDamageableParent(c.transform).Damage(dmg, false);
                    continue;
                }
                else if (c.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.Damage(dmg, false);
                    if (c.TryGetComponent<IPlayerMovementStateProvider>(out var playerMovement))
                    {
                        if (c.TryGetComponent<CameraEffects>(out var cameraEffects))
                        {
                            cameraEffects.ExplosionShake(Vector3.Distance(cameraEffects.transform.position, transform.position));
                        }
                    }
                    continue;
                }
            }

            // Clean up static references to allow garbage collection of hit colliders
            for (int i = 0; i < hitCount; i++)
            {
                _explosionHitBuffer[i] = null;
            }
            base.Die();
        }
    }
}

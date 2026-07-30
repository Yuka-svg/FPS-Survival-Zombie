/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;  
using System.Collections.Generic;
using UnityEditor;

namespace cowsins
{
    public enum AttachmentType
    {
        Barrel,
        Scope,
        Stock,
        Grip,
        Magazine,
        Flashlight,
        Laser
    }

    /// <summary>
    /// Attach this to your weapon object ( the one that goes in the weapon array of WeaponController )
    /// </summary>
    public class WeaponIdentification : MonoBehaviour
    {
        public Weapon_SO weapon;

        [Tooltip("Every weapon, excluding melee, must have a firePoint, which is the point where the bullet comes from." +
            "Just make an empty object, call it firePoint for organization purposes and attach it here. ")]
        public Transform[] FirePoint;

        public Transform aimPoint;

        [HideInInspector] public int totalMagazines, magazineSize, bulletsLeftInMagazine, totalBullets;

        [SerializeField] private Transform headBone; 
        public Attachment Barrel => GetCurrentAttachment(AttachmentType.Barrel);
        public Attachment Scope => GetCurrentAttachment(AttachmentType.Scope);
        public Attachment Stock => GetCurrentAttachment(AttachmentType.Stock);
        public Attachment Grip  => GetCurrentAttachment(AttachmentType.Grip);
        public Attachment Magazine => GetCurrentAttachment(AttachmentType.Magazine);
        public Attachment Flashlight => GetCurrentAttachment(AttachmentType.Flashlight);
        public Attachment Laser => GetCurrentAttachment(AttachmentType.Laser);

        private Dictionary<AttachmentType, Attachment> currentAttachments = new Dictionary<AttachmentType, Attachment>();

        [Tooltip("Defines the default attachments for your weapon. The first time you pick it up, these attachments will be equipped."), SerializeField] private DefaultAttachment defaultAttachments;

        [Tooltip("Defines all the attachments that can be equipped on your weapon.")] public CompatibleAttachments compatibleAttachments;

        [HideInInspector] public Vector3 originalAimPointPos, originalAimPointRot;

        [HideInInspector] public float heatRatio;

        private delegate void ReduceAmmoStyle();

        private ReduceAmmoStyle reduceAmmo;

        private IShootStyle shootBehaviour;
        public Transform HeadBone => headBone;

        private Animator animator;
        public Animator Animator => animator;

        public float damage;
        public float fireRate;
        public float baseSpread;
        public float baseAimSpread;
        public float aimSpeed;
        public float reloadTime;
        public float weightMultiplier;
        public float camShakeAmount;
        public float penetrationAmount;

        public Vector3 aimingRotation;
        public Vector3 aimingOffset;

        public GameObject muzzleVFX;
        public AudioClip[] fireSFXs;

        private void OnEnable()
        {
            originalAimPointPos = aimPoint.localPosition;
            originalAimPointRot = aimPoint.localRotation.eulerAngles;
        }
        private void Awake()
        {
            animator = GetComponentInChildren<Animator>(true); 
            if(animator) animator.keepAnimatorStateOnDisable = true;

            totalMagazines = weapon.totalMagazines;

            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                currentAttachments[type] = null; 
            }

            if(weapon.reloadStyle == ReloadingStyle.defaultReload)
                reduceAmmo = ReduceDefaultAmmo;
            else 
                reduceAmmo = ReduceOverheatAmmo;

            damage = weapon.damagePerBullet;
            fireRate = weapon.fireRate;
            baseSpread = weapon.applyBulletSpread ? weapon.spreadAmount : 0;
            baseAimSpread = weapon.applyBulletSpread ? weapon.aimSpreadAmount : 0;
            aimSpeed = weapon.aimingSpeed;
            reloadTime = weapon.reloadTime;
            weightMultiplier = weapon.weightMultiplier;
            camShakeAmount = weapon.camShakeAmount;
            penetrationAmount = weapon.penetrationAmount;

            aimingOffset = Vector3.zero;
            aimingRotation = weapon.aimingRotation;

            muzzleVFX = weapon.muzzleVFX;
            fireSFXs = weapon.audioSFX.shooting;
            magazineSize =
            weapon.magazineSize +
            PlayerUpgradeManager.Instance.bonusMagazine;
        }

        public void SetShootStyle(IShootStyle shootBehaviour) => this.shootBehaviour = shootBehaviour;

        public void Shoot(float spread, float damageMultiplier, float shakeMultiplier) => shootBehaviour?.Shoot(spread, damageMultiplier, shakeMultiplier);

        public void ReduceAmmo() => reduceAmmo?.Invoke();   

        public void ReduceOverheatAmmo()
        {
            heatRatio += (float)1f / magazineSize;
        }

        public void ReduceDefaultAmmo()
        {
            if (!weapon.infiniteBullets)
            {
                bulletsLeftInMagazine -= weapon.ammoCostPerFire;
                if (bulletsLeftInMagazine < 0)
                {
                    bulletsLeftInMagazine = 0;
                }
            }
        }

        public AudioClip GetFireSFX() => fireSFXs[Random.Range(0, fireSFXs.Length - 1)];

        public Dictionary<AttachmentType, Attachment> GetCurrentAttachments() => currentAttachments;

        public DefaultAttachment GetDefaultAttachments() => defaultAttachments;

        public List<AttachmentIdentifier_SO> GetDefaultAttachmentsIdentifiers()
        {
            var attachments = new List<AttachmentIdentifier_SO>();

            foreach (var kvp in defaultAttachments.DefaultAttachments)
            {
                var attachment = kvp.Value;
                if (attachment != null && attachment.attachmentIdentifier != null)
                {
                    attachments.Add(attachment.attachmentIdentifier);
                }
            }

            return attachments;
        }

        public void SetCurrentAttachment(AttachmentType type, Attachment attachment)
        {
            currentAttachments[type] = attachment;
        }

        /// <summary>
        /// Returns the Default Attachment for the Specific Attachment Type given.
        /// </summary>
        /// <param name="type">Attachment Type to return.</param>
        /// <returns></returns>
        public Attachment GetDefaultAttachment(AttachmentType type) => defaultAttachments.DefaultAttachments.TryGetValue(type, out var attachment) ? attachment : null;

        public Attachment GetCurrentAttachment(AttachmentType type)
        {
            currentAttachments.TryGetValue(type, out var attachment);
            return attachment;
        }

        public bool HasAttachment(AttachmentType type) => currentAttachments.ContainsKey(type);

        public void RemoveAttachment(AttachmentType type)
        {
            currentAttachments.Remove(type);
        }

        public void DeactivateAttachment(AttachmentType type)
        {
            if (currentAttachments.TryGetValue(type, out var atc) && atc != null)
            {
                atc.gameObject.SetActive(false);
            }
        }

#if UNITY_EDITOR

        #region Gizmos

        // Additional Weapon Information on the editor view
        Vector3 boxSize = new Vector3(0.1841836f, 0.14f, 0.54f);
        Vector3 boxPosition = new Vector3(0,-.2f,.6f);

        private void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                Gizmos.color = new Color(1, 0, 0, 0.3f);

                Gizmos.DrawWireCube(transform.position + boxPosition, boxSize);
                Handles.Label(transform.position + boxPosition + Vector3.up * (boxSize.y / 2 + 0.1f), "Approximate Weapon Location");

                if(aimPoint)
                {
                    Gizmos.color = Color.blue;
                    Gizmos.DrawWireCube(aimPoint.position , Vector3.one * .02f);
                    Handles.Label(aimPoint.position + Vector3.up * .05f, "Aim Point");

                }

                for(int i = 0; i < FirePoint.Length; i++)
                {
                    if (FirePoint[i] != null)
                    {
                        Gizmos.color = Color.green;
                        Gizmos.DrawWireCube(FirePoint[i].position, Vector3.one * .02f);
                        Handles.Label(FirePoint[i].position + Vector3.up * .05f, "Fire Point " + (i + 1));

                    }
                }
            }
        }
        #endregion
#endif
    }
}

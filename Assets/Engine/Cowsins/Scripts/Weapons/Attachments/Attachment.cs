using UnityEngine;
namespace cowsins
{
    public abstract class Attachment : MonoBehaviour
    {
        [Title("Basic")]
        [Tooltip("Identifier of the attachment. You can have the same attachment within different weapons as long as they share this attachment identifier" +
                "scriptable object.")]
        public AttachmentIdentifier_SO attachmentIdentifier;

        public virtual void Attach(WeaponIdentification Id)
        {
            this.gameObject.SetActive(true);
            ModifyStats(Id, 1);
            Id.SetCurrentAttachment(attachmentIdentifier.attachmentType, this);
        }

        public virtual void Dettach(WeaponIdentification Id)
        {
            ModifyStats(Id, -1);
            Id.RemoveAttachment(attachmentIdentifier.attachmentType);
            this.gameObject.SetActive(false);
        }

        public virtual void AttachmentAction() { }

        private void ModifyStats(WeaponIdentification Id, int direction)
        {
            Weapon_SO weapon = Id.weapon;
            Id.damage += direction * (weapon.damagePerBullet * attachmentIdentifier.damageIncrease);
            Id.fireRate -= direction * (weapon.fireRate * attachmentIdentifier.fireRateDecrease);
            Id.baseSpread -= direction * (weapon.spreadAmount * attachmentIdentifier.spreadDecrease);
            Id.aimSpeed += direction * (weapon.aimingSpeed * attachmentIdentifier.aimSpeedIncrease);
            Id.reloadTime += direction * (weapon.reloadTime * attachmentIdentifier.reloadSpeedIncrease);
            Id.weightMultiplier += direction * (weapon.weightMultiplier * attachmentIdentifier.weightAdded);
            Id.camShakeAmount += direction * (weapon.camShakeAmount * attachmentIdentifier.cameraShakeMultiplier);
            Id.penetrationAmount += direction * (weapon.penetrationAmount * attachmentIdentifier.penetrationIncrease);
        }
    }
}
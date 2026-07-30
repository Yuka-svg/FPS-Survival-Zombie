using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace cowsins
{
    public class WeaponInventorySystem
    {
        private WeaponContext context;
        private InputManager inputManager;
        private IWeaponBehaviourProvider weaponBehaviour;
        private IWeaponReferenceProvider weaponReference;
        private IWeaponEventsProvider weaponEvents;

        private Weapon_SO weapon => weaponReference.Weapon;
        private WeaponIdentification id => weaponReference.Id;
        private Camera mainCamera => weaponReference.MainCamera;
        private Transform weaponHolder => context.WeaponHolder;

        private WeaponControllerSettings settings;

        public WeaponInventorySystem(WeaponContext context, WeaponControllerSettings settings)
        {
            this.context = context;
            this.inputManager = context.InputManager;
            this.weaponBehaviour = context.Dependencies.WeaponBehaviour;
            this.weaponReference = context.Dependencies.WeaponReference;
            this.weaponEvents = context.Dependencies.WeaponEvents;
            this.settings = settings;

            weaponReference.Inventory = new WeaponIdentification[settings.inventorySize];

            context.Dependencies.InteractEvents.Events.OnAttachmentPickedUp.AddListener((unusedProperty) =>
            {
                var weapon = weaponReference.Inventory[weaponReference.CurrentWeaponIndex].gameObject;
                UnHolster(weapon, true);
            });

        }

        /// <summary>
        /// Active the weapon in the Inventory that corresponds to the CurrentWeaponIndex
        /// </summary>
        public void UnHolster(GameObject weaponObj, bool playAnim)
        {
            if (weapon == null) return;

            context.CanShoot = true;

            weaponObj.SetActive(true);
            weaponReference.Id = weaponObj.GetComponent<WeaponIdentification>();

            weaponEvents.Events.OnEquipWeapon?.Invoke(weaponReference.Id);
            weaponObj.GetComponentInChildren<WeaponSpecificEffects>().Initialize(context.Dependencies);

            SoundManager.Instance.PlaySound(weapon.audioSFX.unholster, .1f, 0, true);

            context.firePoint = weaponReference.Inventory[weaponReference.CurrentWeaponIndex].FirePoint;

            if ((int)weapon.shootStyle == 2) weaponEvents.Events.OnReloadUIChanged?.Invoke(false, false);

            settings.userEvents.OnUnholster.Invoke();
            weaponEvents.Events.OnUnholster?.Invoke(settings.autoReload, playAnim);
        }

        public void SelectWeapon()
        {
            context.CanShoot = false;
            weaponReference.Weapon = null;

            settings.userEvents.OnInventorySlotChanged.Invoke();

            bool unholstered = false;

            // Spawn the appropriate weapon in the inventory
            foreach (WeaponIdentification weapon_ in weaponReference.Inventory)
            {
                if (weapon_ != null)
                {
                    weapon_.gameObject.SetActive(false);
                    weapon_.Animator.enabled = false;
                    if (weapon_ == weaponReference.Inventory[weaponReference.CurrentWeaponIndex])
                    {
                        weaponReference.Weapon = weaponReference.Inventory[weaponReference.CurrentWeaponIndex].weapon;

                        weapon_.Animator.enabled = true;
                        UnHolster(weapon_.gameObject, true);
                        unholstered = true;
                    }
                }
            }

            if (!unholstered)
            {
                weaponEvents.Events.OnUnselectingWeapon?.Invoke();
            }

            weaponEvents.Events.OnSelectWeapon?.Invoke();
            settings.userEvents.OnSelectWeapon.Invoke(); // Invoke your custom method
        }

        public void InstantiateWeapon(Weapon_SO newWeapon, int inventoryIndex, int? _bulletsLeftInMagazine, int? _totalBullets, List<AttachmentIdentifier_SO> attachmentsToAssign)
        {
            if (newWeapon == null)
            {
                CowsinsUtilities.LogError("<b><color=yellow>The weapon to instantiate is null!</color></b> " +
                    "Please ensure the proper <b><color=cyan>Weapon_SO</color></b> is assigned.");
                return;
            }

            // Instantiate Weapon
            var instantiatedWeapon = Object.Instantiate(newWeapon.weaponObject, weaponHolder);
            instantiatedWeapon.transform.localPosition = newWeapon.weaponObject.transform.localPosition;

            // Destroy the Weapon if it already exists in the same slot
            if (weaponReference.Inventory[inventoryIndex] != null) Object.Destroy(weaponReference.Inventory[inventoryIndex].gameObject);

            // Set the Weapon
            weaponReference.Inventory[inventoryIndex] = instantiatedWeapon;

            // Select weapon if it is the current Weapon
            if (inventoryIndex == weaponReference.CurrentWeaponIndex)
            {
                weaponReference.Weapon = newWeapon;
            }
            else instantiatedWeapon.gameObject.SetActive(false);

            // if _bulletsLeftInMagazine is null, calculate magazine size. If not, simply assign _bulletsLeftInMagazine
            weaponReference.Inventory[inventoryIndex].bulletsLeftInMagazine = _bulletsLeftInMagazine ?? newWeapon.magazineSize;
            weaponReference.Inventory[inventoryIndex].totalBullets = _totalBullets ??
            (newWeapon.limitedMagazines
                ? newWeapon.magazineSize * newWeapon.totalMagazines
                : newWeapon.magazineSize);

            weaponEvents.Events.OnWeaponInventoryChanged?.Invoke(inventoryIndex, newWeapon);
            weaponEvents.Events.OnAssignAttachmentsToWeapon?.Invoke(instantiatedWeapon, inventoryIndex, attachmentsToAssign);

            if (inventoryIndex == weaponReference.CurrentWeaponIndex) SelectWeapon();
        }

        public void ReleaseCurrentWeapon() => ReleaseWeapon(weaponReference.CurrentWeaponIndex);

        public void ReleaseWeapon(int index)
        {
            Object.Destroy(weaponReference.Inventory[index].gameObject);
            weaponReference.Inventory[index] = null;
            weaponReference.Weapon = null;

            weaponEvents.Events.OnWeaponInventoryChanged?.Invoke(index, null);
            weaponEvents.Events.OnReleaseWeapon?.Invoke();
        }

        public void GetInitialWeapons()
        {
            var initialWeapons = settings.initialWeapons;
            if (initialWeapons.Length == 0) return;

            int i = 0;
            while (i < initialWeapons.Length)
            {
                InstantiateWeapon(initialWeapons[i], i, null, null, null);
                i++;
            }
            weaponReference.Weapon = initialWeapons[0];
        }


        /// <summary>
        /// Handles inventory slot changes (scrolling or weapon switch).
        /// </summary>
        public void HandleInventory()
        {
            if (inputManager.Reloading) return; // Prevent weapon change while reloading

            MouseWheelWeaponSwitch();
            NumKeyWeaponSwitch();
        }

        private void NumKeyWeaponSwitch()
        {
            if (!settings.allowNumberKeyWeaponSwitch) return;

            // Handle switching with numkeys
            for (int i = 0; i < 9; i++)
            {
                if (i != weaponReference.CurrentWeaponIndex && Keyboard.current[(Key.Digit1 + i)].wasPressedThisFrame && i < settings.inventorySize)
                {
                    ChangeWeaponIndex(i);
                    break;
                }
            }
        }

        private void MouseWheelWeaponSwitch()
        {
            if (!settings.allowMouseWheelWeaponSwitch) return;

            int direction = 0;

            if (inputManager.Scrolling > 0 || inputManager.Previousweapon)
                direction = 1; // next slot
            else if (inputManager.Scrolling < 0 || inputManager.Nextweapon)
                direction = -1; // previous slot

            if (direction == 0) return;

            int newIndex = weaponReference.CurrentWeaponIndex + direction;

            // Ensure new index is within valid range
            if (newIndex < 0 || newIndex >= settings.inventorySize) return;

            ChangeWeaponIndex(newIndex);
        }

        public (Weapon_SO, int, int) SwapWeapons(Weapon_SO newWeapon, int newWeaponBullets, int newWeaponTotalBullets, List<AttachmentIdentifier_SO> newWeaponAttachmentKeys)
        {
            Weapon_SO oldWeapon = weaponReference.Weapon;
            int savedBulletsLeftInMagazine = weaponReference.Id.bulletsLeftInMagazine;
            int savedTotalBullets = weaponReference.Id.totalBullets;
            ReleaseCurrentWeapon();

            InstantiateWeapon(newWeapon, weaponReference.CurrentWeaponIndex, newWeaponBullets, newWeaponTotalBullets, newWeaponAttachmentKeys);

            return (oldWeapon, savedBulletsLeftInMagazine, savedTotalBullets);
        }

        public bool TryToAddWeapons(Weapon_SO weapon, int currentBullets, int totalBullets, List<AttachmentIdentifier_SO> attachmentKeys)
        {
            (bool isFull, int emptyIndex) = CheckIfInventoryIsFull();
            if (isFull) return false;

            InstantiateWeapon(weapon, emptyIndex, currentBullets, totalBullets, attachmentKeys);
            return true;
        }

        public (bool, int) CheckIfInventoryIsFull()
        {
            for (int i = 0; i < settings.inventorySize; i++)
            {
                if (weaponReference.Inventory[i] == null) // Inventory has room for a new weapon.
                {
                    return (false, i);
                }
            }
            // Inventory is full
            return (true, 0);
        }

        public bool AddDuplicateWeaponAmmo(int ammo)
        {
            for (int i = 0; i < weaponReference.Inventory.Length; i++)
            {
                if (weaponReference.Inventory[i] && weaponReference.Inventory[i].weapon == weapon && weapon.limitedMagazines)
                {
                    weaponReference.Id.totalBullets += ammo;
                    return true;
                }
            }
            return false;
        }

        private void ChangeWeaponIndex(int newIndex)
        {
            weaponEvents.Events.OnSwitchingWeapon?.Invoke();
            // Update and select
            weaponReference.CurrentWeaponIndex = newIndex;
            SelectWeapon();
        }
    }

}
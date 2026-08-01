/// <summary>
/// This script belongs to cowsins as a part of the cowsins FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
#if INVENTORY_PRO_ADD_ON
using cowsins.Inventory;
#endif

namespace cowsins
{
    public class InteractManager : MonoBehaviour, IInteractManagerProvider, IInteractEventsProvider
    {
        [System.Serializable]
        public class InteractEvents
        {
            public UnityEvent OnFinishInteraction;
            public UnityEvent<Pickeable> onDrop;
            public UnityEvent onDropWeapon;
        }

        [Tooltip("Attach your main camera"), SerializeField] private Camera mainCamera;

        [Tooltip("Bitmask that defines the interactable layer"), SerializeField] private LayerMask mask;

        [Tooltip("Enable this toggle if you want to be able to drop your weapons"), SerializeField] private bool canDrop;

        [Tooltip("Attach the generic pickeable object here"), SerializeField] private Pickeable weaponGenericPickeable;

        [Tooltip("Attach the generic pickeable object here"), SerializeField] private Pickeable attachmentGenericPickeable;

        [Tooltip("Distance from the player to detect interactable objects"), SerializeField] private float detectInteractionDistance;

        [Tooltip("Distance from the player where the pickeable will be instantiated"), SerializeField] private float droppingDistance;

        [Tooltip("Randomize drop offset (from -randomDropOffset to +randomDropOffset)"), SerializeField, Range(0f,1f)] private float randomDropOffset = .2f;

        [SerializeField, Tooltip("How much time player has to hold the interact button in order to successfully interact")] private float progressRequiredToInteract;

        [Tooltip("Adjust the interaction interval, the lower, the faster you will be able to interact"), Range(.2f, .7f), SerializeField] private float interactInterval = .4f;

        [Tooltip("When picking up a duplicate weapon, if duplicateWeaponAddsBullets is true, the bullets will be added to the total count of the current weapon instead of creating a new instance of the same weapon. " +
            "This feature is only applicable to weapons with limited magazines."), SerializeField]
        private bool duplicateWeaponAddsBullets;

        [Tooltip("If true, the player will be able to inspect the current weapon."), SerializeField] private bool canInspect;

        [Tooltip("Allows the player to equip and unequip attachments while inspecting. It also displays a custom UI for that."), SerializeField] private bool realtimeAttachmentCustomization;

        [Tooltip("When inspecting, display current attachments only. Otherwise you will be able to see all compatible attachments."), SerializeField] private bool displayCurrentAttachmentsOnly;

        [Tooltip("While Inspecting the weapon, if an attachment is dettached and this field is true, the attachment will be dropped."), SerializeField] private bool dropAttachmentOnDettachUI;

        private float progressElapsed;
        private bool alreadyInteracted = false;
        private Interactable _currentHoldTarget;
        private float _lockedHoldDuration;
        private bool _lockedInstantInteraction;
        private bool _requireRePress = false;
        private bool inspecting = false;

        public float ProgressElapsed => progressElapsed;
        public bool Inspecting => inspecting;
        public bool CanDrop => canDrop;
        public float DroppingDistance => droppingDistance;
        public bool DuplicateWeaponAddsBullets => duplicateWeaponAddsBullets;
        public bool CanInspect => canInspect;   
        public bool RealtimeAttachmentCustomization => realtimeAttachmentCustomization; 
        public bool DisplayCurrentAttachmentsOnly => displayCurrentAttachmentsOnly; 
        public Interactable HighlightedInteractable => highlightedInteractable;
        public InteractManagerEvents Events { get; private set; } = new InteractManagerEvents();


        private PlayerOrientation orientation;

        private PlayerDependencies playerDependencies;
        private IPlayerMovementStateProvider playerMovement; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IPlayerControlProvider playerControl; // IPlayerControlProvider is implemented in PlayerControl.cs
        private IWeaponBehaviourProvider weaponController; // IWeaponBehaviourProvider is implemented in WeaponController.cs
        private IWeaponReferenceProvider weaponReferences; // IWeaponReferenceProvider is implemented in WeaponController.cs
        private IWeaponEventsProvider weaponEvents; // IWeaponEventsProvider is implemented in WeaponController.cs
        private InputManager inputManager;

        private Interactable highlightedInteractable;

        public InteractEvents userEvents;

        private void OnEnable()
        {
            // Subscribe to the event
            if(realtimeAttachmentCustomization)
            {
                if (dropAttachmentOnDettachUI) UIEvents.onAttachmentUIElementClicked += DropAttachment;
                else UIEvents.onAttachmentUIElementClicked += DeactivateCurrentAttachment;
            }
        }

        private void Start()
        {
            // Grab main references
            playerDependencies = GetComponent<PlayerDependencies>();
            weaponController = playerDependencies.WeaponBehaviour;
            weaponReferences = playerDependencies.WeaponReference;
            weaponEvents = playerDependencies.WeaponEvents; 
            playerMovement = playerDependencies.PlayerMovementState;
            playerControl = playerDependencies.PlayerControl;
            orientation = playerMovement.Orientation;
            mainCamera = weaponReferences.MainCamera;
            inputManager = playerDependencies.InputManager;

            // Listen for the drop event from the InputManager
            if(canDrop)
                inputManager.OnDrop += HandleDrop;
        }


        private void OnDisable()
        {
            // Unsubscribe to the event
            UIEvents.onAttachmentUIElementClicked -= DropAttachment;
            UIEvents.onAttachmentUIElementClicked -= DeactivateCurrentAttachment;
            if (canDrop) 
                inputManager.OnDrop -= HandleDrop;
        }

        private void Update()
        {
            if (!playerControl.IsControllable)
            {
                if (_currentHoldTarget != null) CancelActiveHold();
                return;
            }

            DetectInteractable();
            DetectInput();
        }
        private void DetectInteractable()
        {
            if (mainCamera == null) return;

            if (_currentHoldTarget != null && progressElapsed > 0f)
            {
                return;
            }

            Interactable interactableTarget = FindDownedCompanionProximity();

            if (interactableTarget == null && Physics.Raycast(mainCamera.transform.position, mainCamera.transform.forward, out RaycastHit interactableHit, detectInteractionDistance, mask))
            {
                if (!interactableHit.collider.TryGetComponent(out interactableTarget))
                {
                    interactableTarget = interactableHit.collider.GetComponentInParent<Interactable>();
                }
            }

            if (interactableTarget != null)
            {
                if (highlightedInteractable != interactableTarget)
                {
                    if (interactableTarget.IsForbiddenInteraction(weaponReferences))
                    {
                        Events.OnForbiddenInteraction?.Invoke();
                    }
                    else
                    {
                        EnableInteractionUI(interactableTarget);
                    }
                    highlightedInteractable = interactableTarget;
                }
            }
            else if (highlightedInteractable != null)
            {
                DisableInteractionUI();
            }
        }

        public void ForceRefreshUI(Interactable interactable)
        {
            highlightedInteractable = interactable;
            if (interactable != null)
            {
                interactable.interactable = true;
                interactable.Highlight();
                Events.OnInteractionProgressChanged?.Invoke(-1f);
                Events.OnAllowedInteraction?.Invoke(interactable.interactText);
            }
            else
            {
                DisableInteractionUI();
            }
        }

        private void EnableInteractionUI(Interactable interactable)
        {
            if (interactable == null)
            {
                DisableInteractionUI();
                return;
            }
            interactable.interactable = true;
            if (highlightedInteractable == interactable) return;
            highlightedInteractable = interactable;
            interactable.Highlight();
            Events.OnAllowedInteraction?.Invoke(interactable.interactText);
        }

        public void CancelActiveHold()
        {
            if (inputManager != null && inputManager.Interacting) _requireRePress = true;
            if (_currentHoldTarget != null && _currentHoldTarget.gameObject != null)
            {
                _currentHoldTarget.OnHoldCancel();
            }
            _currentHoldTarget = null;
            ResetInteractionProgress();
            if (highlightedInteractable != null) ForceRefreshUI(highlightedInteractable); else DisableInteractionUI();
        }

        private Interactable FindDownedCompanionProximity()
        {
            if (mainCamera == null) return null;
            Collider[] hits = Physics.OverlapSphere(mainCamera.transform.position, detectInteractionDistance, mask);
            foreach (var col in hits)
            {
                var trigger = col.GetComponentInParent<Interactable>();
                if (trigger != null && !trigger.IsForbiddenInteraction(weaponReferences) && !trigger.InstantInteraction)
                {
                    Vector3 dirToTarget = (col.bounds.center - mainCamera.transform.position).normalized;
                    if (Vector3.Dot(mainCamera.transform.forward, dirToTarget) > 0.3f)
                        return trigger;
                }
            }
            return null;
        }

        private void DisableInteractionUI()
        {
            if(highlightedInteractable)
            {
                highlightedInteractable.interactable = false;
                highlightedInteractable.Unhighlight();
            }
            highlightedInteractable = null;
            Events.OnDisableInteraction?.Invoke();
        }

        private void DetectInput()
        {
            if (inputManager != null && !inputManager.Interacting)
            {
                _requireRePress = false;
            }

            if (_requireRePress || alreadyInteracted)
            {
                if (_currentHoldTarget != null) CancelActiveHold();
                return;
            }

            if (inputManager != null && !inputManager.Interacting && _currentHoldTarget != null)
            {
                CancelActiveHold();
                return;
            }

            if (inputManager != null && inputManager.Interacting && _currentHoldTarget == null && !alreadyInteracted && !_requireRePress)
            {
                _currentHoldTarget = highlightedInteractable;
            }

            if (_currentHoldTarget == null || _currentHoldTarget.IsForbiddenInteraction(weaponReferences))
            {
                if (_currentHoldTarget != null) CancelActiveHold();
                return;
            }

            var col = _currentHoldTarget.GetComponent<Collider>() ?? _currentHoldTarget.GetComponentInChildren<Collider>();
            Vector3 targetPos = col != null ? col.ClosestPoint(mainCamera.transform.position) : _currentHoldTarget.transform.position;
            if (Vector3.Distance(mainCamera.transform.position, targetPos) > detectInteractionDistance + 1.0f)
            {
                CancelActiveHold();
                return;
            }

            if (progressElapsed <= 0f)
            {
                _lockedHoldDuration = Mathf.Max(0.0001f, _currentHoldTarget.GetHoldDuration(progressRequiredToInteract));
                _lockedInstantInteraction = _currentHoldTarget.InstantInteraction;
            }

            if (progressElapsed > 0f && Mathf.Abs(_currentHoldTarget.GetHoldDuration(progressRequiredToInteract) - _lockedHoldDuration) > 0.01f)
            {
                CancelActiveHold();
                return;
            }

            if (inputManager != null && inputManager.Interacting)
            {
                progressElapsed += Time.deltaTime;
                _currentHoldTarget.OnHoldProgressUpdate(progressElapsed / _lockedHoldDuration);

                if (!_lockedInstantInteraction)
                {
                    Events.OnInteractionProgressChanged?.Invoke(progressElapsed / _lockedHoldDuration);
                }

                if (progressElapsed >= _lockedHoldDuration || (_lockedInstantInteraction && progressElapsed > 0))
                {
                    PerformInteraction();
                }
            }
        }

        private void PerformInteraction()
        {
            ResetInteractionProgress();
            alreadyInteracted = true;
            _requireRePress = true;

            Interactable targetToInteract = _currentHoldTarget != null ? _currentHoldTarget : highlightedInteractable;
            _currentHoldTarget = null;

            if (targetToInteract != null)
            {
                targetToInteract.Interact(this.transform);
                targetToInteract.Unhighlight();
            }

            Invoke(nameof(ResetInteractTimer), interactInterval);
            Events.OnPerformInteraction?.Invoke();

            if (highlightedInteractable != null) ForceRefreshUI(highlightedInteractable); else DisableInteractionUI();

            userEvents.OnFinishInteraction.Invoke();
            Events.OnFinishInteraction?.Invoke();
        }

        private void ResetInteractionProgress()
        {
            progressElapsed = -.01f; 
            Events.OnInteractionProgressChanged?.Invoke(0);
        }

        private void HandleDrop()
        {
            // Handles weapon dropping by pressing the drop button
            if (weaponReferences.Weapon == null || weaponController.IsReloading || !weaponController.IsMeleeAvailable || inspecting || !playerControl.IsControllable) return;

            WeaponPickeable pick = Instantiate(weaponGenericPickeable, orientation.Position + orientation.Forward * droppingDistance + transform.right * randomDropOffset, orientation.Rotation) as WeaponPickeable;
            pick.Drop(playerDependencies, orientation);
            WeaponIdentification wp = weaponReferences.Id; 
            pick.SetPickeableAttachments(wp);

            Events.OnDrop?.Invoke();
            userEvents.onDrop?.Invoke(pick);
        }
        private void ResetInteractTimer() => alreadyInteracted = false;

        public void ToggleInspectionState(bool state) => inspecting = state;

        /// <summary>
        /// Drops the current attachment to the ground ( generates a new attachment pickeable )
        /// </summary>
        /// <param name="atc">Attachment to drop </param>
        /// <param name="enableDefault">Enables the default attachment when dropped if true.</param>
        public void DropAttachment(Attachment atc, bool enableDefault)
        {
            if (atc == null) return;

#if INVENTORY_PRO_ADD_ON
            // If Inventory Pro Add-On is installed and the Inventory is available in the scene, try to add to the Inventory
            if (InventoryProManager.instance != null)
            {
                TryAddAttachmentToInventory(atc.attachmentIdentifier);
            }
            else
            {
                InstantiateAttachmentPickeable(atc.attachmentIdentifier);
            }
#else
            if(dropAttachmentOnDettachUI) InstantiateAttachmentPickeable(atc.attachmentIdentifier);
#endif

            DeactivateCurrentAttachment(atc, enableDefault);

            // We should repaint
            if (displayCurrentAttachmentsOnly)
                Events.OnInspectionUIRefreshRequested?.Invoke(displayCurrentAttachmentsOnly);
        }

        private void DeactivateCurrentAttachment(Attachment atc, bool enableDefault)
        {
            // Grab the current weaponidentification object.
            WeaponIdentification wId = weaponReferences.Id;

            if (wId == null) return;

            Dictionary<AttachmentType, Attachment> attachments = wId.GetCurrentAttachments();

            // Grab what type of attachment it is, returns barrel, Scope, etc...
            AttachmentType attachmentType = atc.attachmentIdentifier.attachmentType;
            // Check if any of the attachments saved in the dictionary is the same type as the attachment to drop type.
            if (attachments.ContainsKey(attachmentType))
            {
                atc.Dettach(wId);

                // Check all the attachment types 
                // This will determine which attachment type matches the dropped attachment
                Attachment defaultAttachment = wId.GetDefaultAttachment(attachmentType);
                wId.DeactivateAttachment(attachmentType);

                // If the default attachment is not null, and we should enable default attachments, assign it and enable it
                if (defaultAttachment != null && enableDefault)
                {
                    attachments[attachmentType] = defaultAttachment;
                    defaultAttachment.gameObject.SetActive(true);
                }
                else
                {
                    // Otherwise do not assign anything
                    attachments[attachmentType] = null;
                }
            }
        }
#if INVENTORY_PRO_ADD_ON
        public void TryAddAttachmentToInventory(AttachmentIdentifier_SO atcIdentifier)
        {
            (bool atcAddedToInv, int amount) = InventoryProManager.instance._GridGenerator.AddItemToInventory(atcIdentifier, 1);
            // If the attachment couldnt be added to the Inventory, drop it.
            if (!atcAddedToInv) InstantiateAttachmentPickeable(atcIdentifier);
        }
#endif
        private void InstantiateAttachmentPickeable(AttachmentIdentifier_SO atcIdentifier)
        {
            // Spawn a new pickeable.
            AttachmentPickeable pick = Instantiate(attachmentGenericPickeable, orientation.Position + orientation.Forward * droppingDistance, orientation.Rotation) as AttachmentPickeable;
            // Assign the appropriate attachment identifier to the spawned pickeable.
            pick.attachmentIdentifier = atcIdentifier;
            // Get visuals
            pick.Drop(playerDependencies, orientation);
        }

        private void ResetInteractable() => highlightedInteractable = null;
    }
}
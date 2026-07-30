using cowsins;
using UnityEngine;

namespace cowsins
{
    public class CrouchSlideBehaviour
    {
        private MovementContext context;
        private Rigidbody rb;
        private InputManager inputManager;
        private IPlayerMovementStateProvider playerMovement;
        private IPlayerMovementEventsProvider playerEvents;

        private PlayerMovementSettings playerSettings;

        private PlayerOrientation orientation => playerMovement.Orientation;
        private float initialHeight;
        private Vector3 initialCenter;
        private Transform cameraHead;
        private float initialHeadLocalY;
        private bool canUnCrouch = false;
        private readonly Collider[] uncrouchOverlaps = new Collider[8];

        private float slideTimer = 0f;
        private Vector3 slideDirection = Vector3.zero;
        private float slideBoostRemaining = 0f;
        private bool isBoosting = false;

        public CrouchSlideBehaviour(MovementContext context)
        {
            this.context = context;
            if (context == null || context.Transform == null) return;

            this.rb = context.Rigidbody;
            this.inputManager = context.InputManager;

            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerEvents = context.Dependencies.PlayerMovementEvents;

            this.playerSettings = context.Settings;
            this.initialHeight = context.Capsule.height;
            this.initialCenter = context.Capsule.center;

            // Safe camera head detection
            cameraHead = context.Transform.Find("Head ( Camera Placement )");
            if (cameraHead == null)
            {
                var allTransforms = context.Transform.GetComponentsInChildren<Transform>(true);
                foreach (var t in allTransforms)
                {
                    if (t != context.Transform && t.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cameraHead = t;
                        break;
                    }
                }
            }

            if (cameraHead != null)
            {
                initialHeadLocalY = cameraHead.localPosition.y;
            }
            else
            {
                initialHeadLocalY = 1.55f; // Fallback for 1.75m height
            }

            playerEvents.Events.AllowSlide += AllowSliding;
        }

        public void Enter()
        {
            if (!playerSettings.allowCrouch) return;

            playerMovement.IsCrouching = true;

            playerSettings.events.OnCrouch.Invoke();
            playerEvents.Events.OnCrouchStart?.Invoke(); // Internal Event

            // Start sliding when conditions match, checking sprint/run speed scaled by player weight.
            float weightMult = context.Dependencies.PlayerMultipliers != null ? context.Dependencies.PlayerMultipliers.WeightMultiplier : 1f;
            float slideThreshold = (playerMovement.RunSpeed * weightMult) - 0.5f;

            if (rb.linearVelocity.magnitude >= slideThreshold && playerMovement.Grounded && playerSettings.allowSliding && !context.HasJumped)
            {
                Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                if (horizontalVel.magnitude > 0.1f)
                    slideDirection = horizontalVel.normalized;
                else
                    slideDirection = Vector3.ProjectOnPlane(orientation.Forward, Vector3.up).normalized;

                slideBoostRemaining = Mathf.Max(0.0001f, playerSettings.slideBoostDuration);
                isBoosting = true;
                playerMovement.IsSliding = true;

                // Begin local boost and timer.
                slideTimer = playerSettings.slideDuration;

                playerSettings.events.OnSlideStart.Invoke();
                playerEvents.Events.OnSlideStart?.Invoke();
            }
        }

        public void Tick() 
        {
            if (!inputManager.Crouching)
            {
                playerMovement.IsCrouching = false;
                ResizeCapsule(initialHeight, playerSettings.crouchUpMultiplier);
                return;
            }

            if (playerMovement.IsCrouching || isBoosting)
            {
                ResizeCapsule(playerSettings.crouchHeight, playerSettings.crouchDownMultiplier);
            }
        }

        public void FixedTick()
        {
            if (!playerMovement.IsSliding) return;

            // Apply initial boost
            if (isBoosting && slideBoostRemaining > 0f)
            {
                float dt = Time.fixedDeltaTime;
                // Distribute the configured slideForce across the boost duration as acceleration
                float boostAmount = playerSettings.slideForce * dt / Mathf.Max(playerSettings.slideBoostDuration, dt);
                rb.AddForce(slideDirection * boostAmount, ForceMode.Acceleration);
                slideBoostRemaining -= dt;
                if (slideBoostRemaining <= 0f) isBoosting = false;
            }

            // Steering while sliding
            Vector3 inputDir = (orientation.Forward * inputManager.Y + orientation.Right * inputManager.X);
            inputDir.y = 0;
            if (inputDir.sqrMagnitude > 0.0001f)
            {
                Vector3 steer = inputDir.normalized * playerSettings.acceleration * playerSettings.slideSteerMultiplier * Time.fixedDeltaTime;
                rb.AddForce(steer, ForceMode.Acceleration);
            }

            // Decrease timer and check for stop condition
            slideTimer -= Time.fixedDeltaTime;
            Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            if (slideTimer <= 0 || horizontalVel.magnitude < playerSettings.slideStopSpeed || !playerMovement.Grounded)
            {
                EndSlide();
            }
        }

        public void Exit()
        {
            playerEvents.Events.OnCrouchStop?.Invoke();
            playerSettings.events.OnStopCrouch?.Invoke();
            EndSlide();
        }

        public bool CanExecute()
        {
            return inputManager.Crouching && !playerMovement.IsWallRunning && playerSettings.allowCrouch && (playerMovement.Grounded || !playerMovement.Grounded && playerSettings.allowCrouchWhileJumping);
        }

        public void HandleCrouch()
        {
            if (inputManager.Crouching)
            {
                ResizeCapsule(playerSettings.crouchHeight, playerSettings.crouchDownMultiplier);
            }

            playerMovement.IsCrouching = true;

            playerEvents.Events.OnCrouching?.Invoke();
        }

        public bool CheckUnCrouch()
        {
            // Performance optimization: skip physical capsule sweep if already fully stood up
            if (Mathf.Approximately(context.Capsule.height, initialHeight))
            {
                playerMovement.IsCrouching = false;
                return true;
            }

            if (inputManager.Crouching) return false;

            float checkRadius = context.Capsule.radius * 0.95f;
            float standingHeight = initialHeight;
            float bottomOffset = initialCenter.y - initialHeight * 0.5f;

            // Calculate the capsule centers at the player's current feet position for a standing capsule.
            Vector3 bottomSphereCenter = context.Transform.position + context.Transform.up * (bottomOffset + checkRadius);
            Vector3 topSphereCenter = context.Transform.position + context.Transform.up * (bottomOffset + standingHeight - checkRadius);

            int count = Physics.OverlapCapsuleNonAlloc(bottomSphereCenter, topSphereCenter, checkRadius, uncrouchOverlaps, context.WhatIsGround, QueryTriggerInteraction.Ignore);
            
            bool isObstacleAbove = false;
            for (int i = 0; i < count; i++)
            {
                if (uncrouchOverlaps[i].transform.IsChildOf(context.Transform))
                {
                    continue;
                }
                isObstacleAbove = true;
                break;
            }
            canUnCrouch = !isObstacleAbove;

            if (isObstacleAbove)
            {
                // Force the player to shrink back to crouch height rather than remaining at a broken intermediate height
                ResizeCapsule(playerSettings.crouchHeight, playerSettings.crouchDownMultiplier);
                return false;
            }

            // Resume standing up
            ResizeCapsule(standingHeight, playerSettings.crouchUpMultiplier);

            if (Mathf.Approximately(context.Capsule.height, standingHeight))
            {
                playerMovement.IsCrouching = false;
                return true;
            }

            return false;
        }

        private bool AllowSliding() => playerSettings.allowSliding;

        private void EndSlide()
        {
            if (!playerMovement.IsSliding) return;

            playerMovement.IsSliding = false;
            //playerEvents.Events.OnSlideEnd?.Invoke();
            playerSettings.events.OnStopCrouch?.Invoke();

            Vector3 vel = rb.linearVelocity;
            Vector3 horizontal = new Vector3(vel.x, 0, vel.z);

            rb.linearVelocity = new Vector3(horizontal.magnitude > 0 ? horizontal.normalized.x * Mathf.Max(0, horizontal.magnitude * 0.6f) : 0, vel.y, horizontal.magnitude > 0 ? horizontal.normalized.z * Mathf.Max(0, horizontal.magnitude * 0.6f) : 0);
        }

        private void ResizeCapsule(float targetHeight, float speedMultiplier)
        {
            CapsuleCollider capsule = context.Capsule;
            if (capsule == null) return;

            float currentH = Mathf.MoveTowards(capsule.height, targetHeight, Time.deltaTime * playerSettings.crouchTransitionSpeed * speedMultiplier);
            capsule.height = currentH;

            float bottomOffset = initialCenter.y - initialHeight * 0.5f;
            capsule.center = new Vector3(initialCenter.x, bottomOffset + currentH * 0.5f, initialCenter.z);

            UpdateHeadPosition(capsule);
        }

        private void UpdateHeadPosition(CapsuleCollider capsule)
        {
            if (cameraHead != null && initialHeight > 0 && capsule != null)
            {
                float bottomOffset = initialCenter.y - initialHeight * 0.5f;
                float targetHeadY = bottomOffset + (initialHeadLocalY - bottomOffset) * (capsule.height / initialHeight);
                cameraHead.localPosition = new Vector3(cameraHead.localPosition.x, targetHeadY, cameraHead.localPosition.z);
            }
        }
    }
}

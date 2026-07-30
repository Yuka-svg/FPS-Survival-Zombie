using UnityEngine;
using System.Collections;

namespace cowsins
{
    public class GroundDetectionBehaviour
    {
        private MovementContext context;
        private Rigidbody rb;
        private InputManager inputManager;
        private IPlayerMovementStateProvider playerMovement;
        private IPlayerMovementEventsProvider playerEvents;

        private PlayerOrientation orientation => playerMovement?.Orientation;

        private CapsuleCollider playerCapsuleCollider;

        private PlayerMovementSettings playerSettings;

        private Vector3 playerScale;

        // Slopes
        private float maxSlopeAngle = 60f;

        // Landing Cooldown
        private float lastLandingTime = -1f;
        private const float landingCooldown = 0.2f;

        // Ground Buffer
        private int groundedFrameCount = 0;
        private int notGroundedFrameCount = 0;
        private const int requiredFrames = 2;

        public GroundDetectionBehaviour(MovementContext context)
        {
            this.context = context;
            this.rb = context.Rigidbody;
            this.inputManager = context.InputManager;
            this.playerMovement = context.Dependencies.PlayerMovementState;
            this.playerEvents = context.Dependencies.PlayerMovementEvents;

            this.playerSettings = context.Settings;
            this.playerCapsuleCollider = context.Capsule;

            this.playerScale = context.Transform.localScale;
        }

        private bool _foundGroundThisFixedUpdate;
        private RaycastHit _groundHitThisFixedUpdate;

        public void FixedTick()
        {
            _foundGroundThisFixedUpdate = PerformGroundCheck(out _groundHitThisFixedUpdate);
        }

        public void Tick()
        {
            // Use ground check performed in FixedUpdate
            bool foundGroundThisFrame = _foundGroundThisFixedUpdate;
            RaycastHit hit = _groundHitThisFixedUpdate;

            context.IsPlayerOnSlope = IsPlayerOnSlope(hit, foundGroundThisFrame);

            // Store the latest ground hit so other behaviours can use the slope normal
            context.SlopeHit = hit;

            // Buffered grounding system
            bool wasGrounded = playerMovement.Grounded;

            // Safe first-frame landing detection to prevent physics bouncing
            if (foundGroundThisFrame && !wasGrounded && groundedFrameCount == 0)
            {
                if (rb.linearVelocity.y < -0.1f)
                {
                    if (!playerMovement.IsClimbing && !playerMovement.IsWallRunning && !playerMovement.IsDashing)
                    {
                        Vector3 vel = rb.linearVelocity;
                        vel.y = 0;
                        rb.linearVelocity = vel;
                    }
                }
            }

            bool newGroundedState = UpdateGroundedState(foundGroundThisFrame, wasGrounded);

            if (newGroundedState != wasGrounded)
            {
                // Just landed
                if (newGroundedState)
                {
                    HandleLanding();
                }
                else // Just left ground
                {
                    HandleLeavingGround();
                }
            }

            playerMovement.Grounded = newGroundedState;
        }

        private bool UpdateGroundedState(bool foundGroundThisFrame, bool wasGrounded)
        {
            if (foundGroundThisFrame)
            {
                groundedFrameCount++;
                notGroundedFrameCount = 0;

                // Become grounded after consistent detection
                if (!wasGrounded && groundedFrameCount >= requiredFrames)
                {
                    return true;
                }
                else if (wasGrounded)
                {
                    return true; // Stay grounded
                }
            }
            else
            {
                notGroundedFrameCount++;
                groundedFrameCount = 0;
                return false; // Leave ground immediately
            }

            return wasGrounded;
        }

        private void HandleLanding()
        {
            if (Time.time - lastLandingTime < landingCooldown)
            {
                return;
            }

            lastLandingTime = Time.time;

            playerEvents.Events.OnLand?.Invoke();
            playerSettings.events.OnLand.Invoke();
            SoundManager.Instance.PlaySound(playerSettings.sounds.landSFX, 0, 0, false);
        }

        private void HandleLeavingGround()
        {
            context.CoyoteTimer = context.CoyoteJumpTime;
        }

        private void ResetGroundingState()
        {
            playerMovement.Grounded = true;
            groundedFrameCount = requiredFrames;
            notGroundedFrameCount = 0;
        }

        /// <summary>
        /// Determines whether the player is on a slope.
        /// </summary>
        private bool IsPlayerOnSlope(RaycastHit hit, bool isGrounded)
        {
            if (isGrounded)
            {
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                return angle < maxSlopeAngle && angle != 0;
            }
            return false;
        }

        private bool IsSliding()
        {
            return playerMovement.IsCrouching && rb.linearVelocity.magnitude > playerMovement.WalkSpeed;
        }

        /// <summary>
        /// Checks if the player is grounded through physics raycast.
        /// </summary>
        private bool PerformGroundCheck(out RaycastHit hit)
        {
            // Get capsule center
            Vector3 center = playerCapsuleCollider.transform.TransformPoint(playerCapsuleCollider.center);

            // Use actual physical height since crouching now directly resizes the collider
            float height = playerCapsuleCollider.height;

            float radius = playerCapsuleCollider.radius * 0.95f;
            float halfHeight = Mathf.Max(0, (height * 0.5f) - radius);

            // Calculate capsule endpoints from center
            Vector3 bottom = center - Vector3.up * halfHeight;
            Vector3 top = center + Vector3.up * halfHeight;

            // Add small offset
            float startOffset = 0.1f;
            bottom += Vector3.up * startOffset;
            top += Vector3.up * startOffset;

            // Perform the cast
            float castDistance = playerSettings.groundCheckDistance + startOffset;
            bool foundGround = Physics.CapsuleCast(
                top,
                bottom,
                radius,
                Vector3.down,
                out hit,
                castDistance,
                context.WhatIsGround,
                QueryTriggerInteraction.Ignore
            );

            if (foundGround && CowsinsUtilities.IsFloor(hit.normal, maxSlopeAngle))
            {
                return true;
            }

            // Fallbacks to Raycast
            Vector3 rayOrigin = center + Vector3.up * startOffset;
            if (Physics.Raycast(rayOrigin, Vector3.down, out hit,
                castDistance, context.WhatIsGround, QueryTriggerInteraction.Ignore))
            {
                return CowsinsUtilities.IsFloor(hit.normal, maxSlopeAngle);
            }

            return false;
        }
    }
}
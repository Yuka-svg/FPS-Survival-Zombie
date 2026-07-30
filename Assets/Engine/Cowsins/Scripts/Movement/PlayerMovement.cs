/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace cowsins
{
    // Add a rigidbody if needed, PlayerMovement.cs requires a rigidbody to work 
    [RequireComponent(typeof(Rigidbody))]
    //[RequireComponent(typeof(____))] Player Movement also requires a non trigger collider. Attach your preffered collider method
    public class PlayerMovement : MonoBehaviour, IPlayerMovementStateProvider, IPlayerMovementEventsProvider
    {
        #region Settings
        public PlayerMovementEvents Events { get; private set; } = new PlayerMovementEvents();

        public PlayerMovementSettings playerSettings = new PlayerMovementSettings();

        #endregion

        #region IPlayerMovementProvider

        // We need to satisfy the required interfaces
        public PlayerOrientation Orientation { get { return orientation; } set { orientation = value; } }
        public bool IsIdle => rb.linearVelocity.magnitude < .1f;
        public float CurrentSpeed { get; set; }
        public float RunSpeed => playerSettings.runSpeed;
        public float WalkSpeed => playerSettings.walkSpeed;
        public float CrouchSpeed => playerSettings.crouchSpeed;
        public bool Grounded { get; set; }
        public bool IsCrouching { get; set; }
        public bool IsSliding { get; set; }  
        public bool IsClimbing { get; set; }
        public bool IsWallRunning { get; set; }
        public bool IsDashing { get; set; }
        public bool CanShootWhileDashing => playerSettings.canShootWhileDashing;
        public bool DamageProtectionWhileDashing => playerSettings.damageProtectionWhileDashing;
        public float NormalFOV => playerSettings.normalFOV;
        public float FadeFOVAmount => playerSettings.fadeFOVAmount;
        public float WallRunningFOV => playerSettings.wallrunningFOV;
        public float RunningFOV => playerSettings.runningFOV;
        public bool AlternateSprint => playerSettings.alternateSprint;
        public bool AlternateCrouch => playerSettings.alternateCrouch;
        public float OriginalCapsuleHeight { get; private set; }
        public float CurrentCapsuleHeight => playerCapsuleCollider != null ? playerCapsuleCollider.height : 1.75f;

        private PlayerOrientation orientation = new PlayerOrientation(Vector3.zero, Quaternion.identity);

        #endregion

        #region Internal References

        private PlayerDependencies playerDependencies;
        private PlayerMultipliers playerMultipliers;
        private Rigidbody rb;
        private CapsuleCollider playerCapsuleCollider;
        private PlayerStates playerStates;
        private InputManager inputManager;

        #endregion

        #region Others

        private const float extraGravityForce = 30.19f;
        public bool showCapsuleGroundCheckDebugInfo = false;

        #endregion

        #region Behaviours
        public UnityEngine.Events.UnityEvent<float> OnStepClimb = new UnityEngine.Events.UnityEvent<float>();
        public MovementContext movementContext { get; private set; }
        public StaminaBehaviour staminaBehaviour { get; private set; }
        public GroundDetectionBehaviour groundDetectionBehaviour { get; private set; }
        public SpeedLinesBehaviour speedLinesBehaviour { get; private set; }
        public FootstepsBehaviour footstepsBehaviour { get; private set; }
        public VelocityHandlerBehaviour velocityHandlerBehaviour { get; private set; }
        public BasicMovementBehaviour basicMovementBehaviour { get; private set; }
        public CameraLookBehaviour cameraLookBehaviour { get; private set; }
        public JumpBehaviour jumpBehaviour { get; private set; }
        public CrouchSlideBehaviour crouchSlideBehaviour { get; private set; }
        public DashBehaviour dashBehaviour { get; private set; }
        public WallBounceBehaviour wallBounceBehaviour { get; private set; }
        public ClimbLadderBehaviour climbLadderBehaviour { get; private set; }
        public WallRunBehaviour wallRunBehaviour { get; private set; }
        public GrapplingHookBehaviour grapplingHookBehaviour { get; private set; }

        #endregion

        #region Basic
        private void OnEnable() => Events.OnRespawn.AddListener(TeleportPlayer);

        private void OnDisable() => Events.OnRespawn.RemoveListener(TeleportPlayer);

        private void Start()
        {
            GetDependencies();
            ConfigureRigidbody();
            playerSettings.events.OnSpawn.Invoke();

            inputManager.SetPlayerInputModes(playerSettings);

            OriginalCapsuleHeight = playerCapsuleCollider != null ? playerCapsuleCollider.height : 1.75f;

            InitializeBehaviours();
        }

        private void Update()
        {
            groundDetectionBehaviour?.Tick();
            jumpBehaviour?.Tick();
        }

        private void FixedUpdate()
        {
            groundDetectionBehaviour?.FixedTick();
            
            // Centralized Custom Gravity
            if (!IsClimbing)
            {
                float gravityForce = extraGravityForce;

                // Check if standing still on a stable slope using vertical raycast
                bool standingStillOnSlope = false;
                Vector3 slopeNormal = Vector3.up;
                if (Grounded && movementContext != null && movementContext.IsPlayerOnSlope)
                {
                    bool noInput = inputManager.X == 0f && inputManager.Y == 0f;
                    if (noInput)
                    {
                        // Cast a vertical ray down from center to get true slope normal (prevents edge repulsion bugs)
                        if (Physics.Raycast(rb.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit trueSlopeHit, 0.4f, playerSettings.whatIsGround, QueryTriggerInteraction.Ignore))
                        {
                            slopeNormal = trueSlopeHit.normal;
                            float slopeAngle = Vector3.Angle(Vector3.up, slopeNormal);
                            if (slopeAngle > 0.01f && slopeAngle <= 45f)
                            {
                                standingStillOnSlope = true;
                            }
                        }
                    }
                }

                if (standingStillOnSlope)
                {
                    // Apply only the perpendicular component of gravity to press the player into the slope
                    Vector3 gravityVec = Vector3.down * gravityForce;
                    Vector3 gravityPerpendicular = Vector3.Dot(gravityVec, slopeNormal) * slopeNormal;
                    rb.AddForce(gravityPerpendicular, ForceMode.Acceleration);
                }
                else if (IsWallRunning)
                {
                    if (playerSettings.useGravity)
                    {
                        rb.AddForce(Vector3.down * gravityForce, ForceMode.Acceleration);
                    }
                }
                else
                {
                    rb.AddForce(Vector3.down * gravityForce, ForceMode.Acceleration);
                }
            }

            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, playerSettings.maxSpeedAllowed);

            staminaBehaviour?.Tick();
        }

        /// <summary>
        /// Basically find everything the script needs to work
        /// </summary>
        private void GetDependencies()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            rb = GetComponent<Rigidbody>();
            playerStates = GetComponent<PlayerStates>();
            playerMultipliers = GetComponent<PlayerMultipliers>();
            playerCapsuleCollider = GetComponent<CapsuleCollider>();
            inputManager = playerDependencies.InputManager;

            if (playerSettings.playerCam == null) CowsinsUtilities.LogWarning("PlayerCam is null in Player > PlayerMovement > Assignables. Skipping Camera Look", this);
            if (playerSettings.cameraFOVManager == null) CowsinsUtilities.LogWarning("CameraFOVManager is null in Player > PlayerMovement > Assignables.", this);
            if (playerSettings.useSpeedLines && playerSettings.speedLines == null) 
                CowsinsUtilities.LogWarning("SpeedLines Particle Effect is null in Player > PlayerMovement > Assignables.", this);
            if (playerSettings.usesStamina && playerSettings.staminaSlider == null)
            {
                if (FindAnyObjectByType<StaminaWidget>() == null)
                    CowsinsUtilities.LogWarning("Stamina Slider is null in Player > PlayerMovement > Stamina. Skipping Stamina UI", this);
            }
        }

        private void ConfigureRigidbody()
        {
            if (rb == null) return;

            rb.freezeRotation = true;
            rb.useGravity = false; // Disable default Unity gravity globally

            // Use ContinuousDynamic to prevent physics tunneling
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Set interpolation for smooth movement
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // Ensure no drag, we handle friction manually
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;

            // Increase contact offset for more stable collisions on mesh colliders (stairs, rough terrain)
            if (playerCapsuleCollider != null && playerCapsuleCollider.contactOffset < 0.05f)
            {
                playerCapsuleCollider.contactOffset = 0.05f;
            }
        }

        private void InitializeBehaviours()
        {
            movementContext = new MovementContext
            {
                Transform = this.transform,
                Rigidbody = rb,
                Capsule = playerCapsuleCollider,
                Camera = playerSettings.playerCam,
                WhatIsGround = playerSettings.whatIsGround,
                InputManager = inputManager,
                Settings = playerSettings,
                Dependencies = playerDependencies,
                CoyoteJumpTime = playerSettings.coyoteJumpTime,
            };

            groundDetectionBehaviour = new GroundDetectionBehaviour(movementContext);
            basicMovementBehaviour = new BasicMovementBehaviour(movementContext);
            velocityHandlerBehaviour = new VelocityHandlerBehaviour(movementContext);
            cameraLookBehaviour = new CameraLookBehaviour(movementContext);
            jumpBehaviour = new JumpBehaviour(movementContext);
            crouchSlideBehaviour = new CrouchSlideBehaviour(movementContext);
            dashBehaviour = new DashBehaviour(movementContext);
            wallBounceBehaviour = new WallBounceBehaviour(movementContext);
            climbLadderBehaviour = new ClimbLadderBehaviour(movementContext);
            wallRunBehaviour = new WallRunBehaviour(movementContext);
            grapplingHookBehaviour = new GrapplingHookBehaviour(movementContext);
            staminaBehaviour = new StaminaBehaviour(movementContext);
            footstepsBehaviour = new FootstepsBehaviour(movementContext);
            speedLinesBehaviour = new SpeedLinesBehaviour(movementContext);
        }
        #endregion

        #region Collisions
        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag("Weapons"))
            {
                Physics.IgnoreCollision(collision.collider, playerCapsuleCollider);
            }
        }

        #endregion

        #region Utils

        /// <summary>
        /// Teleport the player to the specified position and rotation.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void TeleportPlayer(Vector3 position, Quaternion rotation, bool resetStamina, bool resetDashes)
        {
            rb.position = position;
            playerSettings.playerCam.rotation = rotation;

            if(resetStamina) staminaBehaviour?.ResetStamina();
            if(resetDashes) dashBehaviour?.ResetDashes();

            playerStates.ForceChangeState(playerStates._States.Default());
        }
        #endregion

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (playerCapsuleCollider == null)
            {
                if (!TryGetComponent<CapsuleCollider>(out playerCapsuleCollider))
                    return;
            }

            Vector3 center = playerCapsuleCollider.transform.TransformPoint(playerCapsuleCollider.center);
            float halfHeight = Mathf.Max(0, (playerCapsuleCollider.height * 0.5f) - playerCapsuleCollider.radius);
            float radius = playerCapsuleCollider.radius * 0.95f;

            Vector3 bottom = center - Vector3.up * halfHeight;
            Vector3 top = center + Vector3.up * halfHeight;

            Vector3 castOffset = Vector3.down * playerSettings.groundCheckDistance;
            Vector3 bottomCast = bottom + castOffset;
            Vector3 topCast = top + castOffset;

            if(showCapsuleGroundCheckDebugInfo)
                DrawCapsule(bottomCast, topCast, radius, new Color(0f, 0f, 1f, 0.3f));
        }

        private void DrawCapsule(Vector3 bottom, Vector3 top, float radius, Color color)
        {
            Gizmos.color = color;

            Gizmos.DrawWireSphere(bottom, radius);
            Gizmos.DrawWireSphere(top, radius);

            Gizmos.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
            Gizmos.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
            Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
            Gizmos.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
        }

#endif
    }
}
using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// Movement branch of the skill tree. Governs walk/run speed, air control,
    /// dash unlock, wall run/bounce unlock, grapple force, and double jump.
    ///
    /// Pattern matches <see cref="AimSkillSystem"/> and
    /// <see cref="IntelligenceSkillSystem"/>: <see cref="SkillTreeManager"/>
    /// calls <see cref="RefreshStats"/> whenever the movement level changes.
    /// Base stat values are captured in Awake so re-applying levels always
    /// starts from the prefab defaults (not from the previously-modified values).
    /// </summary>
    public class MovementSkillSystem : MonoBehaviour
    {
        [Header("Runtime Stats")]
        [SerializeField] private float walkSpeedMultiplier = 1f;
        [SerializeField] private float runSpeedMultiplier = 1f;
        [SerializeField] private float airControlMultiplier = 1f;
        [SerializeField] private float grappleForceMultiplier = 1f;
        [SerializeField] private bool dashUnlocked;
        [SerializeField] private bool wallRunUnlocked;
        [SerializeField] private bool wallBounceUnlocked;
        [SerializeField] private bool doubleJumpUnlocked;

        public float WalkSpeedMultiplier => walkSpeedMultiplier;
        public float RunSpeedMultiplier => runSpeedMultiplier;
        public float AirControlMultiplier => airControlMultiplier;
        public float GrappleForceMultiplier => grappleForceMultiplier;
        public bool DashUnlocked => dashUnlocked;
        public bool WallRunUnlocked => wallRunUnlocked;
        public bool WallBounceUnlocked => wallBounceUnlocked;
        public bool DoubleJumpUnlocked => doubleJumpUnlocked;

        private PlayerMovement movement;

        // Base values captured at Awake so RefreshStats always starts fresh.
        private float baseWalkSpeed;
        private float baseRunSpeed;
        private float baseAirControl;
        private float baseGrappleForce;
        private int baseMaxJumps;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            if (movement == null)
                movement = FindAnyObjectByType<PlayerMovement>();

            if (movement == null)
            {
                Debug.LogError("[MovementSkillSystem] PlayerMovement not found!");
                return;
            }

            baseWalkSpeed = movement.playerSettings.walkSpeed;
            baseRunSpeed = movement.playerSettings.runSpeed;
            baseAirControl = movement.playerSettings.controlAirborne;
            baseGrappleForce = movement.playerSettings.grappleForce;
            // The skill tree governs double-jump unlock (movement level 5), so
            // the base jump count is always 1 regardless of the prefab value.
            baseMaxJumps = 1;
        }

        /// <summary>
        /// Re-applies all movement stats from scratch based on the current
        /// movement skill level. Called by SkillTreeManager whenever the level
        /// changes (upgrade) or on initial Awake.
        /// </summary>
        public void RefreshStats(int movementLevel)
        {
            if (movement == null) return;

            // Reset multipliers/flags before re-applying.
            walkSpeedMultiplier = 1f;
            runSpeedMultiplier = 1f;
            airControlMultiplier = 1f;
            grappleForceMultiplier = 1f;
            dashUnlocked = false;
            wallRunUnlocked = false;
            wallBounceUnlocked = false;
            doubleJumpUnlocked = false;

            // Restore base values before stacking multipliers.
            movement.playerSettings.walkSpeed = baseWalkSpeed;
            movement.playerSettings.runSpeed = baseRunSpeed;
            movement.playerSettings.controlAirborne = baseAirControl;
            movement.playerSettings.grappleForce = baseGrappleForce;
            movement.playerSettings.maxJumps = baseMaxJumps;
            movement.playerSettings.canWallRun = false;
            movement.playerSettings.canWallBounce = false;
            movement.playerSettings.canDash = false;

            if (movementLevel >= 1)
            {
                walkSpeedMultiplier = 1.05f;
                movement.playerSettings.walkSpeed *= walkSpeedMultiplier;
            }

            if (movementLevel >= 2)
            {
                runSpeedMultiplier = 1.10f;
                movement.playerSettings.runSpeed *= runSpeedMultiplier;
            }

            if (movementLevel >= 3)
            {
                airControlMultiplier = 1.15f;
                movement.playerSettings.controlAirborne =
                    Mathf.Clamp01(baseAirControl * airControlMultiplier);
                // Dash unlock (moved from level 5). Only (re)enable and reset
                // charges when it was previously disabled. Without this,
                // DashBehaviour.currentDashes stays at 0 (the constructor skips
                // initialization when canDash is false), so CanExecute() would
                // always return false even after unlocking.
                if (!movement.playerSettings.canDash)
                {
                    dashUnlocked = true;
                    movement.playerSettings.canDash = true;
                    movement.dashBehaviour?.ResetDashes();
                }
            }

            if (movementLevel >= 4)
            {
                wallRunUnlocked = true;
                wallBounceUnlocked = true;
                movement.playerSettings.canWallRun = true;
                movement.playerSettings.canWallBounce = true;
            }

            if (movementLevel >= 5)
            {
                runSpeedMultiplier *= 1.25f;
                grappleForceMultiplier = 1.25f;
                movement.playerSettings.runSpeed *= 1.25f;
                movement.playerSettings.grappleForce *= grappleForceMultiplier;
                // Double jump unlock: ensure at least 2 jumps. jumpCount is
                // refreshed automatically by JumpBehaviour.Tick when grounded,
                // or on the next OnLand event.
                if (movement.playerSettings.maxJumps < 2)
                {
                    doubleJumpUnlocked = true;
                    movement.playerSettings.maxJumps = 2;
                }
            }

#if UNITY_EDITOR
            Debug.Log(
                $"MOVEMENT LVL {movementLevel} | Walk:{walkSpeedMultiplier:F2}x | Run:{runSpeedMultiplier:F2}x | " +
                $"Air:{airControlMultiplier:F2}x | Grapple:{grappleForceMultiplier:F2}x | " +
                $"Dash:{dashUnlocked} | WallRun:{wallRunUnlocked} | DoubleJump:{doubleJumpUnlocked}"
            );
#endif
        }
    }
}

using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// PlayerFallSafety detects when the player falls off the world map (e.g. Y <= -10f)
    /// and teleports them back to the last safe grounded position or initial spawn point.
    /// </summary>
    public class PlayerFallSafety : MonoBehaviour
    {
        [Header("Fall Safety Settings")]
        [Tooltip("Absolute world Y position threshold. Falling below this Y coordinate triggers out-of-bounds recovery.")]
        [SerializeField] private float fallThresholdY = -10f;

        [Tooltip("If true, attempts to teleport the player back to their last safe grounded position. Otherwise uses initial spawn point.")]
        [SerializeField] private bool useLastGroundedPosition = true;

        [Tooltip("Minimum continuous time (seconds) standing safely grounded before recording last safe position.")]
        [SerializeField] private float safeGroundMinDuration = 0.5f;

        [Tooltip("Cooldown timer (seconds) between fall safety teleport triggers to prevent physics loop/jitter.")]
        [SerializeField] private float teleportCooldown = 1.0f;

        private PlayerMovement playerMovement;
        private PlayerMovementEvents playerEvents;

        private Vector3 initialSpawnPosition;
        private Quaternion initialSpawnRotation;

        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;
        private bool hasValidLastSafePosition = false;

        private float groundedTimer = 0f;
        private float cooldownTimer = 0f;

        private void Start()
        {
            playerMovement = GetComponent<PlayerMovement>();
            var dependencies = GetComponent<PlayerDependencies>();
            if (dependencies != null && dependencies.PlayerMovementEvents != null)
            {
                playerEvents = dependencies.PlayerMovementEvents.Events;
            }

            initialSpawnPosition = transform.position;
            initialSpawnRotation = transform.rotation;
            lastSafePosition = initialSpawnPosition;
            lastSafeRotation = initialSpawnRotation;
            hasValidLastSafePosition = IsCapsuleSpaceSafe(initialSpawnPosition);
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (playerMovement != null && playerMovement.Grounded && transform.position.y > fallThresholdY)
            {
                groundedTimer += Time.deltaTime;
                if (groundedTimer >= safeGroundMinDuration)
                {
                    if (IsCapsuleSpaceSafe(transform.position))
                    {
                        lastSafePosition = transform.position;
                        lastSafeRotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                        hasValidLastSafePosition = true;
                    }
                }
            }
            else
            {
                groundedTimer = 0f;
            }
        }

        private void FixedUpdate()
        {
            if (cooldownTimer <= 0f && transform.position.y <= fallThresholdY)
            {
                cooldownTimer = teleportCooldown;

                Vector3 targetPosition = initialSpawnPosition;
                Quaternion targetRotation = initialSpawnRotation;

                if (useLastGroundedPosition && hasValidLastSafePosition && IsCapsuleSpaceSafe(lastSafePosition))
                {
                    targetPosition = lastSafePosition;
                    targetRotation = lastSafeRotation;
                }

                if (playerEvents != null && playerEvents.OnRespawn != null)
                {
                    playerEvents.OnRespawn.Invoke(targetPosition, targetRotation, true, true);
                }
                else if (playerMovement != null)
                {
                    playerMovement.TeleportPlayer(targetPosition, targetRotation, true, true);
                }
            }
        }

        /// <summary>
        /// Checks whether standing at pos has clear capsule space above and solid ground below.
        /// </summary>
        private bool IsCapsuleSpaceSafe(Vector3 pos)
        {
            if (playerMovement == null || playerMovement.playerSettings == null) return true;

            CapsuleCollider col = GetComponent<CapsuleCollider>();
            float radius = col != null ? col.radius : 0.4f;
            float checkRadius = radius * 0.8f;

            float height = playerMovement != null ? playerMovement.OriginalCapsuleHeight : 1.75f;
            Vector3 bottom = pos + Vector3.up * (radius + 0.2f);
            Vector3 top = pos + Vector3.up * Mathf.Max(radius + 0.25f, height - radius);
            LayerMask mask = playerMovement.playerSettings.whatIsGround;

            // Check downward raycast to ensure there's terrain ground underneath
            bool hasGroundBelow = Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 0.5f, mask, QueryTriggerInteraction.Ignore);
            if (!hasGroundBelow) return false;

            // Check clear capsule space above floor to avoid ceiling/wall overlaps
            return !Physics.CheckCapsule(bottom, top, checkRadius, mask, QueryTriggerInteraction.Ignore);
        }
    }
}

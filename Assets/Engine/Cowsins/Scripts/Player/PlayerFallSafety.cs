using UnityEngine;

namespace cowsins
{
    /// <summary>
    /// PlayerFallSafety detects when the player falls off the world map (e.g. Y <= -10f)
    /// and teleports them back to the last safe grounded position or initial spawn point.
    /// Also supports hotkey reset (e.g. F8) and direct C# / Event / ContextMenu triggers.
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

        [Header("Hotkey & Direct Reset Settings")]
        [Tooltip("Enable keyboard shortcut for resetting player position.")]
        [SerializeField] private bool allowHotkeyReset = true;

        [Tooltip("Hotkey to trigger player position reset.")]
        [SerializeField] private KeyCode resetHotkey = KeyCode.F8;

        public KeyCode ResetHotkey
        {
            get => resetHotkey;
            set
            {
                resetHotkey = value;
                UpdateCachedKey();
            }
        }

        private PlayerMovement playerMovement;
        private PlayerMovementEvents playerEvents;
        private PlayerStats playerStats;

        private Vector3 initialSpawnPosition;
        private Quaternion initialSpawnRotation;

        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;
        private bool hasValidLastSafePosition = false;

        private float groundedTimer = 0f;
        private float cooldownTimer = 0f;

        private KeyCode prevHotkey;

#if ENABLE_INPUT_SYSTEM
        private UnityEngine.InputSystem.Key cachedInputKey = UnityEngine.InputSystem.Key.None;
#endif

        private void Awake()
        {
            UpdateCachedKey();
        }

        private void OnValidate()
        {
            UpdateCachedKey();
        }

        private void Start()
        {
            playerMovement = GetComponent<PlayerMovement>();
            playerStats = GetComponent<PlayerStats>();

            var dependencies = GetComponent<PlayerDependencies>();
            if (dependencies != null)
            {
                if (dependencies.PlayerMovementEvents != null)
                {
                    playerEvents = dependencies.PlayerMovementEvents.Events;
                }
                if (playerStats == null && dependencies.PlayerStats != null)
                {
                    playerStats = dependencies.PlayerStats;
                }
            }

            initialSpawnPosition = transform.position;
            initialSpawnRotation = transform.rotation;
            lastSafePosition = initialSpawnPosition;
            lastSafeRotation = initialSpawnRotation;
            hasValidLastSafePosition = IsCapsuleSpaceSafe(initialSpawnPosition);

            UpdateCachedKey();
        }

        private void Update()
        {
            if (resetHotkey != prevHotkey)
            {
                UpdateCachedKey();
            }

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

            if (IsInputBlocked()) return;

#if ENABLE_INPUT_SYSTEM
            if (allowHotkeyReset && UnityEngine.InputSystem.Keyboard.current != null && cachedInputKey != UnityEngine.InputSystem.Key.None)
            {
                if (UnityEngine.InputSystem.Keyboard.current[cachedInputKey].wasPressedThisFrame)
                {
                    TriggerResetPosition(ignoreCooldown: false);
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
            if (allowHotkeyReset && Input.GetKeyDown(resetHotkey))
            {
                TriggerResetPosition(ignoreCooldown: false);
            }
#endif
        }

        private void FixedUpdate()
        {
            if (cooldownTimer <= 0f && transform.position.y <= fallThresholdY && (playerStats == null || !playerStats.IsDead))
            {
                ExecuteReset();
            }
        }

        /// <summary>
        /// Public API to trigger player position reset.
        /// </summary>
        public void TriggerResetPosition(bool ignoreCooldown = true, bool ignoreStateBlock = false)
        {
            if (!enabled || !gameObject.activeInHierarchy || !Application.isPlaying) return;
            if (playerStats != null && playerStats.IsDead) return;
            if (!ignoreStateBlock && IsInputBlocked()) return;

            if (!ignoreCooldown && cooldownTimer > 0f) return;

            ExecuteReset();
        }

        /// <summary>
        /// Executes immediate position reset and starts teleport cooldown.
        /// </summary>
        private void ExecuteReset()
        {
            cooldownTimer = teleportCooldown;

            Vector3 targetPosition = initialSpawnPosition;
            Quaternion targetRotation = initialSpawnRotation;
            bool foundSafePoint = false;

            if (useLastGroundedPosition && hasValidLastSafePosition)
            {
                if (IsCapsuleSpaceSafe(lastSafePosition, 0.5f, true))
                {
                    targetPosition = lastSafePosition;
                    targetRotation = lastSafeRotation;
                    foundSafePoint = true;
                }
                else
                {
                    for (int step = 1; step <= 3; step++)
                    {
                        Vector3 stepPos = lastSafePosition + Vector3.up * (step * 0.5f);
                        if (IsCapsuleSpaceSafe(stepPos, 0.5f + (step * 0.5f), true))
                        {
                            targetPosition = stepPos;
                            targetRotation = lastSafeRotation;
                            foundSafePoint = true;
                            break;
                        }
                    }
                }
            }

            if (!foundSafePoint)
            {
                if (IsCapsuleSpaceSafe(initialSpawnPosition, 0.5f, true))
                {
                    targetPosition = initialSpawnPosition;
                    targetRotation = initialSpawnRotation;
                    foundSafePoint = true;
                }
                else
                {
                    for (int step = 1; step <= 3; step++)
                    {
                        Vector3 stepPos = initialSpawnPosition + Vector3.up * (step * 0.5f);
                        if (IsCapsuleSpaceSafe(stepPos, 0.5f + (step * 0.5f), true))
                        {
                            targetPosition = stepPos;
                            targetRotation = initialSpawnRotation;
                            foundSafePoint = true;
                            break;
                        }
                    }
                }
            }

            if (!foundSafePoint)
            {
                targetPosition = initialSpawnPosition + Vector3.up * 0.5f;
                targetRotation = initialSpawnRotation;
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

        private bool IsInputBlocked()
        {
            if (Time.timeScale == 0f || PauseMenu.isPaused) return true;
            if (playerStats != null && playerStats.IsDead) return true;
            return false;
        }

        private void UpdateCachedKey()
        {
            prevHotkey = resetHotkey;
#if ENABLE_INPUT_SYSTEM
            cachedInputKey = ConvertKeyCodeToKey(resetHotkey);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private UnityEngine.InputSystem.Key ConvertKeyCodeToKey(KeyCode key)
        {
            if (System.Enum.TryParse<UnityEngine.InputSystem.Key>(key.ToString(), true, out var parsedKey) && parsedKey != UnityEngine.InputSystem.Key.None)
            {
                return parsedKey;
            }

            switch (key)
            {
                case KeyCode.Alpha0: return UnityEngine.InputSystem.Key.Digit0;
                case KeyCode.Alpha1: return UnityEngine.InputSystem.Key.Digit1;
                case KeyCode.Alpha2: return UnityEngine.InputSystem.Key.Digit2;
                case KeyCode.Alpha3: return UnityEngine.InputSystem.Key.Digit3;
                case KeyCode.Alpha4: return UnityEngine.InputSystem.Key.Digit4;
                case KeyCode.Alpha5: return UnityEngine.InputSystem.Key.Digit5;
                case KeyCode.Alpha6: return UnityEngine.InputSystem.Key.Digit6;
                case KeyCode.Alpha7: return UnityEngine.InputSystem.Key.Digit7;
                case KeyCode.Alpha8: return UnityEngine.InputSystem.Key.Digit8;
                case KeyCode.Alpha9: return UnityEngine.InputSystem.Key.Digit9;

                case KeyCode.Keypad0: return UnityEngine.InputSystem.Key.Numpad0;
                case KeyCode.Keypad1: return UnityEngine.InputSystem.Key.Numpad1;
                case KeyCode.Keypad2: return UnityEngine.InputSystem.Key.Numpad2;
                case KeyCode.Keypad3: return UnityEngine.InputSystem.Key.Numpad3;
                case KeyCode.Keypad4: return UnityEngine.InputSystem.Key.Numpad4;
                case KeyCode.Keypad5: return UnityEngine.InputSystem.Key.Numpad5;
                case KeyCode.Keypad6: return UnityEngine.InputSystem.Key.Numpad6;
                case KeyCode.Keypad7: return UnityEngine.InputSystem.Key.Numpad7;
                case KeyCode.Keypad8: return UnityEngine.InputSystem.Key.Numpad8;
                case KeyCode.Keypad9: return UnityEngine.InputSystem.Key.Numpad9;

                case KeyCode.Return: return UnityEngine.InputSystem.Key.Enter;
                case KeyCode.LeftControl: return UnityEngine.InputSystem.Key.LeftCtrl;
                case KeyCode.RightControl: return UnityEngine.InputSystem.Key.RightCtrl;
                case KeyCode.LeftShift: return UnityEngine.InputSystem.Key.LeftShift;
                case KeyCode.RightShift: return UnityEngine.InputSystem.Key.RightShift;
                case KeyCode.LeftAlt: return UnityEngine.InputSystem.Key.LeftAlt;
                case KeyCode.RightAlt: return UnityEngine.InputSystem.Key.RightAlt;
                case KeyCode.Space: return UnityEngine.InputSystem.Key.Space;
                case KeyCode.Tab: return UnityEngine.InputSystem.Key.Tab;
                case KeyCode.Escape: return UnityEngine.InputSystem.Key.Escape;
                case KeyCode.Backspace: return UnityEngine.InputSystem.Key.Backspace;

                case KeyCode.F1: return UnityEngine.InputSystem.Key.F1;
                case KeyCode.F2: return UnityEngine.InputSystem.Key.F2;
                case KeyCode.F3: return UnityEngine.InputSystem.Key.F3;
                case KeyCode.F4: return UnityEngine.InputSystem.Key.F4;
                case KeyCode.F5: return UnityEngine.InputSystem.Key.F5;
                case KeyCode.F6: return UnityEngine.InputSystem.Key.F6;
                case KeyCode.F7: return UnityEngine.InputSystem.Key.F7;
                case KeyCode.F8: return UnityEngine.InputSystem.Key.F8;
                case KeyCode.F9: return UnityEngine.InputSystem.Key.F9;
                case KeyCode.F10: return UnityEngine.InputSystem.Key.F10;
                case KeyCode.F11: return UnityEngine.InputSystem.Key.F11;
                case KeyCode.F12: return UnityEngine.InputSystem.Key.F12;

                default: return UnityEngine.InputSystem.Key.None;
            }
        }
#endif

        [ContextMenu("Trigger Reset Position")]
        private void ContextMenuTriggerResetPosition()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PlayerFallSafety] TriggerResetPosition via ContextMenu requires Play Mode.");
                return;
            }
            if (!enabled || !gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[PlayerFallSafety] Component is disabled or GameObject is inactive.");
                return;
            }
            TriggerResetPosition(ignoreCooldown: true, ignoreStateBlock: true);
        }

        /// <summary>
        /// Checks whether standing at pos has clear capsule space above and solid ground below.
        /// </summary>
        private bool IsCapsuleSpaceSafe(Vector3 pos, float extraGroundCheckDistance = 0f, bool checkGround = true)
        {
            if (playerMovement == null || playerMovement.playerSettings == null) return true;

            CapsuleCollider col = GetComponent<CapsuleCollider>();
            float radius = col != null ? col.radius : 0.4f;
            float checkRadius = radius * 0.85f;

            float height = playerMovement != null ? playerMovement.OriginalCapsuleHeight : 1.75f;
            LayerMask mask = playerMovement.playerSettings.whatIsGround;

            if (checkGround)
            {
                bool hasGroundBelow = Physics.Raycast(pos + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 0.5f + extraGroundCheckDistance, mask, QueryTriggerInteraction.Ignore);
                if (!hasGroundBelow) return false;

                Vector3 groundPoint = hit.point;
                Vector3 bottom = groundPoint + Vector3.up * (radius + 0.15f);
                Vector3 top = groundPoint + Vector3.up * Mathf.Max(radius + 0.2f, height - radius);

                return !Physics.CheckCapsule(bottom, top, checkRadius, mask, QueryTriggerInteraction.Ignore);
            }
            else
            {
                Vector3 bottom = pos + Vector3.up * (radius + 0.15f);
                Vector3 top = pos + Vector3.up * Mathf.Max(radius + 0.2f, height - radius);

                return !Physics.CheckCapsule(bottom, top, checkRadius, mask, QueryTriggerInteraction.Ignore);
            }
        }
    }
}

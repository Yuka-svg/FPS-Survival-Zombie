/// <summary>
/// This script belongs to cowsins� as a part of the cowsins� FPS Engine. All rights reserved. 
/// </summary>
using UnityEngine;
using UnityEngine.Events;
using System;

namespace cowsins
{
    [System.Serializable]
    public class PlayerStats : MonoBehaviour, IDamageable, IPlayerStatsProvider, IFallHeightProvider, IPlayerStatsEventsProvider
    {
        [System.Serializable]
        public class UserStatsEvents
        {
            public UnityEvent OnDeath, OnDamage, OnHeal;
        }

        #region variables

        [ReadOnly] public float health, shield;

        public float maxHealth, maxShield;

        [Tooltip("If enabled, makes the rigidbody kinematic on death. Player loses control either way."), SerializeField] private bool freezePlayerOnDeath;

        [Tooltip("Turn on to apply damage on falling from great height"), SerializeField] private bool takesFallDamage;

        [Tooltip("Minimum height ( in units ) the player has to fall from in order to take damage"), SerializeField, Min(1)] private float minimumHeightDifferenceToApplyDamage;

        [Tooltip("How the damage will increase on landing if the damage on fall is going to be applied"), SerializeField] private float fallDamageMultiplier;

        [SerializeField] private bool enableAutoHeal = false;

        [SerializeField, Min(0)] private float healRate;

        [SerializeField] private float healAmount;

        [SerializeField] private bool restartAutoHealAfterBeingDamaged = false;

        [SerializeField] private float restartAutoHealTime;

        [Header("Shield Regeneration")]
        [Tooltip("Enable automatic shield regeneration after a delay without taking damage")]
        [SerializeField] private bool enableShieldRegen = true;

        [Tooltip("Delay in seconds after last damage taken before shield starts regenerating")]
        [SerializeField, Min(0)] private float shieldRegenDelay = 5f;

        [Tooltip("Time in seconds between each shield regeneration tick")]
        [SerializeField, Min(0.01f)] private float shieldRegenRate = 2f;

        [Tooltip("Amount of shield restored per regeneration tick")]
        [SerializeField, Min(0)] private float shieldRegenAmount = 2.5f;


        // Internal use

        private float? currentFallHeight = null;
        private float lastDamageTime = -999f;

        private bool isDead;

        private PlayerDependencies playerDependencies;
        private IPlayerMovementStateProvider player; // IPlayerMovementStateProvider is implemented in PlayerMovement.cs
        private IPlayerMovementEventsProvider playerEvents; // IPlayerMovementEventsProvider is implemented in PlayerMovement.cs
        private IPlayerMultipliers multipliers; // IPlayerMultipliers is implemented in PlayerMultipliers.cs

        public UserStatsEvents userEvents;

        // Accessors
        public bool IsDead => isDead;
        public float Health => health;
        public float MaxHealth => maxHealth;
        public float Shield => shield;
        public float MaxShield => maxShield;
        public float? CurrentFallHeight => currentFallHeight;
        public bool FreezePlayerOnDeath => freezePlayerOnDeath; 

        public bool TakesFallDamage => takesFallDamage;
        public bool EnableAutoHeal => enableAutoHeal;
        public bool RestartAutoHealAfterBeingDamaged => restartAutoHealAfterBeingDamaged;
        public bool EnableShieldRegen => enableShieldRegen;

        public PlayerStatsEvents Events { get; } = new PlayerStatsEvents();

        public event Action OnDie;
        public void AddOnDieListener(Action listener) => OnDie += listener;
        public void RemoveOnDieListener(Action listener) => OnDie -= listener;

        #endregion

        private void Start()
        {
            GetAllReferences();

            // Apply basic settings 
            maxHealth += PlayerUpgradeManager.Instance.bonusHealth;
            maxShield += PlayerUpgradeManager.Instance.bonusShield;

            health = maxHealth;
            shield = maxShield;
            Events.OnInitializeHealth?.Invoke(health, shield, maxHealth, maxShield);

            if (enableAutoHeal)
                StartAutoHeal();

            if (enableShieldRegen)
                InvokeRepeating(nameof(ManageShieldRegen), shieldRegenRate, shieldRegenRate);
        }

        private void Update()
        {
            // Manage fall damage
            if (!takesFallDamage || player.IsClimbing || IsDead) return;
            ManageFallDamage();
        }

        /// <summary>
        /// Regenerates shield over time after a delay without taking damage.
        /// </summary>
        private void ManageShieldRegen()
        {
            if (!enableShieldRegen || maxShield <= 0 || IsDead) return;
            if (shield >= maxShield) return;
            if (Time.time - lastDamageTime < shieldRegenDelay) return;

            shield = Mathf.Min(shield + shieldRegenAmount, maxShield);
            Events.OnHealthChanged?.Invoke(health, shield, false);
        }
        /// <summary>
        /// Our Player Stats is IDamageable, which means it can be damaged
        /// If so, call this method to damage the player
        /// </summary>
        public void Damage(float _damage, bool isHeadshot)
        {
            if (player == null)
            {
                player = playerDependencies.PlayerMovementState;
            }
            // Early return if player is dashing with damage protection
            if (player != null && player.IsDashing && player.DamageProtectionWhileDashing)
                return;

            // Ensure damage is a positive value
            float damage = Mathf.Abs(_damage);

            // Track last damage time for shield regen delay
            lastDamageTime = Time.time;

            // Trigger damage event
            userEvents.OnDamage.Invoke();

            // Apply damage to shield first
            if (damage <= shield)
            {
                shield -= damage;
            }
            else
            {
                // Apply remaining damage to health
                damage -= shield;
                shield = 0;
                health -= damage;
            }

            // Notify UI about the health change
            Events.OnHealthChanged?.Invoke(health, shield, true);

            if (health <= 0 && !IsDead) Die(); // Die in case we run out of health   

            // Handle auto-healing
            if (enableAutoHeal && restartAutoHealAfterBeingDamaged)
            {
                CancelInvoke(nameof(AutoHeal));
                InvokeRepeating(nameof(AutoHeal), restartAutoHealTime, healRate);
            }

        }


        public void Heal(float healAmount)
        {
            float adjustedHealAmount = Mathf.Abs(healAmount * multipliers.HealMultiplier);

            // If we are at full health and shield, do not heal
            if ((maxShield > 0 && shield == maxShield) || (maxShield == 0 && health == maxHealth))
            {
                return;
            }

            // Trigger heal event
            userEvents.OnHeal.Invoke();

            // Calculate effective healing for health
            float effectiveHealForHealth = Mathf.Min(adjustedHealAmount, maxHealth - health);
            health += effectiveHealForHealth;

            // Calculate remaining heal amount after health is full
            float remainingHeal = adjustedHealAmount - effectiveHealForHealth;

            // Apply remaining heal to shield if applicable
            if (remainingHeal > 0 && maxShield > 0)
            {
                shield = Mathf.Min(shield + remainingHeal, maxShield);
            }

            // Notify UI about the health change
            Events.OnHealthChanged?.Invoke(health, shield, false);
        }

        /// <summary>
        /// Heals only health, without affecting shield.
        /// </summary>
        public void HealHealthOnly(float healAmount)
        {
            if (health >= maxHealth) return;

            float adjustedHealAmount = Mathf.Abs(healAmount * multipliers.HealMultiplier);
            health = Mathf.Min(health + adjustedHealAmount, maxHealth);

            userEvents.OnHeal.Invoke();
            Events.OnHealthChanged?.Invoke(health, shield, false);
        }

        public void HealFull()
        {
            if (IsFullyHealed()) return;

            health = maxHealth;
            shield = maxShield;
            Events.OnHealthChanged?.Invoke(health, shield, false);
        }

        public bool IsFullyHealed()
        {
            if (MaxShield > 0)
            {
                return shield >= MaxShield && health >= MaxHealth;
            }
            else
            {
                return health >= MaxHealth;
            }
        }


        /// <summary>
        /// Perform any actions On death
        /// </summary>
        private void Die()
        {
            isDead = true;
            OnDie?.Invoke();
            userEvents.OnDeath.Invoke(); // Invoke a custom event
        }
        /// <summary>
        /// Basically find everything the script needs to work
        /// </summary>
        private void GetAllReferences()
        {
            playerDependencies = GetComponent<PlayerDependencies>();
            player = playerDependencies.PlayerMovementState;
            playerEvents = playerDependencies.PlayerMovementEvents;
            multipliers = playerDependencies.PlayerMultipliers;
        }
        /// <summary>
        /// While airborne, if you exceed a certain time, damage on fall will be applied
        /// </summary>
        private void ManageFallDamage()
        {
            // Grab current player height
            if (!player.Grounded && transform.position.y > currentFallHeight || !player.Grounded && currentFallHeight == null) currentFallHeight = transform.position.y;

            // Check if we landed, as well if our current height is lower than the original height. If so, check if we should apply damage
            if (player.Grounded && currentFallHeight != null && transform.position.y < currentFallHeight)
            {
                float currentHeight = transform.position.y;

                // Transform nullable variable into a non nullable float for later operations
                float noNullHeight = currentFallHeight ?? default(float);

                float heightDifference = noNullHeight - currentHeight;

                // If the height difference is enough, apply damage
                if (heightDifference > minimumHeightDifferenceToApplyDamage) Damage(heightDifference * fallDamageMultiplier, false);

                // Reset height
                currentFallHeight = null;
            }
        }

        public void SetFallHeight(float newFallHeight) => currentFallHeight = newFallHeight;

        private void StartAutoHeal()
        {
            InvokeRepeating(nameof(AutoHeal), healRate, healRate);
        }

        private void AutoHeal()
        {
            if (shield >= maxShield && health >= maxHealth) return;

            Heal(healAmount);
        }

        public void Respawn(Vector3 respawnPosition)
        {
            isDead = false;
            HealFull();
            playerEvents.Events.OnRespawn?.Invoke(respawnPosition, player.Orientation.Rotation, true, true);
        }

        // This method is used to set the Player Health values when the game loads, only if the Save & Load Add-On is available.
#if SAVE_LOAD_ADD_ON
        public void OverrideHealth(float health, float maxHealth, float shield, float maxShield)
        {
            this.health = health;
            this.maxHealth = maxHealth;
            this.shield = shield;
            this.maxShield = maxShield;
        }
#endif
    }
}
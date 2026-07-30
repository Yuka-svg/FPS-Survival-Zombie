using UnityEngine;
using System;

namespace cowsins
{
    public class PlayerUpgradeManager : MonoBehaviour
    {
        public static PlayerUpgradeManager Instance;
        public event Action OnUpgradesChanged;

        [Header("Permanent Upgrades")]
        public int bonusHealth;
        public int bonusShield;
        public int bonusMagazine;
        public float bonusStamina;
        public float bonusDamage;

        // Cached player-side references — AddHealth/AddStamina/AddDamage are
        // called once per skill node purchase, but FindAnyObjectByType scans
        // the entire hierarchy each time. Cache them on first use and re-resolve
        // only if the cached reference went null (e.g. player respawned).
        private PlayerStats _cachedStats;
        private PlayerMovement _cachedMovement;
        private PlayerMultipliers _cachedMultipliers;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad only persists root GameObjects; detach if nested.
                if (transform.parent != null) transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void AddHealth(int amount)
        {
            bonusHealth += amount;
            // Apply to the live PlayerStats so maxHealth/health update immediately
            // (PlayerStats.Start only reads bonusHealth once at spawn).
            var stats = _cachedStats;
            if (stats == null) { stats = FindAnyObjectByType<PlayerStats>(); _cachedStats = stats; }
            if (stats != null)
            {
                stats.maxHealth += amount;
                stats.health = Mathf.Min(stats.health + amount, stats.maxHealth);
                stats.Events.OnHealthChanged?.Invoke(stats.health, stats.shield, false);
            }
            OnUpgradesChanged?.Invoke();
        }

        public void AddShield(int amount)
        {
            bonusShield += amount;
            OnUpgradesChanged?.Invoke();
        }

        public void AddMagazine(int amount)
        {
            bonusMagazine += amount;
        }

        /// <summary>
        /// Increases the player's max stamina by <paramref name="amount"/> and
        /// applies it to the live PlayerMovement so the stamina bar updates immediately.
        /// </summary>
        public void AddStamina(float amount)
        {
            bonusStamina += amount;
            var movement = _cachedMovement;
            if (movement == null) { movement = FindAnyObjectByType<PlayerMovement>(); _cachedMovement = movement; }
            if (movement != null)
            {
                movement.playerSettings.maxStamina += amount;
            }
            OnUpgradesChanged?.Invoke();
        }

        public void AddDamage(float amount)
        {
            bonusDamage += amount;
            var multipliers = _cachedMultipliers;
            if (multipliers == null) { multipliers = FindAnyObjectByType<PlayerMultipliers>(); _cachedMultipliers = multipliers; }
            if (multipliers != null)
            {
                multipliers.DamageMultiplier += amount;
            }
            OnUpgradesChanged?.Invoke();
        }

    }
}
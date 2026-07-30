using UnityEngine;
using UnityEngine.InputSystem;
using cowsins;

namespace CowsinsTesting
{
    /// <summary>
    /// Temporary test helper to deal damage or heal the player using keyboard inputs.
    /// This is an isolated testing script and can be safely deleted.
    /// Default keys:
    /// - [H]: Deal 10 damage to player
    /// - [G]: Heal player by 10 points
    /// </summary>
    public class PlayerDamageTestHelper : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Amount of damage to apply when key is pressed")]
        public float damageAmount = 10f;

        [Tooltip("Key to press to apply damage")]
        public Key damageKey = Key.H;

        [Tooltip("Key to press to heal player")]
        public Key healKey = Key.G;

        [Tooltip("Amount of healing to apply when heal key is pressed")]
        public float healAmount = 10f;

        private PlayerStats _stats;

        private void Update()
        {
            if (_stats == null)
            {
                _stats = Object.FindAnyObjectByType<PlayerStats>();
                if (_stats == null) return;
            }

            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[damageKey].wasPressedThisFrame)
            {
                Debug.Log($"[PlayerDamageTestHelper] Pressing {damageKey}: Dealing {damageAmount} damage to player.");
                _stats.Damage(damageAmount, false);
            }

            if (kb[healKey].wasPressedThisFrame)
            {
                Debug.Log($"[PlayerDamageTestHelper] Pressing {healKey}: Healing player by {healAmount}.");
                _stats.Heal(healAmount);
            }
        }
    }
}

using UnityEngine;

namespace cowsins
{
    public class AimSkillSystem : MonoBehaviour
    {
        public static AimSkillSystem Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        [Header("Runtime Stats")]

        [SerializeField] private float critChance;
        [SerializeField] private float critMultiplier = 1f;

        [SerializeField] private float recoilMultiplier = 1f;

        [SerializeField] private bool oneShotCrook;
        [SerializeField] private bool bonusDamageVsSpecial;

        public float CritChance => critChance;
        public float CritMultiplier => critMultiplier;
        public float RecoilMultiplier => recoilMultiplier;

        public bool OneShotCrook => oneShotCrook;
        public bool BonusDamageVsSpecial => bonusDamageVsSpecial;

        public void RefreshStats(int aimLevel)
        {
            // Reset toàn bộ stat
            critChance = 0f;
            critMultiplier = 1f;

            recoilMultiplier = 1f;

            oneShotCrook = false;
            bonusDamageVsSpecial = false;

            // Node 1
            if (aimLevel >= 1)
            {
                recoilMultiplier = 0.9f;
            }

            // Node 2
            if (aimLevel >= 2)
            {
                critChance = 0.10f;
            }

            // Node 3
            if (aimLevel >= 3)
            {
                critChance = 0.20f;
            }

            // Node 4
            if (aimLevel >= 4)
            {
                critMultiplier = 1.5f;
            }

            // Node 5
            if (aimLevel >= 5)
            {
                oneShotCrook = true;
                bonusDamageVsSpecial = true;
            }

#if UNITY_EDITOR
            Debug.Log(
                $"AIM LVL {aimLevel} | Crit:{critChance:P0} | CritDmg:{critMultiplier}x | Recoil:{recoilMultiplier}"
            );
#endif
        }

        public bool RollCritical()
        {
            return Random.value <= critChance;
        }

        public float ApplyCriticalDamage(float damage)
        {
            return damage * critMultiplier;
        }
    }
}
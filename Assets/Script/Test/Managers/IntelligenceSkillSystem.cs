using UnityEngine;

namespace cowsins
{
    public class IntelligenceSkillSystem : MonoBehaviour
    {
        [Header("Runtime Stats")]
        [SerializeField] private float xpPickupRadius;
        [SerializeField] private float xpMultiplier = 1f;
        [SerializeField] private bool highlightCollectibles;

        public float XPPickupRadius => xpPickupRadius;
        public float XPMultiplier => xpMultiplier;
        public bool HighlightCollectibles => highlightCollectibles;

        public void RefreshStats(int intelligenceLevel)
        {
            xpPickupRadius = 0f;
            xpMultiplier = 1f;
            highlightCollectibles = false;

            if (intelligenceLevel >= 1)
            {
                xpPickupRadius = 5f;
            }

            if (intelligenceLevel >= 2)
            {
                xpMultiplier *= 1.10f;
            }

            if (intelligenceLevel >= 3)
            {
                xpPickupRadius = 10f;
            }

            if (intelligenceLevel >= 4)
            {
                xpMultiplier *= 1.15f;
            }

            if (intelligenceLevel >= 5)
            {
                xpPickupRadius = 15f;
                highlightCollectibles = true;
            }

#if UNITY_EDITOR
            Debug.Log(
                $"INT {intelligenceLevel} | Radius: {xpPickupRadius} | XP: x{xpMultiplier:F2}"
            );
#endif
        }
    }
}
using UnityEngine;

namespace cowsins
{
    public class ExperienceManager : MonoBehaviour
    {
        // Singleton pattern to ensure that there is only one ExperienceManager instance in the game.
        public static ExperienceManager Instance;
        public bool useExperience;
        public int playerLevel;
        public int[] experienceRequirements;

        private float totalExperience;

        public float TotalExperience => totalExperience;
        [SerializeField] private int skillPoints;

        public int SkillPoints => skillPoints;

        private void OnEnable()
        {
            // If there is no existing ExperienceManager instance, then set this instance as the singleton.
            if (Instance == null) Instance = this;
        }

        // add experience to the player.
        public void AddExperience(float amount)
        {
            // Increase the player's total experience.
            totalExperience += amount;

            // Check if the player has leveled up.
            CheckForLevelUp();
        }

        // remove experience from the player.
        public void RemoveExperience(float amount)
        {
            // Reduce the player's total experience.
            totalExperience = Mathf.Max(totalExperience - amount, 0);

            // Check if the player has leveled down.
            CheckForLevelDown();
        }

        public void ResetExperience() => totalExperience = 0;

        // Requirement (cumulative XP threshold) for a given (0-based) level. Beyond the
        // defined array the threshold keeps growing by the same step as the last two
        // defined entries, so leveling never stops but also never loops infinitely.
        public float GetRequirement(int level)
        {
            if (experienceRequirements == null || experienceRequirements.Length == 0)
                return float.MaxValue;
            if (level < 0)
                return float.MaxValue;
            int n = experienceRequirements.Length;
            if (level < n)
                return experienceRequirements[level];
            float last = experienceRequirements[n - 1];
            float step = n >= 2 ? last - experienceRequirements[n - 2] : last;
            if (step <= 0) step = last; // fallback: grow by last value itself
            return last + step * (level - (n - 1));
        }

        // check if the player has leveled up.
        private void CheckForLevelUp()
        {
            // No hard cap: once past the defined array the last requirement repeats,
            // so the player can keep leveling forever.
            while (totalExperience >= GetRequirement(playerLevel))
            {
                playerLevel++;
                skillPoints++;
            }
        }

        // check if the player has leveled down.
        private void CheckForLevelDown()
        {
            // While the player's level is greater than the minimum level and their total experience is less than the experience required for the current level, decrease the player's level.
            while (playerLevel > 0 && totalExperience < GetRequirement(playerLevel))
            {
                playerLevel--;
            }
        }

        // get the player's level.
        public int GetPlayerLevel()
        {
            // Return the player's level plus one, since the level array starts at zero.
            return playerLevel + 1;
        }

        // get the player's current experience.
        public float GetCurrentExperience()
        {
            // Calculate the player's current experience by subtracting the experience required for the previous level from their total experience.
            float previousLevelExperience = playerLevel > 0 ? GetRequirement(playerLevel - 1) : 0;
            return totalExperience - previousLevelExperience;
        }
        public bool SpendSkillPoints(int amount)
        {
            if (skillPoints < amount)
                return false;

            skillPoints -= amount;
            return true;
        }
    }
}
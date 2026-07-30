using UnityEngine;

/// <summary>
/// ScriptableObject definition for a single achievement.  Created via
/// Create > Game > Achievement.  The AchievementManager holds an array of
/// these and tracks unlock state at runtime; persistence uses the unique
/// <see cref="id"/> as a PlayerPrefs key.
/// </summary>
[CreateAssetMenu(fileName = "NewAchievement", menuName = "Game/Achievement", order = 0)]
public class AchievementData : ScriptableObject
{
    [Tooltip("Stable unique identifier used for PlayerPrefs save. Must be unique across all achievements.")]
    public string id;

    [Tooltip("Display title shown in the popup and achievement list.")]
    [TextArea(1, 2)]
    public string title;

    [Tooltip("Description of how to unlock the achievement.")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Optional icon shown in the popup and list. Leave null to use a default badge.")]
    public Sprite icon;

    [Tooltip("If true, the achievement tracks a cumulative total (e.g. kill 130 Crooks). "
           + "The manager stores the current progress and unlocks when the target is reached.")]
    public bool isProgression;

    [Tooltip("Target value for progression achievements. Ignored when isProgression is false.")]
    public int targetValue;

    /// <summary>PlayerPrefs key for the unlocked flag.</summary>
    public string UnlockedKey => "Achievement_Unlocked_" + id;

    /// <summary>PlayerPrefs key for the current progress (progression achievements only).</summary>
    public string ProgressKey => "Achievement_Progress_" + id;
}

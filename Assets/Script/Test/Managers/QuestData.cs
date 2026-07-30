using UnityEngine;

/// <summary>
/// A single story quest (nhiệm vụ) from the Level Design document. Quests are
/// grouped into 5 chapters; completing every quest in a chapter advances the
/// story to the next chapter. Quests are authored as ScriptableObjects so
/// designers can tweak titles/descriptions/rewards without touching code.
/// </summary>
[CreateAssetMenu(menuName = "Story/Quest", order = 0)]
public class QuestData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Quest number from the Level Design doc (1-12).")]
    public int questId;

    [Tooltip("Chapter this quest belongs to (1-5).")]
    public int chapter = 1;

    public string title;

    [TextArea(3, 8)]
    public string description;

    [TextArea(2, 5)]
    [Tooltip("Short objective text shown in the quest tracker (e.g. 'Tiêu diệt zombie').")]
    public string objective;

    [Header("Rewards")]
    [Tooltip("EXP granted when the quest is completed. 0 = no reward.")]
    public float expReward = 0f;

    [Tooltip("Journal granted when the quest is completed. Leave null for none.")]
    public JournalData journalReward;

    [Tooltip("Optional notification shown when the quest is completed.")]
    [TextArea(1, 3)]
    public string completionNotification;
}

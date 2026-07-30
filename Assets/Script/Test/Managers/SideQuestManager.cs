using System.Collections.Generic;
using UnityEngine;
using cowsins;

/// <summary>
/// Optional side-quest progression driver. Side quests are NOT part of the main
/// story flow — they run in parallel with StoryManager and never block chapter
/// advancement. They are gated by chapter: a side quest only becomes available
/// once the player has reached its chapter (via ChapterBoundary) AND the main
/// story for that chapter has been completed (so side quests act as post-story
/// exploration content per chapter).
///
/// Design goals:
/// - Reuse the existing QuestData assets (so QuestTrigger/KillCountObjective/
///   CollectibleQuestObjective work unchanged — they all call
///   StoryManager.CompleteActiveQuest, which we DON'T want for side quests).
/// - Stay decoupled from StoryManager's linear flow: completing a side quest
///   must not advance the main story.
/// - Show side quests in the QuestTrackerWidget alongside (or instead of) the main
///   quest when the main story for that chapter is done.
///
/// Implementation:
/// - Each side quest has its own QuestData (with questId >= 100 to distinguish
///   from main quests 1-13).
/// - SideQuestManager tracks which side quests are active/completed per chapter.
/// - Side-quest QuestTrigger components call SideQuestManager.CompleteSideQuest
///   instead of StoryManager.CompleteActiveQuest. We do this by giving side-quest
///   triggers a SideQuestTrigger wrapper that routes completion here.
/// - Rewards (EXP + journal) are granted directly, mirroring StoryManager.GrantRewards.
/// </summary>
public class SideQuestManager : MonoBehaviour
{
    public static SideQuestManager Instance;

    [Header("Side Quests per Chapter")]
    [Tooltip("Side quests available in Chapter 1. Unlocked when Ch1 main story is done.")]
    public QuestData[] chapter1SideQuests;
    [Tooltip("Side quests available in Chapter 2. Unlocked when Ch2 main story is done.")]
    public QuestData[] chapter2SideQuests;
    [Tooltip("Side quests available in Chapter 3. Unlocked when Ch3 main story is done.")]
    public QuestData[] chapter3SideQuests;
    [Tooltip("Side quests available in Chapter 4. Unlocked when Ch4 main story is done.")]
    public QuestData[] chapter4SideQuests;
    [Tooltip("Side quests available in Chapter 5. Unlocked when Ch5 main story is done.")]
    public QuestData[] chapter5SideQuests;

    [Header("Activation")]
    [Tooltip("If true, side quests for a chapter become available as soon as the player ENTERS that chapter (parallel with main quests). If false, they only unlock after the chapter's main story is complete (post-story exploration).")]
    public bool unlockOnChapterEnter = false;

    private readonly HashSet<QuestData> _completed = new HashSet<QuestData>();
    private readonly List<QuestData> _active = new List<QuestData>();

    /// <summary>Side quests currently active (the player can work on multiple in parallel).</summary>
    public IReadOnlyList<QuestData> ActiveQuests => _active;

    /// <summary>Number of side quests completed across all chapters.</summary>
    public int TotalCompleted => _completed.Count;

    /// <summary>Fired when a side quest is completed. (completedQuest)</summary>
    public event System.Action<QuestData> OnSideQuestCompleted;

    /// <summary>Fired when a side quest is activated. (activatedQuest)</summary>
    public event System.Action<QuestData> OnSideQuestActivated;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Subscribe to StoryManager to know when chapters change / main story completes.
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnChapterChanged += HandleChapterChanged;
            // Initial unlock for the starting chapter.
            TryUnlockForCurrentChapter();
        }
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
        {
            StoryManager.Instance.OnChapterChanged -= HandleChapterChanged;
        }
    }

    private void HandleChapterChanged(int oldCh, int newCh)
    {
        // When a new chapter is entered, unlock its side quests (if unlockOnChapterEnter)
        // or wait until the chapter's main story is done (default).
        // Also unlock any PREVIOUS chapters whose side quests weren't unlocked yet
        // (e.g. Ch1 side quests unlock when advancing to Ch2, since Ch1 is now done).
        if (newCh <= 0)
        {
            // Story complete — unlock ALL remaining side quests.
            UnlockAll();
            return;
        }
        TryUnlockForCurrentChapter();
        // Unlock side quests for all chapters that are now complete.
        for (int ch = 1; ch < newCh; ch++)
        {
            if (IsChapterMainStoryDone(ch)) UnlockChapter(ch);
        }
    }

    private void TryUnlockForCurrentChapter()
    {
        var sm = StoryManager.Instance;
        if (sm == null) return;

        // If the main story is fully complete, unlock ALL remaining side quests.
        if (sm.StoryComplete)
        {
            UnlockAll();
            return;
        }

        int ch = sm.CurrentChapter;
        bool mainDone = IsChapterMainStoryDone(ch);

        if (unlockOnChapterEnter || mainDone)
        {
            UnlockChapter(ch);
        }
    }

    /// <summary>True if every main quest in the given chapter is completed.</summary>
    private bool IsChapterMainStoryDone(int chapter)
    {
        var sm = StoryManager.Instance;
        if (sm == null) return false;
        // If we've advanced past this chapter, it's definitely done.
        if (sm.CurrentChapter > chapter) return true;
        if (sm.CurrentChapter != chapter) return false;
        // We're in this chapter — it's done when there are no more quests
        // and at least one was completed.
        return sm.GetCurrentQuest() == null && sm.QuestsCompletedThisChapter > 0;
    }

    private void UnlockChapter(int chapter)
    {
        var quests = GetChapterSideQuests(chapter);
        if (quests == null) return;
        foreach (var q in quests)
        {
            if (q == null) continue;
            if (_completed.Contains(q) || _active.Contains(q)) continue;
            _active.Add(q);
            OnSideQuestActivated?.Invoke(q);
            Debug.Log($"[SideQuestManager] Activated side quest: {q.title} (Ch{chapter})");
        }
    }

    /// <summary>
    /// Public wrapper around UnlockChapter for external callers (e.g. the
    /// CompanionManager skip path needs to unlock Ch4 side quests before
    /// auto-completing them).
    /// </summary>
    public void UnlockChapterPublic(int chapter) => UnlockChapter(chapter);

    /// <summary>Unlocks every side quest in every chapter (used when story is complete).</summary>
    private void UnlockAll()
    {
        for (int ch = 1; ch <= 5; ch++)
            UnlockChapter(ch);
    }

    /// <summary>The side-quest list for the given chapter.</summary>
    public QuestData[] GetChapterSideQuests(int chapter)
    {
        switch (chapter)
        {
            case 1: return chapter1SideQuests;
            case 2: return chapter2SideQuests;
            case 3: return chapter3SideQuests;
            case 4: return chapter4SideQuests;
            case 5: return chapter5SideQuests;
            default: return null;
        }
    }

    /// <summary>True if the given side quest is currently active.</summary>
    public bool IsActive(QuestData quest) => quest != null && _active.Contains(quest);

    /// <summary>True if the given side quest has been completed.</summary>
    public bool IsCompleted(QuestData quest) => quest != null && _completed.Contains(quest);

    /// <summary>
    /// Complete a side quest. Called by SideQuestTrigger (or directly) when the
    /// player satisfies the side quest's objective. Grants rewards and removes
    /// the quest from the active list. Does NOT advance the main story.
    /// </summary>
    public void CompleteSideQuest(QuestData quest)
    {
        if (quest == null) return;
        if (!_active.Contains(quest))
        {
            Debug.LogWarning($"[SideQuestManager] {quest.title} is not active — cannot complete.");
            return;
        }
        _active.Remove(quest);
        _completed.Add(quest);
        GrantRewards(quest);
        OnSideQuestCompleted?.Invoke(quest);
        Debug.Log($"[SideQuestManager] Side quest completed: {quest.title}");
    }

    private void GrantRewards(QuestData quest)
    {
        if (quest.expReward > 0f)
        {
            var em = FindAnyObjectByType<ExperienceManager>();
            if (em != null) em.AddExperience(quest.expReward);
            else Debug.LogWarning("[SideQuestManager] No ExperienceManager; EXP reward skipped.");
        }
        if (quest.journalReward != null)
        {
            var cm = CollectibleManager.Instance;
            if (cm != null) cm.Collect(quest.journalReward);
            else Debug.LogWarning("[SideQuestManager] No CollectibleManager; journal reward skipped.");
        }
    }
}

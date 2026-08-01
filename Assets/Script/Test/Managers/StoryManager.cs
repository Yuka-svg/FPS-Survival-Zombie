using System.Collections.Generic;
using UnityEngine;
using cowsins;

/// <summary>
/// Central story progression driver. Tracks the current chapter (1-5) and the
/// active quest, advances through quests/chapters, grants rewards (EXP, journals),
/// and broadcasts chapter/quest changes so UI widgets and spawners can react.
///
/// Quests are authored as QuestData ScriptableObjects and assigned in order per
/// chapter. QuestTrigger components in the scene call CompleteActiveQuest() when
/// the player satisfies an objective (reaching a trigger, killing targets,
/// interacting with an object, etc.).
///
/// The manager is intentionally tolerant of missing references so a partially
/// built scene still runs: null EXP/journal/UI references are skipped with a log.
/// </summary>
public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance;

    [Header("Chapter Quest Lists")]
    [Tooltip("Quests for Chapter 1, in completion order.")]
    public QuestData[] chapter1Quests;
    [Tooltip("Quests for Chapter 2, in completion order.")]
    public QuestData[] chapter2Quests;
    [Tooltip("Quests for Chapter 3, in completion order.")]
    public QuestData[] chapter3Quests;
    [Tooltip("Quests for Chapter 4, in completion order.")]
    public QuestData[] chapter4Quests;
    [Tooltip("Quests for Chapter 5, in completion order.")]
    public QuestData[] chapter5Quests;

    [Header("Starting State")]
    [Tooltip("Chapter the player starts in. Usually 1.")]
    public int startingChapter = 1;

    [Header("Chapter Transition Cutscene")]
    [Tooltip("If true, play a full-screen cutscene when advancing to a new chapter.")]
    public bool playChapterTransitionCutscene = true;

    [Tooltip("Title shown for each chapter transition. Index 0 = Ch1->Ch2, etc. " +
             "If the array is too short or an entry is empty, falls back to \"CHƯƠNG N\".")]
    [TextArea(1, 2)]
    public string[] chapterTransitionTitles = new string[]
    {
        "CHƯƠNG 2 — BỆNH VIỆN",
        "CHƯƠNG 3 — CÔNG TRƯỜNG",
        "CHƯƠNG 4 — KHU DÂN CƯ",
        "CHƯƠNG 5 — CHUNG CƯ"
    };

    [Tooltip("Body/subtitle shown for each chapter transition. Index 0 = Ch1->Ch2, etc.")]
    [TextArea(2, 4)]
    public string[] chapterTransitionBodies = new string[]
    {
        "Sau khi rời lều trại, bạn đến một bệnh viện bỏ hoang. Tiếng rên rỉ vọng ra từ bên trong...",
        "Bên kia bệnh viện là một công trường. Tiếng máy gầm vang vọng giữa đống đổ nát.",
        "Qua công trường, khu dân cư im ắng. Nhưng không có nghĩa là an toàn...",
        "Đỉnh chung cư — nơi cuối cùng. Phải tìm thuốc giải trước khi mọi thứ kết thúc."
    };

    [Tooltip("Cutscene timing for chapter transitions.")]
    public float chapterCutsceneFadeIn = 0.8f;
    public float chapterCutsceneHold = 4f;
    public float chapterCutsceneFadeOut = 1.2f;

    [Tooltip("Optional CutscenePlayer used for chapter transitions. If null, a temporary one is created at runtime.")]
    public CutscenePlayer chapterCutscenePlayer;

    // ---- Runtime state ----
    public int CurrentChapter { get; private set; }
    public int CurrentQuestIndex { get; private set; }
    public QuestData ActiveQuest { get; private set; }

    /// <summary>True when every quest in every chapter is done (story finished).</summary>
    public bool StoryComplete { get; private set; }

    /// <summary>Quests completed in the current chapter so far.</summary>
    public int QuestsCompletedThisChapter { get; private set; }

    /// <summary>
    /// True after advancing to a new chapter but before the player has entered
    /// that chapter's boundary. While true, the first quest of the new chapter
    /// is NOT yet active — it activates when the player enters the chapter area
    /// (ChapterBoundary calls ActivatePendingChapterQuest).
    /// </summary>
    public bool PendingChapterEntry { get; private set; }

    private readonly List<QuestData> _completedQuests = new List<QuestData>();

    // ---- Events ----
    /// <summary>Fired when the active quest changes. (oldQuest, newQuest)</summary>
    public event System.Action<QuestData, QuestData> OnActiveQuestChanged;

    /// <summary>Fired when a quest is completed. (completedQuest)</summary>
    public event System.Action<QuestData> OnQuestCompleted;

    /// <summary>Fired when the chapter changes. (oldChapter, newChapter)</summary>
    public event System.Action<int, int> OnChapterChanged;

    private void Awake()
    {
        Instance = this;
        CurrentChapter = startingChapter;
        CurrentQuestIndex = 0;
        QuestsCompletedThisChapter = 0;
        StoryComplete = false;
        PendingChapterEntry = false; // Player starts inside Ch1 — no pending entry.

        // If starting at a chapter > 1, auto-complete all quests from previous
        // chapters so the story state is consistent (triggers won't re-fire,
        // rewards are granted, completed-quest checks pass). This is the
        // "chapter select / debug skip" path.
        if (startingChapter > 1)
        {
            SkipPreviousChapters(startingChapter);
        }
    }

    private void Start()
    {
        // Set the first quest of the starting chapter as active.
        SetActiveQuest(GetCurrentQuest());

        // If we skipped to a chapter > 1, teleport the player to that
        // chapter's save room and set it as the respawn checkpoint. We use
        // a coroutine that waits one frame so that PlayerMovement.Start()
        // (which may reset the rigidbody) has already run.
        if (startingChapter > 1)
        {
            StartCoroutine(TeleportPlayerNextFrame(startingChapter));
        }
    }

    private System.Collections.IEnumerator TeleportPlayerNextFrame(int targetChapter)
    {
        yield return null; // Wait one frame for all Start() calls to finish.
        TeleportPlayerToChapterSaveRoom(targetChapter);
    }

    /// <summary>
    /// Auto-completes all quests in chapters 1..(targetChapter-1) without
    /// firing events or playing cutscenes. Marks them as completed in
    /// <see cref="_completedQuests"/> so <see cref="IsQuestCompleted"/> returns
    /// true. Does NOT grant rewards (skip = no free EXP/journals). Sets
    /// <see cref="QuestsCompletedThisChapter"/> to 0 for the target chapter.
    ///
    /// Called from Awake when startingChapter > 1.
    /// </summary>
    private void SkipPreviousChapters(int targetChapter)
    {
        for (int ch = 1; ch < targetChapter; ch++)
        {
            var quests = GetChapterQuests(ch);
            if (quests == null) continue;
            foreach (var q in quests)
            {
                if (q != null) _completedQuests.Add(q);
            }
        }
        Debug.Log($"[StoryManager] SkipPreviousChapters({targetChapter}): auto-completed {_completedQuests.Count} quests from chapters 1..{targetChapter - 1}.");
    }

    /// <summary>Returns the quest array for the given chapter (1-5), or null.</summary>
    private QuestData[] GetChapterQuests(int ch)
    {
        switch (ch)
        {
            case 1: return chapter1Quests;
            case 2: return chapter2Quests;
            case 3: return chapter3Quests;
            case 4: return chapter4Quests;
            case 5: return chapter5Quests;
            default: return null;
        }
    }

    /// <summary>
    /// Teleports the player to the save room of the target chapter and sets
    /// it as the respawn checkpoint. Also disables zombie spawners in all
    /// chapters before the target (they're completed — no zombies there).
    /// Called from Start when startingChapter > 1.
    /// </summary>
    private void TeleportPlayerToChapterSaveRoom(int targetChapter)
    {
        // Find the SaveRoom for the target chapter. SaveRoom names follow the
        // convention "SaveRoom_ChN".
        var saveRooms = FindObjectsByType<SaveRoom>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        SaveRoom target = null;
        foreach (var sr in saveRooms)
        {
            if (sr.gameObject.name == $"SaveRoom_Ch{targetChapter}")
            {
                target = sr;
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[StoryManager] TeleportPlayerToChapterSaveRoom: SaveRoom_Ch{targetChapter} not found. Player stays at default position.");
            return;
        }

        // Set the respawn checkpoint to this save room. Use
        // EffectiveRespawnPosition which works even before SaveRoom.Start()
        // has computed _checkpointPos.
        Vector3 checkpointPos = target.EffectiveRespawnPosition;
        SaveRoom.LastCheckpoint = checkpointPos;
        SaveRoom.LastCheckpointRotation = target.transform.rotation;
        Debug.Log($"[StoryManager] Set respawn checkpoint to {checkpointPos} (SaveRoom_Ch{targetChapter}).");

        // Teleport the player to the checkpoint. Use PlayerMovement.TeleportPlayer
        // if available (it handles rigidbody + camera + state reset properly),
        // otherwise fall back to direct position set.
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var pm = player.GetComponent<PlayerMovement>();
            if (pm != null)
            {
                pm.TeleportPlayer(checkpointPos, target.transform.rotation, true, true);
            }
            else
            {
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                player.transform.position = checkpointPos;
            }
            Debug.Log($"[StoryManager] Teleported player to {checkpointPos} (SaveRoom_Ch{targetChapter}).");
        }

        // Disable spawners in all chapters before the target — they're
        // completed and should have no zombies. The ChapterBoundary for
        // each completed chapter will also keep spawners off when the player
        // is outside, but we force-disable them here for safety.
        var boundaries = FindObjectsByType<ChapterBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var b in boundaries)
        {
            if (b.chapter < targetChapter)
            {
                b.SetSpawnersActive(false);
                Debug.Log($"[StoryManager] Disabled spawners for Ch{b.chapter} (completed, skip mode).");
            }
        }
    }

    /// <summary>The quest list for the current chapter.</summary>
    public QuestData[] GetCurrentChapterQuests()
    {
        switch (CurrentChapter)
        {
            case 1: return chapter1Quests;
            case 2: return chapter2Quests;
            case 3: return chapter3Quests;
            case 4: return chapter4Quests;
            case 5: return chapter5Quests;
            default: return null;
        }
    }

    /// <summary>The quest at the current index in the current chapter, or null if none.</summary>
    public QuestData GetCurrentQuest()
    {
        var list = GetCurrentChapterQuests();
        if (list == null || CurrentQuestIndex < 0 || CurrentQuestIndex >= list.Length)
            return null;
        return list[CurrentQuestIndex];
    }

    /// <summary>
    /// Called by QuestTrigger / kill counters / interactables when the active
    /// quest's objective is satisfied. Grants rewards, advances the quest index,
    /// and moves to the next chapter when the current chapter's quests are done.
    /// </summary>
    public void CompleteActiveQuest()
    {
        QuestData done = ActiveQuest;
        if (done == null)
        {
            Debug.LogWarning("[StoryManager] CompleteActiveQuest called but no active quest.");
            return;
        }

        _completedQuests.Add(done);
        QuestsCompletedThisChapter++;
        GrantRewards(done);
        OnQuestCompleted?.Invoke(done);

        Debug.Log($"[StoryManager] Quest completed: {done.title} (Ch{CurrentChapter} #{CurrentQuestIndex + 1})");

        // Advance to the next quest in the same chapter, or the next chapter.
        CurrentQuestIndex++;
        var list = GetCurrentChapterQuests();

        if (list == null || CurrentQuestIndex >= list.Length)
        {
            // Chapter finished — advance to the next chapter.
            AdvanceChapter();
        }
        else
        {
            SetActiveQuest(list[CurrentQuestIndex]);
        }
    }

    private void AdvanceChapter()
    {
        int oldChapter = CurrentChapter;
        if (CurrentChapter >= 5)
        {
            // Final chapter done — story complete.
            SetActiveQuest(null);
            StoryComplete = true;
            Debug.Log("[StoryManager] Story complete! All 5 chapters finished.");
            OnChapterChanged?.Invoke(oldChapter, -1);
            return;
        }

        CurrentChapter++;
        CurrentQuestIndex = 0;
        QuestsCompletedThisChapter = 0;
        PendingChapterEntry = true; // Wait for player to enter the new chapter area.
        Debug.Log($"[StoryManager] Advancing to Chapter {CurrentChapter}. Waiting for player to enter the chapter area before activating quests.");

        // Advance silently — the chapter transition cutscene is played when the
        // player reaches the new chapter's Save Room (see SaveRoom.chapterTransitionOnEnter).
        OnChapterChanged?.Invoke(oldChapter, CurrentChapter);

        // Switch background music to the new chapter.
        if (MusicManager.Instance != null)
            MusicManager.Instance.PlayChapterMusic(CurrentChapter);

        // Do NOT set the active quest here — it will be activated when the player
        // enters the new chapter's boundary (ChapterBoundary.OnTriggerEnter).
        SetActiveQuest(null);
    }

    /// <summary>
    /// Activates the first quest of the current chapter if we're in a pending
    /// state (chapter advanced but player hasn't entered the area yet). Called
    /// by ChapterBoundary when the player enters the current chapter's boundary.
    /// </summary>
    public void ActivatePendingChapterQuest()
    {
        if (!PendingChapterEntry) return;
        PendingChapterEntry = false;
        var list = GetCurrentChapterQuests();
        SetActiveQuest(list != null && list.Length > 0 ? list[0] : null);
        Debug.Log($"[StoryManager] Player entered Chapter {CurrentChapter} area. Activating first quest.");
    }


    /// <summary>
    /// Plays the chapter transition cutscene for the given chapter number.
    /// Called by SaveRoom when the player enters the new chapter's save room.
    /// Does nothing if playChapterTransitionCutscene is false or the cutscene
    /// is already playing.
    /// </summary>
    public void PlayChapterTransitionCutscene(int chapterNumber)
    {
        if (!playChapterTransitionCutscene || !isActiveAndEnabled) return;
        StartCoroutine(PlayChapterTransitionCutsceneRoutine(chapterNumber));
    }

    /// <summary>True while a chapter transition cutscene is playing.</summary>
    public bool IsChapterTransitionPlaying { get; private set; }

    private System.Collections.IEnumerator PlayChapterTransitionCutsceneRoutine(int chapterNumber)
    {
        if (IsChapterTransitionPlaying) yield break;
        IsChapterTransitionPlaying = true;

        // Get or create a CutscenePlayer for the transition.
        var cp = chapterCutscenePlayer;
        if (cp == null)
        {
            cp = GetComponent<CutscenePlayer>();
            if (cp == null) cp = gameObject.AddComponent<CutscenePlayer>();
        }

        // Configure the cutscene content for this chapter.
        int idx = chapterNumber - 2; // Ch1->Ch2 = index 0
        cp.title = (chapterTransitionTitles != null && idx >= 0 && idx < chapterTransitionTitles.Length && !string.IsNullOrEmpty(chapterTransitionTitles[idx]))
            ? chapterTransitionTitles[idx]
            : $"CHƯƠNG {chapterNumber}";
        cp.body = (chapterTransitionBodies != null && idx >= 0 && idx < chapterTransitionBodies.Length)
            ? chapterTransitionBodies[idx]
            : "";
        cp.fadeIn = chapterCutsceneFadeIn;
        cp.hold = chapterCutsceneHold;
        cp.fadeOut = chapterCutsceneFadeOut;

        bool cutsceneDone = false;
        cp.Play(() => cutsceneDone = true);

        while (!cutsceneDone) yield return null;

        IsChapterTransitionPlaying = false;
    }

    public void ForceSetQuest(int chapter, int questIndex)
    {
        if (chapter != CurrentChapter) return;
        var list = GetCurrentChapterQuests();
        if (list == null || questIndex < 0 || questIndex >= list.Length) return;
        CurrentQuestIndex = questIndex;
        SetActiveQuest(list[questIndex]);
    }

    private void SetActiveQuest(QuestData quest)
    {
        QuestData prev = ActiveQuest;
        ActiveQuest = quest;
        if (quest != null)
            Debug.Log($"[StoryManager] New active quest: {quest.title}");
        OnActiveQuestChanged?.Invoke(prev, quest);
    }

    private void GrantRewards(QuestData quest)
    {
        if (quest == null) return;

        // EXP reward.
        if (quest.expReward > 0f)
        {
            var em = FindAnyObjectByType<ExperienceManager>();
            if (em != null)
                em.AddExperience(quest.expReward);
            else
                Debug.LogWarning("[StoryManager] No ExperienceManager in scene; EXP reward skipped.");
        }

        // Journal reward — register it with the CollectibleManager so the journal
        // UI / gallery / True Ending condition sees it without a scene pickup.
        if (quest.journalReward != null)
        {
            var cm = CollectibleManager.Instance;
            if (cm != null)
                cm.Collect(quest.journalReward);
            else
                Debug.LogWarning("[StoryManager] No CollectibleManager in scene; journal reward skipped.");
        }
    }

    /// <summary>True if the given quest has been completed.</summary>
    public bool IsQuestCompleted(QuestData quest)
    {
        return quest != null && _completedQuests.Contains(quest);
    }

    /// <summary>Total quests completed across all chapters.</summary>
    public int TotalQuestsCompleted => _completedQuests.Count;

    /// <summary>All main story quests completed so far, in completion order.</summary>
    public IReadOnlyList<QuestData> CompletedQuests => _completedQuests;
}

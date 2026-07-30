using UnityEngine;

/// <summary>
/// Side-quest variant of KillCountObjective. Completes a side quest when a
/// target number of zombie kills is reached. Routes completion to
/// SideQuestManager instead of StoryManager so the main story is not advanced.
///
/// The objective auto-starts when the side quest becomes active in
/// SideQuestManager. Kill counting mirrors KillCountObjective (ScoreManager.kills
/// delta).
///
/// Optional: assign spawners to activate them when the player enters the
/// trigger area (so zombies spawn locally for the kill objective) and disable
/// them when the quest completes.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SideQuestKillObjective : MonoBehaviour
{
    [Tooltip("Side quest this objective completes.")]
    public QuestData sideQuest;

    [Tooltip("Number of kills required to complete the side quest.")]
    public int targetCount = 5;

    [Tooltip("Optional cutscene to play before completing the side quest.")]
    public CutscenePlayer completionCutscene;

    [Header("Spawners")]
    [Tooltip("Spawners to activate when the player enters this trigger (so zombies spawn locally for the kill objective). Disabled when the quest completes.")]
    public MonoBehaviour[] spawnersToActivate;

    private bool _listening;
    private int _startKills;
    private bool _done;
    private bool _spawnersActive;

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
        // If the side quest is already active, start listening immediately.
        if (!_listening && sideQuest != null
            && SideQuestManager.Instance != null
            && SideQuestManager.Instance.IsActive(sideQuest))
            StartListening();
    }

    private void OnDisable()
    {
        if (SideQuestManager.Instance != null)
            SideQuestManager.Instance.OnSideQuestActivated -= HandleActivated;
        _listening = false;
    }

    private void Subscribe()
    {
        if (SideQuestManager.Instance == null) return;
        SideQuestManager.Instance.OnSideQuestActivated -= HandleActivated;
        SideQuestManager.Instance.OnSideQuestActivated += HandleActivated;
    }

    private void HandleActivated(QuestData quest)
    {
        if (sideQuest != null && quest == sideQuest)
            StartListening();
    }

    private void StartListening()
    {
        if (_listening) return;
        _listening = true;
        _startKills = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
        _done = false;
        Debug.Log($"[SideQuestKillObjective] Started. Need {targetCount} kills from {_startKills}.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (sideQuest == null) return;
        var sqm = SideQuestManager.Instance;
        if (sqm == null || !sqm.IsActive(sideQuest)) return;
        if (_done) return;

        SetSpawnersActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        // Don't disable spawners on exit — let the kill objective finish naturally.
        // Spawners are disabled when the quest completes.
    }

    private void SetSpawnersActive(bool active)
    {
        if (_spawnersActive == active) return;
        _spawnersActive = active;
        if (spawnersToActivate == null) return;
        foreach (var s in spawnersToActivate)
        {
            if (s != null) s.enabled = active;
        }
        Debug.Log($"[SideQuestKillObjective] Spawners {(active ? "activated" : "deactivated")} for {sideQuest.title}.");
    }

    private void Update()
    {
        if (!_listening || _done) return;
        if (sideQuest == null) return;
        var sqm = SideQuestManager.Instance;
        if (sqm == null || !sqm.IsActive(sideQuest)) return;

        int current = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
        if (current - _startKills >= targetCount)
        {
            _done = true;
            _listening = false;
            SetSpawnersActive(false);
            Debug.Log($"[SideQuestKillObjective] Target reached ({current - _startKills}/{targetCount}). Completing side quest.");
            if (completionCutscene != null)
                completionCutscene.Play(() => sqm.CompleteSideQuest(sideQuest));
            else
                sqm.CompleteSideQuest(sideQuest);
        }
    }
}

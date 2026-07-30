using UnityEngine;

/// <summary>
/// Completes the active StoryManager quest when a target number of zombie kills
/// is reached. Watches ScoreManager.kills (incremented by ZombieAI on death) and
/// fires when `kills += targetCount` since the objective started.
///
/// Used for "kill N zombies" quests like Chapter 1 Quest 2 (kill the first
/// zombie) and Chapter 2 Quest 3 (clear the hospital exterior). The objective
/// starts listening when its QuestTrigger is enabled (via `startOnEnable`) or
/// when the matching quest becomes active.
/// </summary>
public class KillCountObjective : MonoBehaviour
{
    [Header("Quest")]
    [Tooltip("Quest this objective completes. If set, the objective auto-starts when this quest becomes active. If null, use startOnEnable.")]
    public QuestData targetQuest;

    [Tooltip("Number of kills required to complete the quest.")]
    public int targetCount = 1;

    [Tooltip("If true and targetQuest is null, start listening as soon as this component is enabled.")]
    public bool startOnEnable = false;

    [Header("Optional Completion Cutscene")]
    [Tooltip("Cutscene to play before completing the quest (e.g. chapter transition). Optional.")]
    public CutscenePlayer completionCutscene;

    private bool _listening;
    private int _startKills;
    private bool _done;
    private bool _subscribed;

    private void OnEnable()
    {
        Subscribe();
        if (startOnEnable && targetQuest == null)
            StartListening();
    }

    private void Start()
    {
        // Fallback: if OnEnable ran before StoryManager.Awake (which sets
        // Instance), the subscription was silently skipped. Start is
        // guaranteed to run after all Awake calls, so Instance is always set.
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || StoryManager.Instance == null) return;
        StoryManager.Instance.OnActiveQuestChanged += HandleQuestChanged;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
        _subscribed = false;
        _listening = false;
    }

    private void HandleQuestChanged(QuestData oldQuest, QuestData newQuest)
    {
        if (targetQuest != null && newQuest == targetQuest)
            StartListening();
    }

    private void StartListening()
    {
        if (_listening) return;
        _listening = true;
        _startKills = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
        _done = false;
        Debug.Log($"[KillCountObjective] Started. Need {targetCount} kill(s) from {_startKills}.");
    }

    private void Update()
    {
        if (!_listening || _done) return;

        var sm = StoryManager.Instance;
        if (sm == null || sm.ActiveQuest == null) return;

        // Safety: only count kills while the right quest is active.
        if (targetQuest != null && sm.ActiveQuest != targetQuest) return;

        int current = ScoreManager.Instance != null ? ScoreManager.Instance.kills : 0;
        if (current - _startKills >= targetCount)
        {
            _done = true;
            _listening = false;
            Debug.Log($"[KillCountObjective] Target reached ({current - _startKills}/{targetCount}). Completing quest.");
            if (completionCutscene != null)
                completionCutscene.Play(() => sm.CompleteActiveQuest());
            else
                sm.CompleteActiveQuest();
        }
    }
}

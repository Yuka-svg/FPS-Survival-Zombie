using UnityEngine;

/// <summary>
/// Completes the active StoryManager quest when every Collectible in
/// `requiredCollectibles` has been picked up by the player. Mirrors
/// KillCountObjective but tracks specific collectible pickups instead of kills.
///
/// Used for "collect N items" quests like Chapter 2 Quest 4 (find patient
/// records). The objective starts listening when its target quest becomes
/// active (or immediately via startOnEnable when targetQuest is null).
/// </summary>
public class CollectibleQuestObjective : MonoBehaviour
{
    [Header("Quest")]
    [Tooltip("Quest this objective completes. If set, the objective auto-starts when this quest becomes active. If null, use startOnEnable.")]
    public QuestData targetQuest;

    [Tooltip("If true and targetQuest is null, start listening as soon as this component is enabled.")]
    public bool startOnEnable = false;

    [Header("Required Collectibles")]
    [Tooltip("Every Collectible in this list must be picked up to complete the quest.")]
    public Collectible[] requiredCollectibles;

    [Header("Optional Completion Cutscene")]
    [Tooltip("Cutscene to play before completing the quest (e.g. story beat). Optional.")]
    public CutscenePlayer completionCutscene;

    [Tooltip("Delay (seconds) between cutscene end and quest completion. 0 = instant.")]
    public float delayAfterCutscene = 0f;

    private bool _listening;
    private bool _done;

    /// <summary>Number of required collectibles picked up so far.</summary>
    public int PickedCount
    {
        get
        {
            if (requiredCollectibles == null) return 0;
            int n = 0;
            for (int i = 0; i < requiredCollectibles.Length; i++)
                if (requiredCollectibles[i] != null && requiredCollectibles[i].IsPicked) n++;
            return n;
        }
    }

    /// <summary>Total number of non-null required collectibles.</summary>
    public int RequiredCount
    {
        get
        {
            if (requiredCollectibles == null) return 0;
            int n = 0;
            for (int i = 0; i < requiredCollectibles.Length; i++)
                if (requiredCollectibles[i] != null) n++;
            return n;
        }
    }

    private void OnEnable()
    {
        Subscribe();
        if (startOnEnable && targetQuest == null)
            StartListening();
    }

    private void Start()
    {
        // Fallback: if OnEnable ran before StoryManager.Awake, Instance was null
        // and the subscription was missed. Re-subscribe here and check if the
        // target quest is already active (Start runs after all Awake/OnEnable).
        Subscribe();
        if (!_listening && targetQuest != null
            && StoryManager.Instance != null
            && StoryManager.Instance.ActiveQuest == targetQuest)
            StartListening();
    }

    private void OnDisable()
    {
        if (StoryManager.Instance != null)
            StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
        _listening = false;
    }

    private void Subscribe()
    {
        if (StoryManager.Instance == null) return;
        // Avoid double-subscription.
        StoryManager.Instance.OnActiveQuestChanged -= HandleQuestChanged;
        StoryManager.Instance.OnActiveQuestChanged += HandleQuestChanged;
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
        _done = false;

        // Warn about any null entries in the required collectibles array.
        if (requiredCollectibles != null)
        {
            for (int i = 0; i < requiredCollectibles.Length; i++)
            {
                if (requiredCollectibles[i] == null)
                    Debug.LogWarning($"[CollectibleQuestObjective] {name}: requiredCollectibles[{i}] is null! Quest may not complete.");
            }
        }

        Debug.Log($"[CollectibleQuestObjective] Started. Need {RequiredCount} collectible(s); already picked {PickedCount}.");
    }

    private void Update()
    {
        if (_done) return;

        var sm = StoryManager.Instance;
        if (sm == null || sm.ActiveQuest == null) return;

        // Safety: only count while the right quest is active.
        if (targetQuest != null && sm.ActiveQuest != targetQuest) return;

        // Auto-start listening if the quest is active but we missed the event
        // (e.g. OnEnable ran before StoryManager.Awake so the subscription was
        // late, or the quest was already active when this component was added).
        if (!_listening) StartListening();

        if (RequiredCount > 0 && PickedCount >= RequiredCount)
        {
            _done = true;
            _listening = false;
            Debug.Log($"[CollectibleQuestObjective] All {RequiredCount} collectible(s) picked. Completing quest.");

            if (completionCutscene != null)
            {
                completionCutscene.Play(() =>
                {
                    if (delayAfterCutscene > 0f)
                        StartCoroutine(DelayedComplete(delayAfterCutscene));
                    else
                        sm.CompleteActiveQuest();
                });
            }
            else
            {
                sm.CompleteActiveQuest();
            }
        }
    }

    private System.Collections.IEnumerator DelayedComplete(float delay)
    {
        yield return new WaitForSeconds(delay);
        StoryManager.Instance?.CompleteActiveQuest();
    }
}

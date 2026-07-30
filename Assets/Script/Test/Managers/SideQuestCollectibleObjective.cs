using UnityEngine;

/// <summary>
/// Side-quest variant of CollectibleQuestObjective. Completes a side quest when
/// every Collectible in requiredCollectibles has been picked up. Routes
/// completion to SideQuestManager instead of StoryManager so the main story is
/// not advanced.
///
/// The objective auto-starts when the side quest becomes active in
/// SideQuestManager.
/// </summary>
public class SideQuestCollectibleObjective : MonoBehaviour
{
    [Tooltip("Side quest this objective completes.")]
    public QuestData sideQuest;

    [Tooltip("Every Collectible in this list must be picked up to complete the side quest.")]
    public Collectible[] requiredCollectibles;

    [Tooltip("Optional cutscene to play before completing the side quest.")]
    public CutscenePlayer completionCutscene;

    private bool _listening;
    private bool _done;

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

    private void OnEnable() { Subscribe(); }

    private void Start()
    {
        Subscribe();
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
        _done = false;
        Debug.Log($"[SideQuestCollectibleObjective] Started. Need {RequiredCount} collectible(s); already picked {PickedCount}.");
    }

    private void Update()
    {
        if (_done) return;
        if (sideQuest == null) return;
        var sqm = SideQuestManager.Instance;
        if (sqm == null || !sqm.IsActive(sideQuest)) return;
        if (!_listening) StartListening();

        if (RequiredCount > 0 && PickedCount >= RequiredCount)
        {
            _done = true;
            _listening = false;
            Debug.Log($"[SideQuestCollectibleObjective] All {RequiredCount} collectible(s) picked. Completing side quest.");
            if (completionCutscene != null)
                completionCutscene.Play(() => sqm.CompleteSideQuest(sideQuest));
            else
                sqm.CompleteSideQuest(sideQuest);
        }
    }
}

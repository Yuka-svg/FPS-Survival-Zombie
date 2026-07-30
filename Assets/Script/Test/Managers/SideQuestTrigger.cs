using UnityEngine;

/// <summary>
/// Companion component for side-quest QuestTriggers. Attach alongside a
/// QuestTrigger (or QuestInteractable) that is meant to complete a SIDE quest
/// instead of the main story quest.
///
/// Why this exists:
/// The existing QuestTrigger/KillCountObjective/CollectibleQuestObjective all
/// call StoryManager.CompleteActiveQuest() on completion. For side quests we
/// must NOT advance the main story — instead we call
/// SideQuestManager.CompleteSideQuest(). Rather than fork all those components,
/// SideQuestTrigger intercepts completion: it sets the QuestTrigger's targetQuest
/// to the side quest (so the trigger only fires when the side quest is the
/// "active" one) and, when the trigger fires, routes completion to
/// SideQuestManager instead of StoryManager.
///
/// Usage:
/// - Add a QuestTrigger (Mode=OnPlayerEnter or Manual) + SideQuestTrigger to the
///   side-quest objective location.
/// - Set sideQuest to the side-quest QuestData asset.
/// - The QuestTrigger's targetQuest will be auto-set to sideQuest on Awake so
///   the trigger only fires for this side quest.
///
/// For kill-count / collectible side quests, use SideQuestKillObjective or
/// SideQuestCollectibleObjective instead (they mirror KillCountObjective and
/// CollectibleQuestObjective but route completion to SideQuestManager).
/// </summary>
[RequireComponent(typeof(QuestTrigger))]
public class SideQuestTrigger : MonoBehaviour
{
    [Tooltip("The side quest this trigger completes.")]
    public QuestData sideQuest;

    [Tooltip("Optional cutscene to play before completing the side quest.")]
    public CutscenePlayer cutscene;

    private QuestTrigger _trigger;
    private bool _fired;

    private void Awake()
    {
        _trigger = GetComponent<QuestTrigger>();
        // Force the QuestTrigger to only fire for our side quest. We also set
        // its mode to Manual so it won't auto-fire on player enter — instead
        // we control completion via SideQuestManager. For OnPlayerEnter side
        // quests, we handle the trigger ourselves below.
        if (_trigger != null)
        {
            _trigger.targetQuest = sideQuest;
        }
    }

    private void Start()
    {
        // Re-apply in case the QuestTrigger overwrote targetQuest after Awake.
        if (_trigger != null) _trigger.targetQuest = sideQuest;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_fired) return;
        if (!other.CompareTag("Player") || other.gameObject.layer != LayerMask.NameToLayer("Player")) return;
        if (sideQuest == null) return;
        var sqm = SideQuestManager.Instance;
        if (sqm == null || !sqm.IsActive(sideQuest)) return;

        _fired = true;
        if (cutscene != null)
            cutscene.Play(() => sqm.CompleteSideQuest(sideQuest));
        else
            sqm.CompleteSideQuest(sideQuest);
    }

    /// <summary>Externally-driven completion (e.g. from a kill counter).</summary>
    public void Complete()
    {
        if (_fired) return;
        if (sideQuest == null) return;
        var sqm = SideQuestManager.Instance;
        if (sqm == null || !sqm.IsActive(sideQuest)) return;

        _fired = true;
        if (cutscene != null)
            cutscene.Play(() => sqm.CompleteSideQuest(sideQuest));
        else
            sqm.CompleteSideQuest(sideQuest);
    }
}

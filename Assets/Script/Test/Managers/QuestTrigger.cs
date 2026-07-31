using UnityEngine;

/// <summary>
/// Completes the active StoryManager quest when the player enters the trigger
/// volume or when Complete() is called explicitly (e.g. from a kill counter or
/// interactable). Place one of these at each quest objective location.
///
/// Modes:
/// - OnPlayerEnter: quest completes the moment the player steps into the trigger.
/// - Manual: completion is driven externally via Complete() (used by kill-count
///   goals, interaction events, wave-clear callbacks, etc.).
/// </summary>
[RequireComponent(typeof(Collider))]
public class QuestTrigger : MonoBehaviour
{
    public enum Mode { OnPlayerEnter, Manual }

    [Header("Quest")]
    [Tooltip("Quest this trigger completes. If set, the trigger only fires when " +
             "this quest is the active one. Leave null to complete whatever the " +
             "active quest is (less safe but useful for simple linear chapters).")]
    public QuestData targetQuest;

    [Tooltip("How completion is triggered.")]
    public Mode mode = Mode.OnPlayerEnter;

    [Tooltip("If true, this trigger can only fire once. Usually true.")]
    public bool oneShot = true;

    [Header("Optional Cutscene")]
    [Tooltip("Cutscene to play before completing the quest. Optional.")]
    public CutscenePlayer cutscene;

    [Tooltip("Delay (seconds) between cutscene end and quest completion. 0 = instant.")]
    public float delayAfterCutscene = 0f;

    private bool _fired;

    private void Reset()
    {
        // Ensure the collider is a trigger by default.
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    public static readonly System.Collections.Generic.List<QuestTrigger> ActiveTriggers = new System.Collections.Generic.List<QuestTrigger>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveTriggers.Clear();
    }

    private void OnEnable()
    {
        if (!ActiveTriggers.Contains(this))
            ActiveTriggers.Add(this);
    }

    private void OnDisable()
    {
        ActiveTriggers.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveTriggers.Remove(this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (mode != Mode.OnPlayerEnter) return;
        if (!other.CompareTag("Player") || other.gameObject.layer != LayerMask.NameToLayer("Player")) return;
        TryFire();
    }

    /// <summary>Externally-driven completion (kill count, interaction, wave clear, etc.).</summary>
    public void Complete()
    {
        if (mode != Mode.Manual) return;
        TryFire();
    }

    private void TryFire()
    {
        if (_fired && oneShot) return;

        var sm = StoryManager.Instance;
        if (sm == null)
        {
            Debug.LogWarning($"[QuestTrigger] No StoryManager in scene; {name} cannot complete a quest.");
            return;
        }

        // If a specific quest is assigned, only fire when it's the active quest.
        if (targetQuest != null && sm.ActiveQuest != targetQuest)
        {
            Debug.Log($"[QuestTrigger] {name}: active quest is {sm.ActiveQuest?.name}, not {targetQuest.name}. Ignored.");
            return;
        }

        if (sm.ActiveQuest == null)
        {
            Debug.LogWarning($"[QuestTrigger] {name}: no active quest to complete.");
            return;
        }

        if (oneShot) _fired = true;

        if (cutscene != null)
        {
            cutscene.Play(() =>
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

    private System.Collections.IEnumerator DelayedComplete(float delay)
    {
        yield return new WaitForSeconds(delay);
        StoryManager.Instance?.CompleteActiveQuest();
    }
}
